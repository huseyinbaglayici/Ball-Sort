using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game;
using Project.Contexts.Game.Sort;
using Project.Core.Save;

namespace Project.Tests
{
    public sealed class CampaignEconomyTests
    {
        private static readonly TimeSpan Refill = TimeSpan.FromMinutes(CampaignRules.LifeRefillMinutes);

        private string _folder;
        private DateTime _now;
        private SaveService _save;
        private PlayerData _data;
        private WalletService _wallet;
        private BoosterInventory _boosters;
        private LivesService _lives;
        private ProgressService _progress;
        private int _nextRandom;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ballsort-tests-" + Guid.NewGuid().ToString("N"));
            _now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // Reloads every module from disk, like an app restart.
        private void Build()
        {
            _save = new SaveService(_folder);
            _data = new PlayerData(_save);
            _wallet = new WalletService(_data);
            _boosters = new BoosterInventory(_data, _wallet);
            _lives = new LivesService(_data, () => _now);
            _progress = new ProgressService(_data, _wallet, _boosters, _lives, (min, max) => _nextRandom);
        }

        private void StartAt(int level)
        {
            _data.Model.Level = level;
            _data.Save();
            _boosters.GrantOpenings(level);
        }

        [Test]
        public void RoadLevelsAroundALevel()
        {
            Assert.AreEqual(10, CampaignRules.NextRoadLevel(1));
            Assert.AreEqual(10, CampaignRules.NextRoadLevel(10));
            Assert.AreEqual(25, CampaignRules.NextRoadLevel(11));
            Assert.AreEqual(0, CampaignRules.NextRoadLevel(CampaignRules.MaxLevel + 1));
            Assert.AreEqual(0, CampaignRules.PreviousRoadLevel(10));
            Assert.AreEqual(10, CampaignRules.PreviousRoadLevel(11));
            Assert.AreEqual(CampaignRules.MaxLevel, CampaignRules.PreviousRoadLevel(CampaignRules.MaxLevel + 1));
        }

        [Test]
        public void NewPlayerStartsWith1000CoinsAndTwoUndos()
        {
            Assert.AreEqual(1000, _wallet.Coins);
            Assert.AreEqual(1, _progress.Level);
            Assert.AreEqual(2, _boosters.Stock(BoosterType.Undo));
            Assert.AreEqual(0, _boosters.Stock(BoosterType.ExtraTube));
            Assert.AreEqual(0, _boosters.Stock(BoosterType.FreeMove));
            Assert.AreEqual(CampaignRules.MaxLives, _lives.Count);
        }

        [Test]
        public void FirstWinPaysByDifficulty()
        {
            Assert.AreEqual(LevelDifficulty.Hard, CampaignRules.DifficultyOf(14));
            Assert.AreEqual(LevelDifficulty.SuperHard, CampaignRules.DifficultyOf(19));
            Assert.AreEqual(LevelDifficulty.Normal, CampaignRules.DifficultyOf(4));
            Assert.AreEqual(LevelDifficulty.Normal, CampaignRules.DifficultyOf(9));

            var hard = 0;
            var superHard = 0;
            var total = 0;
            var road = 0;
            for (var level = 1; level <= 100; level++)
            {
                if (CampaignRules.DifficultyOf(level) == LevelDifficulty.Hard) hard++;
                if (CampaignRules.DifficultyOf(level) == LevelDifficulty.SuperHard) superHard++;
                total += CampaignRules.FirstWinCoins(level);
                road += CampaignRules.RoadRewardAt(level).Coins;
            }
            Assert.AreEqual(9, hard);
            Assert.AreEqual(9, superHard);
            Assert.AreEqual(3080, total);
            Assert.AreEqual(1000, road);
        }

        [Test]
        public void WinningTheWholeCampaignPaysTheGuaranteedTotal()
        {
            for (var level = 1; level <= 100; level++)
            {
                var result = _progress.CompleteLevel();
                Assert.AreEqual(level, result.Level);
                Assert.IsTrue(result.FirstWin);
            }

            Assert.AreEqual(5080, _wallet.Coins);
            Assert.IsTrue(_progress.IsCampaignFinished);
            Assert.AreEqual(101, _progress.Level);
            Assert.IsFalse(_progress.CompleteLevel().FirstWin, "there is no level 101");
            Assert.AreEqual(5080, _wallet.Coins);

            // Each booster: 2 on unlock plus the road; 6 / 5 / 5 in all.
            Assert.AreEqual(6, _boosters.Stock(BoosterType.Undo));
            Assert.AreEqual(5, _boosters.Stock(BoosterType.FreeMove));
            Assert.AreEqual(5, _boosters.Stock(BoosterType.ExtraTube));
        }

