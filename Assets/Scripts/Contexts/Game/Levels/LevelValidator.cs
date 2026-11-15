using System.Collections.Generic;

namespace Project.Contexts.Game.Levels
{
    /// <summary>Checks the structural rules a playable level must follow.</summary>
    public static class LevelValidator
    {
        public static void Validate(LevelData level, List<string> errors)
        {
            errors.Clear();
            var bottles = level.Bottles;
            if (bottles.Count == 0)
            {
                errors.Add("Level has no bottles.");
                return;
            }

            var colorCounts = new Dictionary<int, int>();
            var hasFreeSpace = false;
            for (var b = 0; b < bottles.Count; b++)
            {
                var slots = bottles[b].Slots;
                if (slots.Count != level.Capacity)
                    errors.Add($"Bottle {b + 1} has {slots.Count} slots, expected {level.Capacity}.");

                for (var s = 0; s < slots.Count; s++)
                {
                    if (slots[s].IsEmpty)
                    {
                        hasFreeSpace = true;
                        continue;
                    }
                    if (s > 0 && slots[s - 1].IsEmpty)
                        errors.Add($"Bottle {b + 1} has a gap below slot {s + 1}.");
                    colorCounts.TryGetValue(slots[s].Color, out var count);
                    colorCounts[slots[s].Color] = count + 1;
                }
            }

            foreach (var pair in colorCounts)
                if (pair.Value % level.Capacity != 0)
                    errors.Add($"Color {pair.Key} has {pair.Value} items, must be a multiple of {level.Capacity}.");

            if (!hasFreeSpace) errors.Add("Every bottle is full, no move is possible.");
        }
    }
}
