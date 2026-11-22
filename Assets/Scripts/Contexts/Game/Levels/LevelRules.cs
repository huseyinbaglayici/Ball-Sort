using System;

namespace Project.Contexts.Game.Levels
{
    // Board shapes the game lays out: the tube sizes it has art for and the widest row.
    public static class LevelRules
    {
        public static readonly int[] TubeCapacities = { 4, 8, 12, 16 };
        public const int MaxPerRow = 6;

        public static bool IsTubeCapacity(int capacity) => Array.IndexOf(TubeCapacities, capacity) >= 0;
    }
}
