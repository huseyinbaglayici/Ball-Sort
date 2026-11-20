using System;
using System.Collections.Generic;
using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Item type -> prefab and pool sizes.
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

        public Item GetPrefab(ItemType type)
        {
            if (Prefabs.TryGetValue(type, out var prefab)) return prefab;
            throw new InvalidOperationException($"ItemRegistry '{name}' has no prefab for {type}.");
        }

        public bool Has(ItemType type) => Prefabs.ContainsKey(type);

        private Dictionary<ItemType, Item> Prefabs
        {
            get
            {
                if (_prefabs != null) return _prefabs;
                _prefabs = new Dictionary<ItemType, Item>();
                foreach (var entry in _entries)
                    if (entry.Prefab != null) _prefabs[entry.Type] = entry.Prefab;
                return _prefabs;
            }
        }

        public void Prewarm(PoolService pools)
        {
            foreach (var entry in _entries)
                if (entry.Prefab != null) pools.Prewarm(entry.Prefab.gameObject, entry.WarmCount, entry.MaxCount);
        }

        private void OnValidate() => _prefabs = null;
    }
}
