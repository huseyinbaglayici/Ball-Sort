using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Base for everything the board spawns from the pool.
    public abstract class Item : MonoBehaviour, IPoolable
    {
        public virtual void OnSpawn() { }

        public virtual void OnDespawn() { }
    }
}
