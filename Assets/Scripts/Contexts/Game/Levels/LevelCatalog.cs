using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    /// <summary>
    /// Ordered list of the game's levels. Every level has one or more layout variants; the variant is chosen
    /// by how many times the player has retried the level, cycling through the list.
    /// Rebuilt from the level assets by Tools &gt; Project &gt; Rebuild Level Catalog.
    /// </summary>
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

        /// <param name="level">1-based level number. Past the last level the list starts over.</param>
        /// <param name="attempt">0 for the first try, +1 for every retry.</param>
        public LevelData Get(int level, int attempt)
        {
            if (_levels.Count == 0) return null;

            var entry = _levels[Mathf.Max(0, level - 1) % _levels.Count];
            if (entry.Variants.Count == 0) return null;
            return entry.Variants[Mathf.Abs(attempt) % entry.Variants.Count];
        }
    }
}
