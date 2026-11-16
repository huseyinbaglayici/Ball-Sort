using Project.Contexts.Game.Items;
using Project.Core.Events;

namespace Project.Contexts.Game
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    public readonly struct BottleTapped : IEvent
    {
        public readonly BottleItem Bottle;
        public BottleTapped(BottleItem bottle) => Bottle = bottle;
    }
}
