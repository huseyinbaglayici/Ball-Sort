using System;
using System.Text;
using Project.Core.Localization;

namespace Project.Contexts.Application.Economy
{
    // Shared wording so every screen names coins, lives, boosters and road rewards the same way.
    public static class EconomyText
    {
        public static string Timer(TimeSpan time) =>
            time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";

        public static string Lives(LivesService lives, LocalizationService loc)
        {
            if (lives.IsUnlimited) return loc.Format("lives.unlimited", Timer(lives.UnlimitedLeft));
            var count = lives.Count;
            return count >= CampaignRules.MaxLives
                ? loc.Format("lives.full", count, CampaignRules.MaxLives)
                : loc.Format("lives.refill", count, CampaignRules.MaxLives, Timer(lives.NextLifeIn));
        }

        public static string Booster(BoosterType type, LocalizationService loc) => loc.Get(type switch
        {
            BoosterType.Undo => "booster.undo",
            BoosterType.ExtraTube => "booster.extra_tube",
            _ => "booster.free_move"
        });

        // Comma separated; empty when there is none.
        public static string Road(RoadReward road, LocalizationService loc)
        {
            var text = new StringBuilder();
            void Add(string part)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(part);
            }

            if (road.Coins > 0) Add(loc.Format("reward.coins", road.Coins));
            foreach (var type in CampaignRules.AllBoosters)
                if (road.Boosters(type) > 0) Add($"+{road.Boosters(type)} {Booster(type, loc)}");
            if (road.UnlimitedLifeMinutes > 0) Add(loc.Format("reward.unlimited", road.UnlimitedLifeMinutes));
            return text.ToString();
        }

        public static string Difficulty(LevelDifficulty difficulty, LocalizationService loc) => difficulty switch
        {
            LevelDifficulty.Hard => loc.Get("level.hard"),
            LevelDifficulty.SuperHard => loc.Get("level.super_hard"),
            _ => string.Empty
        };

        public static int DifficultyMarks(LevelDifficulty difficulty) => difficulty switch
        {
            LevelDifficulty.Hard => 1,
            LevelDifficulty.SuperHard => 2,
            _ => 0
        };

        public static string LivesCount(LivesService lives) => lives.IsUnlimited ? "∞" : lives.Count.ToString();

        // Lives pill's second line: unlimited time left, "Full", or time to the next life.
        public static string LivesTimer(LivesService lives, LocalizationService loc)
        {
            if (lives.IsUnlimited) return Timer(lives.UnlimitedLeft);
            return lives.Count >= CampaignRules.MaxLives ? loc.Get("lives.full_short") : Timer(lives.NextLifeIn);
        }
    }
}
