using System;
using System.Collections.Generic;
using Project.Contexts.Application.Economy;
using Project.Core.Events;

namespace Project.Contexts.LiveOps
{
    public readonly struct OrbitRushChanged : IEvent { }

    public enum OrbitRushState
    {
        // Below the activation level.
        Locked,
        // A new window: the intro opens once.
        Intro,
        // Waiting for the player to join.
        Join,
        Racing,
        // Reached the target among the winners; the stage gift waits to be claimed.
        StageWon,
        // Others filled the podium or the stage time ran out.
        StageLost,
        // Grand prize given; waits for the next window.
        Completed
    }

    public readonly struct OrbitRushRacer
    {
        public readonly string Name;
        public readonly int Score;
        public readonly bool IsPlayer;
        // Seconds into the stage when the target was reached; MaxValue while racing.
        public readonly double FinishedAt;

        public OrbitRushRacer(string name, int score, bool isPlayer, double finishedAt)
        {
            Name = name;
            Score = score;
            IsPlayer = isPlayer;
            FinishedAt = finishedAt;
        }
    }

    // Offline staged race: rooms of five, the first three to the target win the stage; wins move by the streak gear.
    public sealed class OrbitRushService
    {
        public const int ActivationLevel = 34;
        public const int WindowDays = 3;
        public const int Capacity = 5;
        public const int Winners = 3;
        public const double StageHours = 12;
        public const int GrandPrize = 500;
        public static readonly DateTime Anchor = new(2026, 1, 1);
        public static readonly int[] Targets = { 5, 8, 12, 16, 20 };
        public static readonly int[] StageRewards = { 50, 100, 150, 200, 250 };
        public static readonly int[] RankRewards = { 30, 20, 10 };
        public static readonly int[] GearMultipliers = { 1, 2, 3, 4, 5 };
        public static int Stages => Targets.Length;

        private readonly PlayerData _data;
        private readonly WalletService _wallet;
        private readonly Func<DateTime> _localNow;

        public OrbitRushService(PlayerData data, WalletService wallet, Func<DateTime> localNow = null)
        {
            _data = data;
            _wallet = wallet;
            _localNow = localNow ?? (() => DateTime.Now);
        }

        private OrbitRushModel Model
        {
            get
            {
                Sync();
                return _data.Model.OrbitRush;
            }
        }

        public int Window => (int)Math.Floor((_localNow() - Anchor).TotalDays / WindowDays);
        public TimeSpan TimeLeft => Anchor.AddDays((Window + 1) * WindowDays) - _localNow();

        public OrbitRushState State => (OrbitRushState)Model.State;
        public bool IsVisible => State != OrbitRushState.Locked;
        public int Stage => Math.Min(Model.Stage, Stages - 1);
        public int StagesWon => Model.Stage;
        public int Target => Targets[Stage];
        public int Score => Model.Score;
        public int Gear => Model.Gear;
        public int Multiplier => GearMultipliers[Model.Gear];
        public int WonRank => Model.WonRank;

        // The stage ends with the window if that comes first.
        public TimeSpan StageTimeLeft
        {
            get
            {
                var end = new DateTime(Model.StageStartTicks).AddHours(StageHours);
                var left = end - _localNow();
                return left < TimeLeft ? left : TimeLeft;
            }
        }

        // What the reward claim pays: the stage gift and the finish place.
        public int StageReward => StageRewards[Stage] + (Model.WonRank > 0 ? RankRewards[Model.WonRank - 1] : 0);

        // Ranked best first; finished racers by finish time.
        public List<OrbitRushRacer> Racers()
        {
            var model = Model;
            var racers = new List<OrbitRushRacer>(Capacity);
            var elapsed = Elapsed(model);
            for (var i = 1; i < Capacity; i++) racers.Add(OrbitRushBots.Racer(model, i, elapsed));
            var playerFinished = model.Score >= Target ? (model.WonRank > 0 ? OrbitRushBots.FinishBefore(model, model.WonRank) : elapsed) : double.MaxValue;
            racers.Add(new OrbitRushRacer(null, model.Score, true, playerFinished));
            racers.Sort((a, b) =>
            {
                if (a.FinishedAt != b.FinishedAt) return a.FinishedAt.CompareTo(b.FinishedAt);
                if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                return a.IsPlayer ? -1 : b.IsPlayer ? 1 : 0;
            });
            return racers;
        }

        public int PlayerRank
        {
            get
            {
                var racers = Racers();
                for (var i = 0; i < racers.Count; i++)
                    if (racers[i].IsPlayer) return i + 1;
                return Capacity;
            }
        }

        // Points the Home icon has not counted up yet.
        public int PendingCollect => State is OrbitRushState.Racing or OrbitRushState.StageWon ? Math.Max(0, Model.Score - Model.ShownScore) : 0;

        public bool HasClaimable => State is OrbitRushState.StageWon or OrbitRushState.Intro;

        public void MarkCollected()
        {
            if (Model.ShownScore == Model.Score) return;
            Model.ShownScore = Model.Score;
            _data.Save();
        }

        public void MarkIntroShown()
        {
            if (State != OrbitRushState.Intro) return;
            Set(OrbitRushState.Join);
        }

        public bool Join()
        {
            if (State is not (OrbitRushState.Intro or OrbitRushState.Join)) return false;
            StartStage(_data.Model.OrbitRush, 0);
            Changed();
            return true;
        }

