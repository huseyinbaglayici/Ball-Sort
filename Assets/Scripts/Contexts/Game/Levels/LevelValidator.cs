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

            for (var b = 0; b < bottles.Count; b++)
                if (!LevelRules.IsTubeCapacity(bottles[b].Capacity))
                    errors.Add($"Bottle {b + 1} holds {bottles[b].Capacity}; tubes come in {string.Join("/", LevelRules.TubeCapacities)}.");

            var rows = new Dictionary<int, int>();
            foreach (var bottle in bottles)
            {
                var row = (int)System.Math.Round(bottle.Position.y * 100f);
                rows.TryGetValue(row, out var inRow);
                rows[row] = inRow + 1;
            }
            foreach (var row in rows.Values)
                if (row > LevelRules.MaxPerRow) errors.Add($"A row has {row} bottles; at most {LevelRules.MaxPerRow} fit.");

            var frozen = new HashSet<int>();
            foreach (var curtain in level.Curtains)
            {
                if (curtain.Bottle < 0 || curtain.Bottle >= bottles.Count)
                {
                    errors.Add($"A frozen tube is on missing bottle {curtain.Bottle + 1}.");
                    continue;
                }
                if (!frozen.Add(curtain.Bottle)) errors.Add($"Bottle {curtain.Bottle + 1} is frozen twice.");
                colorCounts.TryGetValue(curtain.Color, out var have);
                if (curtain.Need <= 0 || curtain.Need > have)
                    errors.Add($"Frozen bottle {curtain.Bottle + 1} waits for {curtain.Need} of color {curtain.Color}, the level has {have}.");
            }

            // Key id -> bottle it starts in.
            var keys = new Dictionary<string, int>();
            for (var b = 0; b < bottles.Count; b++)
                foreach (var slot in bottles[b].Slots)
                    if (!slot.IsEmpty && slot.IsKey && !keys.TryAdd(slot.Key, b)) errors.Add($"Key {slot.Key} appears twice.");
            var boxes = new HashSet<string>();
            foreach (var box in level.Lockboxes)
            {
                if (string.IsNullOrEmpty(box.Id)) errors.Add("A lockbox has no id.");
                else if (!boxes.Add(box.Id)) errors.Add($"Lockbox {box.Id} is defined twice.");
                if (box.Bottles.Count == 0) errors.Add($"Lockbox {box.Id} closes no bottle.");
                if (!keys.TryGetValue(box.Id ?? string.Empty, out var keyBottle)) errors.Add($"Lockbox {box.Id} has no key ball.");
                else if (box.Bottles.Contains(keyBottle)) errors.Add($"Key {box.Id} starts inside a bottle its own box closes.");
                foreach (var b in box.Bottles)
                    if (b < 0 || b >= bottles.Count) errors.Add($"Lockbox {box.Id} closes missing bottle {b + 1}.");
            }
            foreach (var key in keys.Keys)
                if (!boxes.Contains(key)) errors.Add($"Key {key} opens no lockbox.");

            if (!hasFreeSpace) errors.Add("Every bottle is full, no move is possible.");
        }
    }
}
