using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    // Each level has layout variants picked by retry count; rebuilt by Tools > Project > Rebuild Level Catalog.
    [CreateAssetMenu(menuName = "Project/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public int Number;
            public List<LevelData> Variants = new();
        }

        [SerializeField] private List<Entry> _levels = new();

        public List<Entry> Levels => _levels;
        public int Count => _levels.Count;

        // Level is 1-based; both the level and the variant wrap past the end.
        public LevelData Get(int level, int variant)
        {
            var entry = EntryFor(level);
            if (entry == null || entry.Variants.Count == 0) return null;
            return entry.Variants[Mathf.Abs(variant) % entry.Variants.Count];
        }

        public int VariantCount(int level) => EntryFor(level)?.Variants.Count ?? 0;

        private Entry EntryFor(int level) => _levels.Count == 0 ? null : _levels[Mathf.Max(0, level - 1) % _levels.Count];
    }
}