        // A campaign win: points by the gear, then the gear goes up.
        public void RecordWin()
        {
            if (State != OrbitRushState.Racing) return;
            var model = _data.Model.OrbitRush;
            model.Score = Math.Min(Target, model.Score + GearMultipliers[model.Gear]);
            model.Gear = Math.Min(GearMultipliers.Length - 1, model.Gear + 1);
            if (model.Score >= Target)
            {
                model.WonRank = OrbitRushBots.Finished(model, Elapsed(model)) + 1;
                model.State = (int)OrbitRushState.StageWon;
            }
            Changed();
        }

        // A try that ended without a win clears the streak.
        public void RecordLoss()
        {
            var model = _data.Model.OrbitRush;
            if (model.Gear == 0) return;
            model.Gear = 0;
            Changed();
        }

        // Pays the stage gift and place, then opens the next stage or gives the grand prize.
        public int ClaimStage()
        {
            if (State != OrbitRushState.StageWon) return 0;
            var model = _data.Model.OrbitRush;
            var coins = StageReward;
            using (_data.Batch())
            {
                model.Stage++;
                if (model.Stage >= Stages)
                {
                    coins += GrandPrize;
                    model.State = (int)OrbitRushState.Completed;
                    model.WonRank = 0;
                }
                else StartStage(model, 0);
                model.Earned += coins;
                _wallet.Add(coins);
                _data.Save();
            }
            Changed();
            return coins;
        }

        // A new room for the same stage.
        public bool Retry()
        {
            if (State != OrbitRushState.StageLost) return false;
            var model = _data.Model.OrbitRush;
            StartStage(model, model.Attempt + 1);
            Changed();
            return true;
        }

        // The ended window's result (stages won, coins paid in it), once; false when there is none.
        public bool TryTakeFinish(out int stagesWon, out int coins)
        {
            var model = Model;
            stagesWon = model.FinishStage;
            coins = model.FinishCoins;
            if (model.FinishStage < 0) return false;
            model.FinishStage = -1;
            model.FinishCoins = 0;
            _data.Save();
            return true;
        }

        private void StartStage(OrbitRushModel model, int attempt)
        {
            model.State = (int)OrbitRushState.Racing;
            model.Score = 0;
            model.ShownScore = 0;
            model.WonRank = 0;
            model.Attempt = attempt;
            model.StageStartTicks = _localNow().Ticks;
        }

        private void Set(OrbitRushState state)
        {
            _data.Model.OrbitRush.State = (int)state;
            Changed();
        }

        private void Changed()
        {
            _data.Save();
            EventBus<OrbitRushChanged>.Raise(default);
        }

        // Window rollover, the level gate and the stage clock.
        private static bool Clamp(OrbitRushModel model)
        {
            var before = (model.Gear, model.Stage, model.WonRank, model.State);
            model.Gear = Math.Clamp(model.Gear, 0, GearMultipliers.Length - 1);
            model.Stage = Math.Clamp(model.Stage, 0, Stages);
            model.WonRank = Math.Clamp(model.WonRank, 0, Winners);
            model.State = Math.Clamp(model.State, (int)OrbitRushState.Locked, (int)OrbitRushState.Completed);
            return before != (model.Gear, model.Stage, model.WonRank, model.State);
        }

        private void Sync()
        {
            var model = _data.Model.OrbitRush;
            var changed = Clamp(model);
            var window = Window;
            // Only forward: a clock or time zone moved back keeps the event it already has.
            if (window > model.Window)
            {
                // The payout and the new window are one write, so a crash between them can't pay twice.
                using (_data.Batch())
                {
                    if (model.Window >= 0) EndWindow(model);
                    var finish = (model.FinishStage, model.FinishCoins);
                    _data.Model.OrbitRush = model = new OrbitRushModel { Window = window, FinishStage = finish.FinishStage, FinishCoins = finish.FinishCoins };
                    _data.Save();
                }
            }

            var unlocked = _data.Model.Level >= ActivationLevel;
            if (!unlocked && model.State != (int)OrbitRushState.Locked)
            {
                model.State = (int)OrbitRushState.Locked;
                changed = true;
            }
            else if (unlocked && model.State == (int)OrbitRushState.Locked)
            {
                model.State = (int)OrbitRushState.Intro;
                changed = true;
            }

            if (model.State == (int)OrbitRushState.Racing)
            {
                var elapsed = Elapsed(model);
                if (OrbitRushBots.Finished(model, elapsed) >= Winners || elapsed >= StageHours * 3600)
                {
                    model.State = (int)OrbitRushState.StageLost;
                    changed = true;
                }
            }

            if (changed) _data.Save();
        }

        // Anything already won is paid when the window closes.
        private void EndWindow(OrbitRushModel model)
        {
            var state = (OrbitRushState)model.State;
            if (state is OrbitRushState.Locked or OrbitRushState.Intro or OrbitRushState.Join) return;
            var coins = 0;
            if (state == OrbitRushState.StageWon)
            {
                var stage = Math.Min(model.Stage, Stages - 1);
                coins = StageRewards[stage] + (model.WonRank > 0 ? RankRewards[model.WonRank - 1] : 0);
                model.Stage++;
                if (model.Stage >= Stages) coins += GrandPrize;
                _wallet.Add(coins);
            }
            model.FinishStage = model.Stage;
            model.FinishCoins = model.Earned + coins;
        }

        private double Elapsed(OrbitRushModel model) =>
            Math.Max(0, (_localNow() - new DateTime(model.StageStartTicks)).TotalSeconds);
    }
}
