using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Application.Economy;
using Project.Core.Save;

namespace Project.Tests
{
    // Life refill, demo purchases and the interstitial point rules.
    public sealed class StoreTests
    {
        private string _folder;
        private DateTime _now;
        private PlayerData _data;
        private WalletService _wallet;
        private BoosterInventory _boosters;
        private LivesService _lives;
        private MonetizationService _ads;
        private bool _beginnerUnlocked;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ballsort-store-" + Guid.NewGuid().ToString("N"));
            _now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private void Build()
        {
            _data = new PlayerData(new SaveService(_folder));
            _wallet = new WalletService(_data);
            _boosters = new BoosterInventory(_data, _wallet);
            _lives = new LivesService(_data, () => _now);
            _ads = new MonetizationService(_data, _lives);
        }

        private StoreService Store() => new(_data, _wallet, _boosters, () => _beginnerUnlocked, () => _now);

        private void EmptyLives(int coins)
        {
            for (var i = 0; i < CampaignRules.MaxLives; i++) _lives.TrySpend();
            _wallet.TrySpend(_wallet.Coins);
            _wallet.Add(coins);
        }

        [Test]
        public void RefillShortOfCoinsSpendsNothing()
        {
            EmptyLives(CampaignRules.LifeRefillPrice - 1);
            Assert.AreEqual(RefillResult.NoCoins, _lives.BuyRefill(_wallet));
            Assert.AreEqual(CampaignRules.LifeRefillPrice - 1, _wallet.Coins);
            Assert.AreEqual(0, _lives.Count);
        }

        [Test]
        public void RefillTwiceChargesOnceAndSurvivesARestart()
        {
            EmptyLives(CampaignRules.LifeRefillPrice);
            Assert.AreEqual(RefillResult.Refilled, _lives.BuyRefill(_wallet));
            Assert.AreEqual(RefillResult.NotNeeded, _lives.BuyRefill(_wallet));
            Build();
            Assert.AreEqual(0, _wallet.Coins);
            Assert.AreEqual(CampaignRules.MaxLives, _lives.Count);
        }

        [Test]
        public void ALifeThatArrivedMakesTheRefillFree()
        {
            EmptyLives(CampaignRules.LifeRefillPrice);
            _now += TimeSpan.FromMinutes(CampaignRules.LifeRefillMinutes + 1);
            Assert.AreEqual(RefillResult.CanPlay, _lives.BuyRefill(_wallet));
            Assert.AreEqual(CampaignRules.LifeRefillPrice, _wallet.Coins);
        }

        [Test]
        public void LossPointsAreCappedPerLevel()
        {
            _ads.CountLoss(30);
            _ads.CountLoss(30);
            _ads.CountLoss(30);
            Assert.AreEqual(CampaignRules.LossAdPointCap, _data.Model.AdPoints, 1e-4f);
            _ads.CountLoss(31);
            Assert.AreEqual(CampaignRules.LossAdPointCap + CampaignRules.LossAdPoint, _data.Model.AdPoints, 1e-4f);
        }

        [Test]
        public void AnInterstitialThatFailsToLoadKeepsTheCounter()
        {
            _data.Model.FirstInterstitialSeen = true;
            _data.Model.AdPoints = 5f;
            _data.Model.AdActiveSeconds = 500f;
            _ads.SimulateInterstitialFailure = true;
            _ads.ShowLevelEndInterstitial(26, true, null);
            Assert.AreEqual(6f, _data.Model.AdPoints, 1e-4f);

            _ads.SimulateInterstitialFailure = false;
            _ads.ShowLevelEndInterstitial(26, true, null);
            Assert.AreEqual(0f, _data.Model.AdPoints, 1e-4f);
            Assert.AreEqual(0f, _data.Model.AdActiveSeconds, 1e-4f);
        }

        [Test]
        public void NoAdsOfferOpensOnlyAfterAShownInterstitial()
        {
            var store = Store();
            Assert.IsFalse(store.NoAdsOffered);
            _ads.SimulateInterstitialFailure = true;
            _ads.ShowInterstitial("level_end", 26, null);
            Assert.IsFalse(store.NoAdsOffered);
            _ads.SimulateInterstitialFailure = false;
            _ads.ShowInterstitial("level_end", 26, null);
            Assert.IsTrue(store.NoAdsOffered);
            store.Buy(StoreCatalog.NoAds, _ => { });
            Assert.IsFalse(store.NoAdsOffered);
            Assert.IsTrue(_ads.NoAds);
        }

        [Test]
        public void BeginnerOfferLastsFortyEightHoursAndOnlyBeforeABuy()
        {
            var store = Store();
            Assert.IsFalse(store.BeginnerAvailable, "locked before its win");
            _beginnerUnlocked = true;
            store.MarkBeginnerShown();
            _now += TimeSpan.FromHours(StoreCatalog.BeginnerHours - 1);
            Assert.IsTrue(store.BeginnerAvailable);
            _now += TimeSpan.FromHours(2);
            Assert.IsFalse(store.BeginnerAvailable);

            _data.Model.BeginnerShownTicks = 0;
            store.Buy(StoreCatalog.CoinPacks[0], _ => { });
            Assert.IsFalse(store.BeginnerAvailable, "gone after any purchase");
        }

        [Test]
        public void ATransactionDeliversOnce()
        {
            var store = Store();
            var coins = _wallet.Coins;
            Assert.IsTrue(store.Deliver("tx", StoreCatalog.Bundles[1]));
            Assert.IsFalse(store.Deliver("tx", StoreCatalog.Bundles[1]));
            Assert.AreEqual(coins + StoreCatalog.Bundles[1].Coins, _wallet.Coins);
            Assert.AreEqual(StoreCatalog.Bundles[1].ExtraTube, _boosters.Stock(BoosterType.ExtraTube));
        }
    }
}