        [TestCase(55)]
        [TestCase(65)]
        [TestCase(75)]
        [TestCase(85)]
        [TestCase(95)]
        public void RoadRewardAddsCoinsAndBoosters(int level)
        {
            StartAt(level);
            var coins = _wallet.Coins;
            var stock = Array.ConvertAll(CampaignRules.AllBoosters, _boosters.Stock);
            var road = CampaignRules.RoadRewardAt(level);

            var result = _progress.CompleteLevel();

            Assert.AreEqual(coins + CampaignRules.FirstWinCoins(level) + road.Coins, _wallet.Coins);
            Assert.AreEqual(result.BaseCoins + road.Coins, result.TotalCoins);
            foreach (var type in CampaignRules.AllBoosters)
                Assert.AreEqual(stock[(int)type] + road.Boosters(type), _boosters.Stock(type), type.ToString());

            // Saved with the next level, in the same write.
            Build();
            Assert.AreEqual(level + 1, _progress.Level);
            Assert.AreEqual(coins + result.TotalCoins, _wallet.Coins);
        }

        [TestCase(27)]
        [TestCase(52)]
        [TestCase(70)]
        [TestCase(90)]
        public void UnlimitedLifeRoadRewardStartsAtOnce(int level)
        {
            StartAt(level);
            var result = _progress.CompleteLevel();

            Assert.AreEqual(30, result.Road.UnlimitedLifeMinutes);
            Assert.IsTrue(_lives.IsUnlimited);
            Assert.AreEqual(Refill, _lives.UnlimitedLeft);
            Assert.IsFalse(CampaignRules.InterstitialEligible(level, CampaignRules.AdGroups[0], false));
        }

        [Test]
        public void SecondUnlimitedGrantExtendsTheFirst()
        {
            StartAt(70);
            _progress.CompleteLevel();
            _now += TimeSpan.FromSeconds(3);
            StartAt(90);
            _progress.CompleteLevel();

            Assert.AreEqual(Refill + Refill - TimeSpan.FromSeconds(3), _lives.UnlimitedLeft);

            Build(); // survives a restart
            Assert.IsTrue(_lives.IsUnlimited);
            Assert.AreEqual(Refill + Refill - TimeSpan.FromSeconds(3), _lives.UnlimitedLeft);
        }

        [Test]
        public void BoostersOpenOnTheirLevelWithTwoInStock()
        {
            StartAt(7);
            Assert.AreEqual(2, _boosters.Stock(BoosterType.ExtraTube));
            Assert.IsFalse(BoosterInventory.IsUnlocked(BoosterType.FreeMove, 7));

            StartAt(15);
            Assert.AreEqual(2, _boosters.Stock(BoosterType.FreeMove));

            // Only once, however often the level is entered.
            _boosters.GrantOpenings(15);
            Assert.AreEqual(2, _boosters.Stock(BoosterType.FreeMove));
        }

        [Test]
        public void BoosterUseTakesStockBeforeCoins()
        {
            _wallet.TrySpend(500);
            Assert.IsTrue(_boosters.TryUse(BoosterType.Undo, 1, out var first));
            Assert.IsTrue(first.FromStock);
            Assert.IsTrue(_boosters.TryUse(BoosterType.Undo, 1, out _));
            Assert.AreEqual(500, _wallet.Coins);

            Assert.IsTrue(_boosters.TryUse(BoosterType.Undo, 1, out var paid));
            Assert.IsFalse(paid.FromStock);
            Assert.AreEqual(300, paid.Coins);
            Assert.AreEqual(200, _wallet.Coins);

            Assert.IsFalse(_boosters.CanUse(BoosterType.Undo, 1));
            Assert.IsFalse(_boosters.TryUse(BoosterType.Undo, 1, out _));
            Assert.AreEqual(200, _wallet.Coins);
        }

        [Test]
        public void LockedBoosterCannotBeUsed()
        {
            _wallet.Add(10000);
            Assert.IsFalse(_boosters.TryUse(BoosterType.FreeMove, 14, out _));
            Assert.IsFalse(_boosters.CanUse(BoosterType.ExtraTube, 6));
        }

        [Test]
        public void FiveLivesThenWaitForARefill()
        {
            for (var i = 0; i < 5; i++) Assert.IsTrue(_lives.TrySpend());
            Assert.IsFalse(_lives.TrySpend());
            Assert.IsFalse(_lives.CanPlay);

            _now += Refill;
            Assert.AreEqual(1, _lives.Count);

            _now += TimeSpan.FromTicks(Refill.Ticks * 7);
            Assert.AreEqual(5, _lives.Count);
            Assert.AreEqual(TimeSpan.Zero, _lives.NextLifeIn);
        }

