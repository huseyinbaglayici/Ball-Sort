using System.Collections.Generic;

namespace Project.Contexts.Game.Levels
{
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
                if (slots.Count == 0) errors.Add($"Bottle {b + 1} has no slots.");

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

            // One capacity: every color fills whole bottles. Mixed (long) capacities: whole groups of the smallest one.
            var unit = int.MaxValue;
            foreach (var bottle in bottles) unit = System.Math.Min(unit, bottle.Capacity);
            foreach (var pair in colorCounts)
                if (unit > 0 && pair.Value % unit != 0)
                    errors.Add($"Color {pair.Key} has {pair.Value} items, must be a multiple of {unit}.");

            foreach (var curtain in level.Curtains)
                if (curtain.Bottle < 0 || curtain.Bottle >= bottles.Count) errors.Add($"A curtain is on missing bottle {curtain.Bottle + 1}.");

            var keys = new HashSet<string>();
            foreach (var bottle in bottles)
            foreach (var slot in bottle.Slots)
                if (!slot.IsEmpty && slot.IsKey) keys.Add(slot.Key);
            foreach (var box in level.Lockboxes)
            {
                if (!keys.Contains(box.Id)) errors.Add($"Lockbox {box.Id} has no key ball.");
                foreach (var b in box.Bottles)
                    if (b < 0 || b >= bottles.Count) errors.Add($"Lockbox {box.Id} closes missing bottle {b + 1}.");
            }

            if (!hasFreeSpace) errors.Add("Every bottle is full, no move is possible.");
        }
    }
}
