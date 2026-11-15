using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>Base of everything the board spawns from the pool: bottles, ball units and later blockers.</summary>
    public abstract class Item : MonoBehaviour, IPoolable
    {
        public abstract ItemType Type { get; }

        public virtual void OnSpawn() { }

        public virtual void OnDespawn() { }
    }
}
