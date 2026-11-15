using Project.Contexts.Game.Levels;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>Places bottles in centered rows, top row first.</summary>
    public static class LevelLayout
    {
        public const float BottleSpacing = 2.1f;
        public const float RowSpacing = 6f;

        public static void Arrange(LevelData level, int perRow)
        {
            var bottles = level.Bottles;
            perRow = Mathf.Max(1, perRow);
            var rows = Mathf.CeilToInt(bottles.Count / (float)perRow);
            for (var i = 0; i < bottles.Count; i++)
            {
                var row = i / perRow;
                var inRow = Mathf.Min(perRow, bottles.Count - row * perRow);
                var column = i % perRow;
                bottles[i].Position = new Vector2(
                    (column - (inRow - 1) * 0.5f) * BottleSpacing,
                    ((rows - 1) * 0.5f - row) * RowSpacing);
            }
        }
    }
}
