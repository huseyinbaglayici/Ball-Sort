using System;
using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    public sealed class ItemFactory
    {
        private readonly PoolService _pools;
        private readonly ItemRegistry _registry;

        public ItemFactory(PoolService pools, ItemRegistry registry)
        {
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Prewarm() => _registry.Prewarm(_pools);

        public bool Has(ItemType type) => _registry.Has(type);

        public T Get<T>(ItemType type, Transform parent) where T : Item
        {
            var item = _pools.Spawn(_registry.GetPrefab(type), Vector3.zero, parent);
            if (item is T typed) return typed;

            _pools.Despawn(item);
            throw new InvalidCastException($"Prefab for {type} is {item.GetType().Name}, expected {typeof(T).Name}.");
        }

        public void Release(Item item) => _pools.Despawn(item);
    }
}
