using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Application.Economy;
using Project.Contexts.LiveOps;
using Project.Core.Save;
using UnityEditor;

namespace Project.Tests
{
    public sealed class LiveOpsTests
    {
        private string _folder;
        private DateTime _now;
        private PlayerData _data;
        private WalletService _wallet;
        private DailyRewardService _daily;
        private OrbitRushService _rush;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ballsort-liveops-" + Guid.NewGuid().ToString("N"));
            _now = new DateTime(2026, 11, 22, 10, 0, 0);
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // Reloads from disk, like an app restart.
        private void Build()
        {
            _data = new PlayerData(new SaveService(_folder));
            _wallet = new WalletService(_data);
            _daily = new DailyRewardService(_data, _wallet, () => _now);
            _rush = new OrbitRushService(_data, _wallet, () => _now);
        }

        [Test]
        public void DailyClaimsOncePerLocalDay()
        {
            var coins = _wallet.Coins;
            Assert.IsTrue(_daily.CanClaim);
            Assert.AreEqual(0, _daily.CurrentIndex);
            Assert.AreEqual(50, _daily.Claim());
            Assert.AreEqual(coins + 50, _wallet.Coins);
            Assert.IsFalse(_daily.CanClaim);
            Assert.AreEqual(0, _daily.Claim());

            _now = _now.Date.AddHours(23).AddMinutes(59);
            Assert.IsFalse(_daily.CanClaim);
            _now = _now.Date.AddDays(1);
            Build();
            Assert.IsTrue(_daily.CanClaim);
            Assert.AreEqual(1, _daily.CurrentIndex);
            Assert.IsTrue(_daily.IsClaimed(0));
            Assert.IsFalse(_daily.IsClaimed(1));
        }

        [Test]
        public void DailyStreakRunsSevenDaysThenRestarts()
        {
            for (var day = 0; day < 7; day++)
            {
                Assert.AreEqual(DailyRewardService.Rewards[day], _daily.Claim());
                _now = _now.AddDays(1);
            }
            Assert.AreEqual(0, _daily.CurrentIndex);
            Assert.AreEqual(50, _daily.Claim());
        }

        [Test]
        public void SkippedDayResetsTheStreak()
        {
            _daily.Claim();
            _now = _now.AddDays(1);
            _daily.Claim();
            _now = _now.AddDays(2);
            Assert.AreEqual(0, _daily.CurrentIndex);
            Assert.IsFalse(_daily.IsClaimed(0));
            Assert.AreEqual(50, _daily.Claim());
        }

        [Test]
        public void ClockSetBackNeverPaysAgain()
        {
            _daily.Claim();
            _now = _now.AddDays(-3);
            Assert.IsFalse(_daily.CanClaim);
            Assert.AreEqual(0, _daily.Claim());
        }

        [Test]
        public void DailyAutoShowsOncePerDay()
        {
            Assert.IsTrue(_daily.ShouldAutoShow);
            _daily.MarkAutoShown();
            Build();
            Assert.IsFalse(_daily.ShouldAutoShow);
            _now = _now.AddDays(1);
            Assert.IsTrue(_daily.ShouldAutoShow);
        }

        // Unlocked, intro seen and joined: stage one is racing.
        private void JoinRace()
        {
            _data.Model.Level = OrbitRushService.ActivationLevel;
            _rush.MarkIntroShown();
            Assert.IsTrue(_rush.Join());
        }

        // Enough wins in a row to reach the stage target.
        private void WinStage()
        {
            while (_rush.State == OrbitRushState.Racing) _rush.RecordWin();
        }

        [Test]
        public void RushWindowsAreThreeDaysAndRestart()
        {
            _now = OrbitRushService.Anchor.AddDays(OrbitRushService.WindowDays * 10 + 1);
            Assert.AreEqual(10, _rush.Window);
            Assert.AreEqual(TimeSpan.FromDays(2), _rush.TimeLeft);
            _now = _now.AddDays(2);
            Assert.AreEqual(11, _rush.Window);
        }

