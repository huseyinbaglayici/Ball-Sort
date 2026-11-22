using System;

namespace Project.Contexts.Application.Economy
{
    public enum BoosterType
    {
        Undo,
        ExtraTube,
        FreeMove
    }

    public enum LevelDifficulty
    {
        Normal,
        Hard,
        SuperHard
    }

    public enum Feature
    {
        Challenge,
        StarterPack
    }

    // Interstitial test group: start level, then wins and active seconds required between two.
    public readonly struct AdGroup
    {
        public readonly string Name;
        public readonly int StartLevel;
        public readonly int Points;
        public readonly int ActiveSeconds;

        public AdGroup(string name, int startLevel, int points, int activeSeconds)
        {
            Name = name;
            StartLevel = startLevel;
            Points = points;
            ActiveSeconds = activeSeconds;
        }
    }

    // What the first win of a road level gives on top of its base coins.
    public readonly struct RoadReward
    {
        public readonly int Coins;
        public readonly int Undo;
        public readonly int ExtraTube;
        public readonly int FreeMove;
        public readonly int UnlimitedLifeMinutes;

        public RoadReward(int coins = 0, int undo = 0, int extraTube = 0, int freeMove = 0, int unlimitedLifeMinutes = 0)
        {
            Coins = coins;
            Undo = undo;
            ExtraTube = extraTube;
            FreeMove = freeMove;
            UnlimitedLifeMinutes = unlimitedLifeMinutes;
        }

        public bool IsEmpty => Coins == 0 && Undo == 0 && ExtraTube == 0 && FreeMove == 0 && UnlimitedLifeMinutes == 0;

        public int Boosters(BoosterType type) => type switch
        {
            BoosterType.Undo => Undo,
            BoosterType.ExtraTube => ExtraTube,
            _ => FreeMove
        };
    }

    // Stateless, so modules and tests share one table.
    public static class CampaignRules
    {
        public const int MaxLevel = 100;
        // New accounts only; existing saves keep their balance.
        public const int StartingCoins = 1000;
        public const int OpeningBoosterGrant = 2;
        // Coins only, once per try; adds a 4-slot temporary tube.
        public const int ContinuePrice = 900;
        public const int ContinueTubeCapacity = 4;
        public const int RewardedLifeGrant = 1;
        public const int MaxLives = 5;
        public const int LifeRefillMinutes = 30;
        public const int LevelsPerMetaPeriod = 20;
        public const int MaxMetaPeriod = 5;

        public static readonly BoosterType[] AllBoosters = { BoosterType.Undo, BoosterType.ExtraTube, BoosterType.FreeMove };

        public static readonly AdGroup[] AdGroups =
        {
            new("A", startLevel: 22, points: 3, activeSeconds: 120),
            new("B", startLevel: 32, points: 2, activeSeconds: 60)
        };

        // Levels that introduce a mechanic; no interstitial on them or right before them.
        private static readonly int[] IntroductionLevels = { 7, 20, 40, 60, 80 };

        // Levels 1 to this one run back to back without Home; the win of this level opens Home first.
        public const int ChainLevels = 10;

        public static bool IsCampaignLevel(int level) => level >= 1 && level <= MaxLevel;

        // Inside the opening chain: the app opens straight into the level.
        public static bool IsChainLevel(int level) => level >= 1 && level <= ChainLevels;

        // A win of this level goes straight into the next one.
        public static bool ChainsAfterWin(int level) => level >= 1 && level < ChainLevels;

        public static LevelDifficulty DifficultyOf(int level)
        {
            if (!IsCampaignLevel(level) || level < 14) return LevelDifficulty.Normal;
            if (level % 10 == 4) return LevelDifficulty.Hard;
            if (level % 10 == 9) return LevelDifficulty.SuperHard;
            return LevelDifficulty.Normal;
        }

        public static int FirstWinCoins(int level)
        {
            if (!IsCampaignLevel(level)) return 0;
            return DifficultyOf(level) switch
            {
                LevelDifficulty.Hard => 60,
                LevelDifficulty.SuperHard => 100,
                _ => 20
            };
        }

        public static RoadReward RoadRewardAt(int level) => level switch
        {
            10 => new RoadReward(coins: 100),
            25 => new RoadReward(undo: 1),
            27 => new RoadReward(unlimitedLifeMinutes: 30),
            30 => new RoadReward(coins: 200),
            35 => new RoadReward(freeMove: 1),
            45 => new RoadReward(extraTube: 1),
            50 => new RoadReward(coins: 150),
            52 => new RoadReward(unlimitedLifeMinutes: 30),
            55 => new RoadReward(coins: 250, undo: 1),
            65 => new RoadReward(freeMove: 1),
            70 => new RoadReward(unlimitedLifeMinutes: 30),
            75 => new RoadReward(coins: 150, extraTube: 1),
            85 => new RoadReward(undo: 1, freeMove: 1),
            90 => new RoadReward(unlimitedLifeMinutes: 30),
            95 => new RoadReward(undo: 1, extraTube: 1),
            100 => new RoadReward(coins: 150),
            _ => default
        };

        // First road-reward level at or after level; 0 if none.
        public static int NextRoadLevel(int level)
        {
            for (var l = Math.Max(1, level); l <= MaxLevel; l++)
                if (!RoadRewardAt(l).IsEmpty) return l;
            return 0;
        }

        // Last road-reward level before level; 0 if none.
        public static int PreviousRoadLevel(int level)
        {
            for (var l = Math.Min(level - 1, MaxLevel); l >= 1; l--)
                if (!RoadRewardAt(l).IsEmpty) return l;
            return 0;
        }

        public static int UnlockLevel(BoosterType type) => type switch
        {
            BoosterType.Undo => 1,
            BoosterType.ExtraTube => 7,
            _ => 15
        };

        public static int Price(BoosterType type) => type switch
        {
            BoosterType.Undo => 300,
            BoosterType.ExtraTube => 500,
            _ => 400
        };

        // First level whose win shows the feature; it stays available after that.
        public static int UnlockLevel(Feature feature) => feature switch
        {
            Feature.Challenge => 18,
            Feature.StarterPack => 21,
            _ => throw new ArgumentOutOfRangeException(nameof(feature))
        };

        // 1-5, from the number of campaign levels won in a row from level 1.
        public static int MetaPeriod(int levelsWon) =>
            Math.Min(MaxMetaPeriod, 1 + Math.Max(0, levelsWon) / LevelsPerMetaPeriod);

        public static bool IsCampaignFinal(int levelsWon) => levelsWon >= MaxLevel;

        // Never on peak, road, intro or final levels or with No Ads; frequency is checked separately.
        public static bool InterstitialEligible(int level, AdGroup group, bool noAds)
        {
            if (level < group.StartLevel || level >= MaxLevel) return false;
            if (noAds) return false;
            if (DifficultyOf(level) != LevelDifficulty.Normal || !RoadRewardAt(level).IsEmpty) return false;
            return Array.IndexOf(IntroductionLevels, level) < 0 && Array.IndexOf(IntroductionLevels, level + 1) < 0;
        }
    }
}
