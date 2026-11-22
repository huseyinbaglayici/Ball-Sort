using Project.Contexts.Application;
using Project.Contexts.Application.Economy;

namespace Project.Contexts.Game
{
    // A win's cues: one by its difficulty, then one delivery sound for what it paid.
    public static class WinSounds
    {
        public static string For(LevelDifficulty difficulty) => difficulty switch
        {
            LevelDifficulty.Hard => "win_hard",
            LevelDifficulty.SuperHard => "win_super",
            _ => "win"
        };

        // Null when nothing new was paid.
        public static string Reward(LevelResult result)
        {
            if (!result.FirstWin) return null;
            var road = result.Road;
            var boosters = road.Undo + road.ExtraTube + road.FreeMove;
            var kinds = (road.Coins > 0 ? 1 : 0) + (boosters > 0 ? 1 : 0) + (road.UnlimitedLifeMinutes > 0 ? 1 : 0);
            if (kinds > 1) return "multi_grant";
            if (boosters > 1) return "tool_pair";
            if (boosters == 1) return "tool_grant";
            if (road.UnlimitedLifeMinutes > 0) return "unlimited";
            return road.Coins > 0 ? "road" : "coin";
        }
    }
}
