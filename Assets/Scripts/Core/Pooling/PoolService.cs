using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.Pooling
{
    // Read-only view for the Pool Monitor window.
    public readonly struct PoolStats
    {
        public readonly string Name;
        public readonly int InUse;
        public readonly int Available;
        public readonly int Max;
        public readonly int Grows;

        public PoolStats(string name, int inUse, int available, int max, int grows)
        {
            Name = name;
            InUse = inUse;
            Available = available;
            Max = max;
            Grows = grows;
        }
    }

    // One pool per prefab, created on prewarm or lazily on first spawn.
    public sealed class PoolService : IDisposable
    {
        private const int DefaultMaxCount = 16;

        private readonly Transform _root;
        private readonly Dictionary<GameObject, ComponentPool<Transform>> _byPrefab = new();

        public PoolService(Transform root)
        {
            _root = root;
        }

        public void Prewarm(GameObject prefab, int warmCount, int maxCount)
        {
            if (prefab == null) return;
            var pool = GetOrCreate(prefab, maxCount);
            pool.Warm(warmCount - pool.TotalCreated);
        }

        public T Spawn<T>(T prefab, Vector3 position, Transform parent = null) where T : Component
        {
            var instance = GetOrCreate(prefab.gameObject, DefaultMaxCount).Get();
            if (parent != null) instance.SetParent(parent, false);
            instance.position = position;
            return instance.GetComponent<T>();
        }

        public void Despawn(Component instance)
        {
            if (instance == null) return;
            var handle = instance.GetComponent<PoolHandle>();
            if (handle != null && handle.Owner is ComponentPool<Transform> pool && pool.Return(instance.transform))
                return;

            Debug.LogWarning($"[Pool] '{instance.name}' is not spawned from a pool; destroying it instead.");
            UnityEngine.Object.Destroy(instance.gameObject);
        }

        public void DespawnAll()
        {
            foreach (var pool in _byPrefab.Values) pool.ReturnAll();
        }

        public void CollectStats(List<PoolStats> into)
        {
            into.Clear();
            foreach (var pool in _byPrefab.Values)
                into.Add(new PoolStats(pool.Name, pool.InUseCount, pool.AvailableCount, pool.MaxCount, pool.GrowCount));
        }

        public void Dispose()
        {
            foreach (var pool in _byPrefab.Values) pool.DestroyAll();
            _byPrefab.Clear();
        }

        private ComponentPool<Transform> GetOrCreate(GameObject prefab, int maxCount)
        {
            if (_byPrefab.TryGetValue(prefab, out var pool)) return pool;

            var container = new GameObject($"Pool - {prefab.name}").transform;
            container.SetParent(_root, false);
            pool = new ComponentPool<Transform>(prefab.transform, container, 0, maxCount);
            _byPrefab.Add(prefab, pool);
            return pool;
        }
    }
}