        [Test]
        public void RushOpensFromTheActivationLevelWithAnIntroAndAJoin()
        {
            _data.Model.Level = OrbitRushService.ActivationLevel - 1;
            Assert.AreEqual(OrbitRushState.Locked, _rush.State);
            Assert.IsFalse(_rush.IsVisible);
            _data.Model.Level = OrbitRushService.ActivationLevel;
            Assert.AreEqual(OrbitRushState.Intro, _rush.State);
            _rush.RecordWin();
            Assert.AreEqual(0, _rush.Score);
            _rush.MarkIntroShown();
            Assert.AreEqual(OrbitRushState.Join, _rush.State);
            Assert.IsTrue(_rush.Join());
            Assert.AreEqual(OrbitRushState.Racing, _rush.State);
            Assert.AreEqual(0, _rush.Stage);
            Assert.AreEqual(OrbitRushService.Targets[0], _rush.Target);
        }

        [Test]
        public void WinsMoveByTheStreakGearAndALossClearsIt()
        {
            JoinRace();
            _rush.RecordWin();
            _rush.RecordWin();
            Assert.AreEqual(1 + 2, _rush.Score);
            Assert.AreEqual(3, _rush.Multiplier);
            _rush.RecordLoss();
            Assert.AreEqual(1, _rush.Multiplier);
            Build();
            _rush.RecordWin();
            Assert.AreEqual(4, _rush.Score);
        }

        [Test]
        public void ReachingTheTargetFirstWinsTheStageAndTheClaimPaysGiftAndPlace()
        {
            JoinRace();
            WinStage();
            Assert.AreEqual(OrbitRushState.StageWon, _rush.State);
            Assert.AreEqual(1, _rush.WonRank);
            Assert.AreEqual(1, _rush.PlayerRank);
            Assert.IsTrue(_rush.HasClaimable);
            var coins = _wallet.Coins;
            var paid = OrbitRushService.StageRewards[0] + OrbitRushService.RankRewards[0];
            Assert.AreEqual(paid, _rush.ClaimStage());
            Assert.AreEqual(coins + paid, _wallet.Coins);
            Assert.AreEqual(OrbitRushState.Racing, _rush.State);
            Assert.AreEqual(1, _rush.Stage);
            Assert.AreEqual(0, _rush.Score);
            Assert.AreEqual(0, _rush.ClaimStage());
        }

        [Test]
        public void StageTimeOutLosesTheRoomAndRetryOpensANewOne()
        {
            JoinRace();
            _rush.RecordWin();
            _now = _now.AddHours(OrbitRushService.StageHours + 0.1);
            Assert.AreEqual(OrbitRushState.StageLost, _rush.State);
            _rush.RecordWin();
            Assert.AreEqual(1, _rush.Score);
            Assert.IsTrue(_rush.Retry());
            Assert.AreEqual(OrbitRushState.Racing, _rush.State);
            Assert.AreEqual(0, _rush.Score);
            Assert.AreEqual(0, _rush.Stage);
        }

        [Test]
        public void ThePodiumFillingUpLosesTheStage()
        {
            JoinRace();
            // Step through the stage; once three bots are home the room is lost.
            for (var step = 0; step < OrbitRushService.StageHours * 2; step++)
            {
                _now = _now.AddMinutes(30);
                var finished = 0;
                foreach (var racer in _rush.Racers())
                    if (!racer.IsPlayer && racer.Score >= _rush.Target) finished++;
                if (finished >= OrbitRushService.Winners) Assert.AreEqual(OrbitRushState.StageLost, _rush.State);
                else if (step < OrbitRushService.StageHours * 2 - 1) Assert.AreEqual(OrbitRushState.Racing, _rush.State);
                if (_rush.State == OrbitRushState.StageLost) break;
            }
        }