        [Test]
        public void RefillClockRunsWhileTheAppIsClosed()
        {
            _lives.TrySpend();
            _lives.TrySpend();
            _now += Refill + TimeSpan.FromMinutes(1);
            Build();
            Assert.AreEqual(4, _lives.Count);
            Assert.AreEqual(Refill - TimeSpan.FromMinutes(1), _lives.NextLifeIn);
        }

        [Test]
        public void UnlimitedLivesAreNotSpent()
        {
            for (var i = 0; i < 5; i++) _lives.TrySpend();
            _lives.GrantUnlimited(30);
            Assert.IsTrue(_lives.TrySpend());
            Assert.AreEqual(0, _lives.Count);

            _now += Refill; // unlimited is over, and the first life is back
            Assert.IsFalse(_lives.IsUnlimited);
            Assert.AreEqual(1, _lives.Count);
        }

        [Test]
        public void InterstitialSchedule()
        {
            var a = CampaignRules.AdGroups[0];
            var b = CampaignRules.AdGroups[1];
            Assert.AreEqual(22, a.StartLevel);
            Assert.AreEqual(32, b.StartLevel);
            Assert.IsTrue(CampaignRules.InterstitialEligible(22, a, false));
            Assert.IsFalse(CampaignRules.InterstitialEligible(21, a, false));
            Assert.IsFalse(CampaignRules.InterstitialEligible(22, b, false));
            Assert.IsTrue(CampaignRules.InterstitialEligible(32, b, false));
            foreach (var level in new[] { 24, 25, 29, 35, 39, 40, 50, 59, 60, 75, 79, 80, 99, 100 })
                Assert.IsFalse(CampaignRules.InterstitialEligible(level, a, false), level.ToString());
            Assert.IsFalse(CampaignRules.InterstitialEligible(26, a, true), "No Ads");
        }

        [Test]
        public void RewardedLifeOnlyWhenOutOfLives()
        {
            var ads = new MonetizationService(_data, _lives);
            Assert.IsFalse(ads.CanOfferRewardedLife);
            for (var i = 0; i < 5; i++) _lives.TrySpend();
            Assert.IsTrue(ads.CanOfferRewardedLife);

            var next = _lives.NextLifeIn;
            ads.ShowRewardedLife(1, earned => Assert.IsTrue(earned));
            Assert.AreEqual(1, _lives.Count);
            Assert.AreEqual(next, _lives.NextLifeIn, "the natural refill keeps running");
            Assert.IsFalse(ads.CanOfferRewardedLife);

            _lives.TrySpend();
            _lives.GrantUnlimited(30);
            Assert.IsFalse(ads.CanOfferRewardedLife, "not during unlimited lives");
        }

        [Test]
        public void FeaturesOpenWithProgress()
        {
            StartAt(18);
            Assert.IsFalse(_progress.IsUnlocked(Feature.Challenge));
            _progress.CompleteLevel();
            Assert.IsTrue(_progress.IsUnlocked(Feature.Challenge));
            Assert.IsFalse(_progress.IsUnlocked(Feature.StarterPack));
        }

        [Test]
        public void MetaPeriodFollowsLevelsWon()
        {
            for (var won = 0; won <= 100; won++)
            {
                Assert.AreEqual(Math.Min(5, 1 + won / 20), CampaignRules.MetaPeriod(won), won.ToString());
                Assert.AreEqual(won == 100, CampaignRules.IsCampaignFinal(won), won.ToString());
            }
        }

        [Test]
        public void MetaChangesOnlyOnTheBoundaryWins()
        {
            for (var level = 1; level <= 100; level++)
            {
                var result = _progress.CompleteLevel();
                var boundary = level % 20 == 0;
                Assert.AreEqual(boundary, result.MetaChanged, level.ToString());
                Assert.AreEqual(level == 100, result.CampaignFinal, level.ToString());
                if (boundary && level < 100) Assert.AreEqual(level / 20 + 1, result.MetaPeriod);
            }
        }

        [Test]
        public void EachRetryPlaysTheNextVariant()
        {
            for (var retry = 0; retry < 5; retry++)
            {
                Assert.AreEqual(retry, _progress.PickVariant(5), retry.ToString());
                _progress.FailAttempt();
            }

            Build(); // the count survives a restart
            Assert.AreEqual(5, _progress.Attempt);
        }

