using System;
using System.Collections.Generic;
using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// Data table: item type -> prefab and pool size. Designers tune WarmCount/MaxCount here,
    /// code only asks for a type.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Item Registry", fileName = "ItemRegistry")]
    public sealed class ItemRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemType Type;
            public Item Prefab;
            [Min(0)] public int WarmCount;
            [Min(1)] public int MaxCount;
        }

        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<ItemType, Item> _prefabs;

        public IReadOnlyList<Entry> Entries => _entries;

        public Item GetPrefab(ItemType type)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<ItemType, Item>();
                foreach (var entry in _entries)
                    if (entry.Prefab != null) _prefabs[entry.Type] = entry.Prefab;
            }

            if (_prefabs.TryGetValue(type, out var prefab)) return prefab;
            throw new InvalidOperationException($"ItemRegistry '{name}' has no prefab for {type}.");
        }

        public void Prewarm(PoolService pools)
        {
            foreach (var entry in _entries)
                if (entry.Prefab != null) pools.Prewarm(entry.Prefab.gameObject, entry.WarmCount, entry.MaxCount);
        }

        private void OnValidate() => _prefabs = null;
    }
}
