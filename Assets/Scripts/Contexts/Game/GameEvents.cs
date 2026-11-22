using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Core.Events;

namespace Project.Contexts.Game
{
    public enum GameState
    {
        Idle,
        Playing,
        Won,
        Lost
    }

    public readonly struct BottleTapped : IEvent
    {
        public readonly BottleItem Bottle;
        public BottleTapped(BottleItem bottle) => Bottle = bottle;
    }

    // A new try's board is built (not on a resume from the fail screen).
    public readonly struct LevelStarted : IEvent
    {
        public readonly int Level;
        public readonly LevelData Data;
        public readonly SortBoard Model;

        public LevelStarted(int level, LevelData data, SortBoard model)
        {
            Level = level;
            Data = data;
            Model = model;
        }
    }

    public readonly struct BottleSelected : IEvent
    {
        public readonly int Bottle;
        public BottleSelected(int bottle) => Bottle = bottle;
    }

    public readonly struct BottleDeselected : IEvent
    {
        public readonly int Bottle;
        public BottleDeselected(int bottle) => Bottle = bottle;
    }

    // A transfer the model accepted; its balls are still flying.
    public readonly struct MoveMade : IEvent
    {
        public readonly int From;
        public readonly int To;

        public MoveMade(int from, int to)
        {
            From = from;
            To = to;
        }
    }

    // The board is cleared and the level is no longer shown.
    public readonly struct LevelLeft : IEvent
    {
    }
}
