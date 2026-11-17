using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>
    /// Groups the level assets by number into the <see cref="LevelCatalog"/> the game reads (see <see cref="LevelGrouping"/>).
    /// Names follow the importer: Level_007 (one layout) or Level_007_v1, Level_007_v2 (several layouts).
    /// </summary>
    public static class LevelCatalogBuilder
    {
        public const string CatalogPath = "Assets/Configs/LevelCatalog.asset";

        [MenuItem("Tools/Project/Rebuild Level Catalog")]
        public static void RebuildFromMenu() => Rebuild();

        public static LevelCatalog Rebuild()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Levels.Clear();
            var variantCount = 0;
            foreach (var group in LevelGrouping.Build(LevelAssetStore.FindLevels()))
            {
                if (group.Number <= 0) continue;
                var entry = new LevelCatalog.Entry { Number = group.Number };
                foreach (var variant in group.Variants)
                    if (variant.InCatalog) entry.Variants.Add(variant.Level);
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
