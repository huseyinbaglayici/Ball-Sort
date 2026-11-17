using System.Collections.Generic;
using System.Text.RegularExpressions;
using Project.Contexts.Game.Levels;

namespace Project.Editor.LevelEditor
{
    /// <summary>One layout a level can show (the game picks one per try).</summary>
    public readonly struct LevelVariant
    {
        public readonly string Label;
        public readonly LevelData Level;
        /// <summary>
        /// Whether the game's catalog uses it: a level with numbered layouts (_v1, _v2, ...) ignores a plain Level_NNN
        /// asset with the same number.
        /// </summary>
        public readonly bool InCatalog;

        public LevelVariant(string label, LevelData level, bool inCatalog)
        {
            Label = label;
            Level = level;
            InCatalog = inCatalog;
        }
    }

    /// <summary>One numbered level and its layouts. Assets whose name is not Level_NNN form a group of one.</summary>
    public sealed class LevelGroup
    {
        public int Number;
        public string Title;
        public readonly List<LevelVariant> Variants = new();
    }

    /// <summary>
    /// Groups level assets by the number in their name (Level_007_v1, Level_007_v2 ... = level 7), for the editor window
    /// and the level catalog. The assets and their names stay exactly as they are.
    /// </summary>
    public static class LevelGrouping
    {
        private static readonly Regex NamePattern = new(@"^Level_(\d+)(?:_v(\d+))?$");

        public static List<LevelGroup> Build(IEnumerable<LevelData> levels)
        {
            var numbered = new SortedDictionary<int, List<(int Index, LevelData Level)>>();
            var others = new List<LevelData>();
            foreach (var level in levels)
            {
                if (level == null) continue;
                var match = NamePattern.Match(level.name);
                if (!match.Success)
                {
                    others.Add(level);
                    continue;
                }

                var number = int.Parse(match.Groups[1].Value);
                var index = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                if (!numbered.TryGetValue(number, out var list)) numbered[number] = list = new List<(int, LevelData)>();
                list.Add((index, level));
            }

            var groups = new List<LevelGroup>();
            foreach (var pair in numbered)
            {
                pair.Value.Sort((a, b) => a.Index.CompareTo(b.Index));
                var hasNumbered = pair.Value.Exists(entry => entry.Index > 0);
                var group = new LevelGroup { Number = pair.Key, Title = $"Level {pair.Key}" };
                foreach (var (index, level) in pair.Value)
                {
                    var inCatalog = index > 0 || !hasNumbered;
                    var label = index > 0 ? $"Variant {index}" : inCatalog ? "Layout" : "Base (not used by the catalog)";
                    group.Variants.Add(new LevelVariant(label, level, inCatalog));
                }
                groups.Add(group);
            }

            foreach (var level in others)
            {
                var group = new LevelGroup { Number = 0, Title = level.name };
                group.Variants.Add(new LevelVariant("Layout", level, inCatalog: false));
                groups.Add(group);
            }
            return groups;
        }

        public static LevelGroup Find(List<LevelGroup> groups, LevelData level)
        {
            foreach (var group in groups)
                foreach (var variant in group.Variants)
                    if (variant.Level == level) return group;
            return null;
        }

        /// <summary>Name for a new level: one number after the highest existing level.</summary>
        public static string NextName(List<LevelGroup> groups)
        {
            var highest = 0;
            foreach (var group in groups)
                if (group.Number > highest) highest = group.Number;
            return $"Level_{highest + 1:000}";
        }
    }
}
