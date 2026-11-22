using System;

namespace Project.Contexts.Game.Levels
{
    [Flags]
    public enum Mechanic
    {
        None = 0,
        HiddenBall = 1,
        FrozenTube = 2,
        ShutterKey = 4,
        Bomb = 8,
        Portal = 16,
        Tower = 32
    }

    public static class LevelMechanics
    {
        // Tubes this tall play as towers.
        public const int TowerCapacity = 8;

        // Every move warps through a portal, so it is in every level.
        public static Mechanic Of(LevelData level)
        {
            if (level == null) return Mechanic.None;
            var found = Mechanic.Portal;
            if (level.Curtains.Count > 0) found |= Mechanic.FrozenTube;
            if (level.Lockboxes.Count > 0) found |= Mechanic.ShutterKey;
            foreach (var bottle in level.Bottles)
            {
                if (bottle.Capacity >= TowerCapacity) found |= Mechanic.Tower;
                foreach (var slot in bottle.Slots)
                {
                    if (slot.IsEmpty) continue;
                    if (slot.Hidden || slot.Kind == SlotKind.Special) found |= Mechanic.HiddenBall;
                    if (slot.IsBomb) found |= Mechanic.Bomb;
                }
            }
            return found;
        }

        // Levels 1..count as first laid out.
        public static Mechanic Seen(LevelCatalog catalog, int count)
        {
            var seen = Mechanic.None;
            if (catalog == null) return seen;
            for (var level = 1; level <= Math.Min(count, catalog.Count); level++) seen |= Of(catalog.Get(level, 0));
            return seen;
        }
    }
}
