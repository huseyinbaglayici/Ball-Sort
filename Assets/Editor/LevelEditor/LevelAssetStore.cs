using System.Collections.Generic;
using System.IO;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>Finds, creates and saves level and palette assets on disk.</summary>
    public static class LevelAssetStore
    {
        public const string LevelsFolder = "Assets/Configs/Levels";
        private const string PalettePath = "Assets/Configs/LevelPalette.asset";

        public static List<LevelData> FindLevels()
        {
            var levels = new List<LevelData>();
            if (!AssetDatabase.IsValidFolder(LevelsFolder)) return levels;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelData), new[] { LevelsFolder }))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level != null) levels.Add(level);
            }
            levels.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return levels;
        }

        /// <summary>Saves under <paramref name="name"/>. An existing asset is overwritten in place so references keep working.</summary>
        public static LevelData Save(LevelData level, string name)
        {
            EnsureFolder();
            var path = $"{LevelsFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(level, path);
                return level;
            }

            EditorUtility.CopySerialized(level, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(level);
            return existing;
        }

        public static LevelPalette FindOrCreatePalette()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LevelPalette));
            if (guids.Length > 0) return AssetDatabase.LoadAssetAtPath<LevelPalette>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var palette = ScriptableObject.CreateInstance<LevelPalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
            return palette;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder)) AssetDatabase.CreateFolder("Assets/Configs", "Levels");
        }
    }
}
