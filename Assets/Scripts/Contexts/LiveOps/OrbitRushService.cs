using System;
using Project.Contexts.Application.Economy;
using Project.Core.Events;

namespace Project.Contexts.LiveOps
{
    public readonly struct OrbitRushChanged : IEvent { }

    public enum MilestoneKind { Flag, Chest, Treasure }

    public readonly struct OrbitRushMilestone
    {
        public readonly int Step;
        public readonly MilestoneKind Kind;
        public readonly int Coins;

        public OrbitRushMilestone(int step, MilestoneKind kind, int coins)
        {
            Step = step;
            Kind = kind;
            Coins = coins;
        }
    }

    // Back-to-back windows on the local clock; progress is levels won inside the current one.
    public sealed class OrbitRushService
    {
        public const int WindowDays = 3;
        public static readonly DateTime Anchor = new(2026, 1, 1);
        public static readonly OrbitRushMilestone[] Milestones =
        {
            new(2, MilestoneKind.Flag, 25),
            new(4, MilestoneKind.Chest, 100),
            new(9, MilestoneKind.Chest, 200),
            new(11, MilestoneKind.Flag, 50),
            new(13, MilestoneKind.Flag, 75),
            new(14, MilestoneKind.Treasure, 500)
        };
        public static int Steps => Milestones[^1].Step;

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

        public int Progress => Math.Min(Model.Wins, Steps);

        public bool IsReached(int milestone) => Progress >= Milestones[milestone].Step;
        public bool IsClaimed(int milestone) => (Model.Claimed & (1 << milestone)) != 0;
        public bool CanClaim(int milestone) => IsReached(milestone) && !IsClaimed(milestone);

        public bool HasClaimable
        {
            get
            {
                for (var i = 0; i < Milestones.Length; i++)
                    if (CanClaim(i)) return true;
                return false;
            }
        }

        public void RecordWin()
        {
            if (Model.Wins >= Steps) return;
            Model.Wins++;
            _data.Save();
            EventBus<OrbitRushChanged>.Raise(default);
        }

        // Returns the coins paid; 0 when not claimable.
        public int Claim(int milestone)
        {
            if (milestone < 0 || milestone >= Milestones.Length || !CanClaim(milestone)) return 0;
            var coins = Milestones[milestone].Coins;
            using (_data.Batch())
            {
                Model.Claimed |= 1 << milestone;
                _wallet.Add(coins);
                _data.Save();
            }
            EventBus<OrbitRushChanged>.Raise(default);
            return coins;
        }

        // A new window starts from zero; unclaimed rewards of the old one are gone.
        private void Sync()
        {
            var model = _data.Model.OrbitRush;
            var window = Window;
            if (model.Window == window) return;
            model.Window = window;
            model.Wins = 0;
            model.Claimed = 0;
            _data.Save();
        }
    }
}
