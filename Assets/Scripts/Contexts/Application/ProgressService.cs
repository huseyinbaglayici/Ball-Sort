using System;
using System.Collections.Generic;
using Project.Contexts.Application.Economy;
using Project.Core.Events;

namespace Project.Contexts.Application
{
    // Empty (FirstWin false) for a non-campaign win.
    public readonly struct LevelResult
    {
        public readonly int Level;
        public readonly bool FirstWin;
        public readonly LevelDifficulty Difficulty;
        public readonly int BaseCoins;
        public readonly RoadReward Road;
        public readonly int MetaPeriod;
        public readonly bool MetaChanged;
        public readonly bool CampaignFinal;

        public LevelResult(int level, bool firstWin, int baseCoins, RoadReward road, int metaPeriod, bool metaChanged,
            bool campaignFinal)
        {
            Level = level;
            FirstWin = firstWin;
            Difficulty = CampaignRules.DifficultyOf(level);
            BaseCoins = baseCoins;
            Road = road;
            MetaPeriod = metaPeriod;
            MetaChanged = metaChanged;
            CampaignFinal = campaignFinal;
        }

        public int TotalCoins => BaseCoins + Road.Coins;
    }

    // The campaign is linear, so levels won are 1 to Level - 1; replays and undo never change it.
    public sealed class ProgressService
    {
        private readonly PlayerData _data;
        private readonly WalletService _wallet;
        private readonly BoosterInventory _boosters;
        private readonly LivesService _lives;
        private readonly Func<int, int, int> _random;

        // random: int in [min, max); Unity's Random.Range by default.
        public ProgressService(PlayerData data, WalletService wallet, BoosterInventory boosters, LivesService lives,
            Func<int, int, int> random = null)
        {
            if (random == null) random = (min, max) => UnityEngine.Random.Range(min, max);
            _random = random;
            _data = data;
            _wallet = wallet;
            _boosters = boosters;
            _lives = lives;
            _boosters.GrantOpenings(Level);
        }

        public int Level => _data.Model.Level;

        public int LevelsWon => Level - 1;
        public int MetaPeriod => CampaignRules.MetaPeriod(LevelsWon);
        public bool IsCampaignFinished => CampaignRules.IsCampaignFinal(LevelsWon);

        public bool IsUnlocked(Feature feature) => LevelsWon >= CampaignRules.UnlockLevel(feature);

        // 0 on the first try, +1 per try that ended without a win.
        public int Attempt => _data.Model.RetryCount;

        public void FailAttempt()
        {
            _data.Model.RetryCount++;
            _data.Save();
            EventBus<LevelFailed>.Raise(default);
        }

        // A new try starts with every PickVariant.
        public int TryId => _data.Model.TryId;

        // Try n uses variant n; once all were played, a random one other than the last.
        public int PickVariant(int variantCount)
        {
            var model = _data.Model;
            model.TryId++;
            if (variantCount <= 0)
            {
                _data.Save();
                return 0;
            }

            int pick;
            if (model.RetryCount < variantCount)
            {
                pick = model.RetryCount;
            }
            else
            {
                var pool = new List<int>(variantCount);
                for (var i = 0; i < variantCount; i++) pool.Add(i);
                pool.Remove(model.LastVariant);
                pick = pool.Count == 0 ? 0 : pool[_random(0, 100) % pool.Count];
            }

            model.LastVariant = pick;
            _data.Save();
            return pick;
        }

        // Coins, road reward, next level and opening boosters go in one save before LevelCompleted.
        public LevelResult CompleteLevel()
        {
            var level = Level;
            if (!CampaignRules.IsCampaignLevel(level)) return default;

            var periodBefore = MetaPeriod;
            var road = CampaignRules.RoadRewardAt(level);
            using (_data.Batch())
            {
                _data.Model.Level = level + 1;
                _data.Model.RetryCount = 0;
                _data.Model.LastVariant = 0;
                _data.Save();
                _wallet.Add(CampaignRules.FirstWinCoins(level) + road.Coins);
                foreach (var type in CampaignRules.AllBoosters) _boosters.Add(type, road.Boosters(type));
                _lives.GrantUnlimited(road.UnlimitedLifeMinutes);
                _boosters.GrantOpenings(Level);
            }

            var final = IsCampaignFinished;
            var result = new LevelResult(level, true, CampaignRules.FirstWinCoins(level), road, MetaPeriod,
                MetaPeriod != periodBefore || final, final);

            EventBus<LevelCompleted>.Raise(new LevelCompleted(result));
            return result;
        }

        // Debug: back to a new player's save.
        public void ResetProgress()
        {
            var model = _data.Model;
            using (_data.Batch())
            {
                model.Level = 1;
                model.RetryCount = 0;
                model.LastVariant = 0;
                model.BoosterStock = new int[CampaignRules.BoosterCount];
                model.OpeningGranted = new bool[CampaignRules.BoosterCount];
                model.Lives = new LivesModel();
                model.NoAds = false;
                model.FirstInterstitialSeen = false;
                model.Daily = new DailyRewardModel();
                model.OrbitRush = new OrbitRushModel();
                _wallet.TrySpend(_wallet.Coins);
                _wallet.Add(CampaignRules.StartingCoins);
                _lives.Refresh();
                _boosters.GrantOpenings(Level);
            }
            foreach (var type in CampaignRules.AllBoosters)
                EventBus<BoosterStockChanged>.Raise(new BoosterStockChanged(type, _boosters.Stock(type)));
            EventBus<LivesChanged>.Raise(new LivesChanged(_lives.Count, _lives.IsUnlimited));
        }
    }
}
