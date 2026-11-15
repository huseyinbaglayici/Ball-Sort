using System.Collections.Generic;
using System.Text.RegularExpressions;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>
    /// Groups the level assets by number into the <see cref="LevelCatalog"/> the game reads.
    /// Names follow the importer: Level_007 (one layout) or Level_007_v1, Level_007_v2 (several layouts).
    /// </summary>
    public static class LevelCatalogBuilder
    {
        public const string CatalogPath = "Assets/Configs/LevelCatalog.asset";
        private static readonly Regex NamePattern = new(@"^Level_(\d+)(?:_v(\d+))?$");

        [MenuItem("Tools/Project/Rebuild Level Catalog")]
        public static void RebuildFromMenu() => Rebuild();

        public static LevelCatalog Rebuild()
        {
            var groups = new SortedDictionary<int, SortedDictionary<int, LevelData>>();
            var plain = new Dictionary<int, LevelData>();
            foreach (var level in LevelAssetStore.FindLevels())
            {
                var match = NamePattern.Match(level.name);
                if (!match.Success) continue;

                var number = int.Parse(match.Groups[1].Value);
                if (!match.Groups[2].Success)
                {
                    plain[number] = level;
                    continue;
                }

                if (!groups.TryGetValue(number, out var variants))
                    groups[number] = variants = new SortedDictionary<int, LevelData>();
                variants[int.Parse(match.Groups[2].Value)] = level;
            }

            // A level that has numbered layouts (_v1, _v2, ...) ignores a plain Level_NNN asset with the same number.
            foreach (var pair in plain)
            {
                if (groups.ContainsKey(pair.Key)) continue;
                groups[pair.Key] = new SortedDictionary<int, LevelData> { [1] = pair.Value };
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Levels.Clear();
            var variantCount = 0;
            foreach (var pair in groups)
            {
                var entry = new LevelCatalog.Entry { Number = pair.Key };
                entry.Variants.AddRange(pair.Value.Values);
                variantCount += entry.Variants.Count;
                catalog.Levels.Add(entry);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelCatalog] {catalog.Levels.Count} levels, {variantCount} layouts.");
            return catalog;
        }
    }
}
