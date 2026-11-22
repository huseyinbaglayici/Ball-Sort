using System;
using Project.Core.Save;

namespace Project.Contexts.Application.Economy
{
    [Serializable]
    public sealed class LivesModel
    {
        public int Count = CampaignRules.MaxLives;
        // UTC ticks; 0 while lives are full.
        public long NextRefillTicks;
        // UTC ticks; 0 when never granted.
        public long UnlimitedUntilTicks;
    }

    // Proof of a paid continue, saved in the same write as its coins.
    [Serializable]
    public sealed class ContinueReceipt
    {
        public int TryId;
        public int Level;
        public int Coins;
        public int BoardVersion;
        public long UtcTicks;
    }

    // Day numbers are local calendar days (DateTime.Date ticks / ticks per day); 0 = never.
    [Serializable]
    public sealed class DailyRewardModel
    {
        // Days claimed in the current 7-day run.
        public int Streak;
        public int LastClaimDay;
        public int LastAutoShownDay;
    }

    [Serializable]
    public sealed class OrbitRushModel
    {
        // Index of the event window the progress belongs to.
        public int Window = -1;
        public int Wins;
        // Bit per milestone.
        public int Claimed;
    }

    // One file so a win's coins, road reward and next level are written together.
    [Serializable]
    public sealed class ProgressModel
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        // MaxLevel + 1 once the campaign is won.
        public int Level = 1;
        public int Coins = CampaignRules.StartingCoins;
        public int[] BoosterStock = new int[3];
        // Indexed by BoosterType.
        public bool[] OpeningGranted = new bool[3];
        public LivesModel Lives = new();
        public bool NoAds;
        public bool FirstInterstitialSeen;
        // Index into CampaignRules.AdGroups.
        public int AdGroup;
        // Eligible wins since the last interstitial.
        public int AdPoints;
        // Since the last interstitial.
        public float AdActiveSeconds;
        // Tries of the current level without a win; reset on a win.
        public int RetryCount;
        public int LastVariant;
        // Incremented per campaign try; ties a paid continue to its try.
        public int TryId;
        // A try with a receipt can never buy or apply another continue.
        public ContinueReceipt LastContinue = new();
        public DailyRewardModel Daily = new();
        public OrbitRushModel OrbitRush = new();
    }

    // Batch() defers writes so one logical change is one file write.
    public sealed class PlayerData
    {
        public const string Key = "progress";

        private readonly SaveService _save;
        private int _batchDepth;
        private bool _dirty;

        public ProgressModel Model { get; }

        public PlayerData(SaveService save)
        {
            _save = save;
            Model = save.Load<ProgressModel>(Key);
            if (Migrate(Model)) _save.Save(Key, Model);
        }

        public void Save()
        {
            if (_batchDepth > 0)
            {
                _dirty = true;
                return;
            }
            _save.Save(Key, Model);
        }

        // Saves once when the outermost batch ends.
        public BatchScope Batch()
        {
            _batchDepth++;
            return new BatchScope(this);
        }

        private void EndBatch()
        {
            if (--_batchDepth > 0 || !_dirty) return;
            _dirty = false;
            _save.Save(Key, Model);
        }

        // Upgrades an older or damaged model; returns whether anything changed.
        public static bool Migrate(ProgressModel model)
        {
            var changed = false;
            if (model.Version < 2)
            {
                // Version 1 saves keep level and coins, lives start full; opening boosters come on level entry.
                model.Lives = new LivesModel();
                model.BoosterStock = new int[3];
                model.OpeningGranted = new bool[3];
                model.Version = 2;
                changed = true;
            }

            if (model.BoosterStock == null || model.BoosterStock.Length != 3)
            {
                var stock = new int[3];
                if (model.BoosterStock != null) Array.Copy(model.BoosterStock, stock, Math.Min(3, model.BoosterStock.Length));
                model.BoosterStock = stock;
                changed = true;
            }

            if (model.OpeningGranted == null || model.OpeningGranted.Length != 3)
            {
                var granted = new bool[3];
                if (model.OpeningGranted != null) Array.Copy(model.OpeningGranted, granted, Math.Min(3, model.OpeningGranted.Length));
                model.OpeningGranted = granted;
                changed = true;
            }

            if (model.Lives == null)
            {
                model.Lives = new LivesModel();
                changed = true;
            }

            if (model.LastContinue == null)
            {
                model.LastContinue = new ContinueReceipt();
                changed = true;
            }

            if (model.Daily == null)
            {
                model.Daily = new DailyRewardModel();
                changed = true;
            }

            if (model.OrbitRush == null)
            {
                model.OrbitRush = new OrbitRushModel();
                changed = true;
            }

            var level = Math.Clamp(model.Level, 1, CampaignRules.MaxLevel + 1);
            var coins = Math.Max(0, model.Coins);
            var lives = Math.Clamp(model.Lives.Count, 0, CampaignRules.MaxLives);
            var retries = Math.Max(0, model.RetryCount);
            var lastVariant = Math.Max(0, model.LastVariant);
            if (level != model.Level || coins != model.Coins || lives != model.Lives.Count || retries != model.RetryCount ||
                lastVariant != model.LastVariant) changed = true;
            model.Level = level;
            model.Coins = coins;
            model.Lives.Count = lives;
            model.RetryCount = retries;
            model.LastVariant = lastVariant;
            if (model.AdGroup < 0 || model.AdGroup >= CampaignRules.AdGroups.Length || model.AdPoints < 0 ||
                model.AdActiveSeconds < 0f)
            {
                model.AdGroup = Math.Clamp(model.AdGroup, 0, CampaignRules.AdGroups.Length - 1);
                model.AdPoints = Math.Max(0, model.AdPoints);
                model.AdActiveSeconds = Math.Max(0f, model.AdActiveSeconds);
                changed = true;
            }
            for (var i = 0; i < 3; i++)
            {
                if (model.BoosterStock[i] >= 0) continue;
                model.BoosterStock[i] = 0;
                changed = true;
            }
            return changed;
        }

        public readonly struct BatchScope : IDisposable
        {
            private readonly PlayerData _data;
            public BatchScope(PlayerData data) => _data = data;
            public void Dispose() => _data?.EndBatch();
        }
    }
}
