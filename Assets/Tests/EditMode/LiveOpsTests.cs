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

        [Test]
        public void RushWindowsAreThreeDaysAndRestart()
        {
            _now = OrbitRushService.Anchor.AddDays(OrbitRushService.WindowDays * 10 + 1);
            Assert.AreEqual(10, _rush.Window);
            Assert.AreEqual(TimeSpan.FromDays(2), _rush.TimeLeft);

            _rush.RecordWin();
            _rush.RecordWin();
            Build();
            Assert.AreEqual(2, _rush.Progress);

            _now = _now.AddDays(2);
            Assert.AreEqual(11, _rush.Window);
            Assert.AreEqual(0, _rush.Progress);
        }

        [Test]
        public void RushMilestonesPayOnceWhenReached()
        {
            var coins = _wallet.Coins;
            Assert.IsFalse(_rush.CanClaim(0));
            Assert.AreEqual(0, _rush.Claim(0));
            for (var i = 0; i < OrbitRushService.Milestones[0].Step; i++) _rush.RecordWin();
            Assert.IsTrue(_rush.HasClaimable);
            Assert.AreEqual(OrbitRushService.Milestones[0].Coins, _rush.Claim(0));
            Assert.AreEqual(coins + OrbitRushService.Milestones[0].Coins, _wallet.Coins);
            Assert.IsTrue(_rush.IsClaimed(0));
            Assert.AreEqual(0, _rush.Claim(0));
            Assert.IsFalse(_rush.HasClaimable);
        }

        [Test]
        public void RushProgressStopsAtTheTreasure()
        {
            for (var i = 0; i < OrbitRushService.Steps + 5; i++) _rush.RecordWin();
            Assert.AreEqual(OrbitRushService.Steps, _rush.Progress);
            for (var i = 0; i < OrbitRushService.Milestones.Length; i++) Assert.IsTrue(_rush.CanClaim(i));
        }

        [Test]
        public void MapLayoutMatchesThePath()
        {
            var layout = AssetDatabase.LoadAssetAtPath<OrbitRushMapLayout>("Assets/Configs/OrbitRushMapLayout.asset");
            Assert.IsNotNull(layout);
            Assert.AreEqual(OrbitRushService.Steps + 1, layout.Stops.Length);
            Assert.AreEqual(OrbitRushService.Milestones.Length, layout.Rewards.Length);
        }
    }
}