        [Test]
        public void AfterEveryVariantARandomOneButNotTheLast()
        {
            for (var retry = 0; retry < 5; retry++)
            {
                _progress.PickVariant(5);
                _progress.FailAttempt();
            }

            // Last played is 4; the pool is 0-3 and the pick is random % 4.
            _nextRandom = 7;
            Assert.AreEqual(3, _progress.PickVariant(5));
            _progress.FailAttempt();
            _nextRandom = 99; // pool without 3: 0, 1, 2, 4 -> index 3
            Assert.AreEqual(4, _progress.PickVariant(5));
            _progress.FailAttempt();

            for (var i = 0; i < 100; i++)
            {
                _nextRandom = i;
                var last = _data.Model.LastVariant;
                Assert.AreNotEqual(last, _progress.PickVariant(5));
            }
        }

        [Test]
        public void WinStartsTheNextLevelAtTheFirstVariant()
        {
            _progress.PickVariant(5);
            _progress.FailAttempt();
            _progress.PickVariant(5);
            _progress.FailAttempt();
            _progress.CompleteLevel();

            Assert.AreEqual(0, _progress.Attempt);
            Assert.AreEqual(0, _progress.PickVariant(5));
        }

        [Test]
        public void SingleVariantLevelAlwaysPlaysIt()
        {
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, _progress.PickVariant(1));
                _progress.FailAttempt();
            }
        }

        private static SortBoard Stuck() => BoardBuilder.Of("4|0 0 1 1", "4|1 1 0 0").Build();

        private ContinueService Continue() => new(_data, _wallet, () => _now);

        [Test]
        public void ContinuePaysOnceAndAppliesTheVerifiedRescue()
        {
            var board = Stuck();
            var offer = RescueAdvisor.Analyze(board).Offer;
            Assume.That(offer, Is.Not.Null, "the advisor must verify the stuck board");
            var service = Continue();

            Assert.AreEqual(ContinueResult.Applied, service.TryBuy(board, offer, 30, 7, out var tube));
            Assert.AreEqual(100, _wallet.Coins);
            Assert.IsTrue(board.IsTemporary(tube));
            Assert.AreEqual(4, board.Capacity(tube));
            Assert.IsTrue(service.UsedInTry(7));

            // A double tap pays nothing more.
            Assert.AreEqual(ContinueResult.AlreadyUsed, service.TryBuy(board, offer, 30, 7, out _));
            Assert.AreEqual(100, _wallet.Coins);

            // The receipt survives a restart.
            Build();
            Assert.IsTrue(Continue().UsedInTry(7));
            Assert.AreEqual(100, _wallet.Coins);
            Assert.AreEqual(900, _data.Model.LastContinue.Coins);
        }

        [Test]
        public void ContinueNeeds900Coins()
        {
            var board = Stuck();
            var offer = RescueAdvisor.Analyze(board).Offer;
            Assume.That(offer, Is.Not.Null);
            _wallet.TrySpend(101); // 899 left

            Assert.AreEqual(ContinueResult.NotEnoughCoins, Continue().TryBuy(board, offer, 30, 1, out _));
            Assert.AreEqual(899, _wallet.Coins);
            Assert.IsFalse(board.ContinueUsed);
        }

        [Test]
        public void StaleOfferChargesNothing()
        {
            var board = Stuck();
            var offer = RescueAdvisor.Analyze(board).Offer;
            Assume.That(offer, Is.Not.Null);
            offer.BoardVersion--; // the board moved on since the offer was made

            Assert.AreEqual(ContinueResult.Stale, Continue().TryBuy(board, offer, 30, 1, out _));
            Assert.AreEqual(1000, _wallet.Coins);
            Assert.IsFalse(board.ContinueUsed);
        }

        [Test]
        public void VersionOneSaveKeepsLevelAndCoins()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, PlayerData.Key + ".json"), "{\"Version\":1,\"Level\":12,\"Coins\":75}");
            Build();

            Assert.AreEqual(ProgressModel.CurrentVersion, _data.Model.Version);
            Assert.AreEqual(12, _progress.Level);
            Assert.AreEqual(75, _wallet.Coins, "no second starting grant");
            Assert.AreEqual(2, _boosters.Stock(BoosterType.Undo));
            Assert.AreEqual(2, _boosters.Stock(BoosterType.ExtraTube));
            Assert.AreEqual(0, _boosters.Stock(BoosterType.FreeMove));
            Assert.AreEqual(5, _lives.Count);

            Build(); // migrated once, saved
            Assert.AreEqual(2, _boosters.Stock(BoosterType.Undo));
            Assert.AreEqual(75, _wallet.Coins);
        }
    }
}