        [Test]
        public void RoomHasFiveRankedRacersThatStayTheSameAfterAReload()
        {
            JoinRace();
            _now = _now.AddHours(3);
            _rush.RecordWin();
            var before = _rush.Racers();
            Assert.AreEqual(OrbitRushService.Capacity, before.Count);
            var names = new System.Collections.Generic.HashSet<string>();
            var players = 0;
            foreach (var racer in before)
                if (racer.IsPlayer) players++;
                else Assert.IsTrue(names.Add(racer.Name));
            Assert.AreEqual(1, players);
            for (var i = 1; i < before.Count; i++) Assert.GreaterOrEqual(before[i - 1].Score, before[i].Score);
            Build();
            var after = _rush.Racers();
            for (var i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].Name, after[i].Name);
                Assert.AreEqual(before[i].Score, after[i].Score);
            }
        }

        [Test]
        public void AllStagesEndWithTheGrandPrize()
        {
            JoinRace();
            var coins = _wallet.Coins;
            var paid = 0;
            for (var stage = 0; stage < OrbitRushService.Stages; stage++)
            {
                WinStage();
                paid += _rush.ClaimStage();
            }
            Assert.AreEqual(OrbitRushState.Completed, _rush.State);
            Assert.AreEqual(coins + paid, _wallet.Coins);
            var expected = OrbitRushService.GrandPrize;
            for (var stage = 0; stage < OrbitRushService.Stages; stage++)
                expected += OrbitRushService.StageRewards[stage] + OrbitRushService.RankRewards[0];
            Assert.AreEqual(expected, paid);
            _rush.RecordWin();
            Assert.AreEqual(OrbitRushState.Completed, _rush.State);
        }

        [Test]
        public void WindowEndPaysAnUnclaimedStageAndReportsTheFinishOnce()
        {
            JoinRace();
            WinStage();
            var coins = _wallet.Coins;
            _now = _now.AddDays(OrbitRushService.WindowDays);
            Assert.AreEqual(OrbitRushState.Intro, _rush.State);
            var paid = OrbitRushService.StageRewards[0] + OrbitRushService.RankRewards[0];
            Assert.AreEqual(coins + paid, _wallet.Coins);
            Build();
            Assert.IsTrue(_rush.TryTakeFinish(out var stages, out var finishCoins));
            Assert.AreEqual(1, stages);
            Assert.AreEqual(paid, finishCoins);
            Assert.IsFalse(_rush.TryTakeFinish(out _, out _));
        }

        [Test]
        public void ClockMovedBackAcrossAWindowKeepsTheRunningEvent()
        {
            JoinRace();
            _rush.RecordWin();
            var coins = _wallet.Coins;
            var now = _now;
            _now = _now.AddDays(-OrbitRushService.WindowDays);
            Assert.AreEqual(OrbitRushState.Racing, _rush.State);
            Assert.AreEqual(1, _rush.Score);
            Assert.IsFalse(_rush.TryTakeFinish(out _, out _));
            _now = now;
            Build();
            Assert.AreEqual(OrbitRushState.Racing, _rush.State);
            Assert.AreEqual(1, _rush.Score);
            Assert.AreEqual(coins, _wallet.Coins);
        }

        [Test]
        public void HomeIconCountsUpWinsOnce()
        {
            JoinRace();
            _rush.RecordWin();
            _rush.RecordWin();
            Assert.AreEqual(3, _rush.PendingCollect);
            _rush.MarkCollected();
            Assert.AreEqual(0, _rush.PendingCollect);
        }

        [Test]
        public void MapLayoutMatchesTheStages()
        {
            var layout = AssetDatabase.LoadAssetAtPath<OrbitRushMapLayout>("Assets/Configs/OrbitRushMapLayout.asset");
            Assert.IsNotNull(layout);
            Assert.AreEqual(OrbitRushService.Stages + 2, layout.Waypoints.Length);
            Assert.AreEqual(OrbitRushService.Stages + 1, layout.Rewards.Length);
            foreach (var waypoint in layout.Waypoints) Assert.Less(waypoint, layout.Stops.Length);
        }

        [Test]
        public void FinishReportsEverythingTheWindowPaid()
        {
            JoinRace();
            WinStage();
            var claimed = _rush.ClaimStage();
            WinStage();
            _now = _now.AddDays(OrbitRushService.WindowDays);
            Assert.IsTrue(_rush.TryTakeFinish(out var stages, out var coins));
            Assert.AreEqual(2, stages);
            Assert.AreEqual(claimed + OrbitRushService.StageRewards[1] + OrbitRushService.RankRewards[0], coins);
        }
    }
}
