using System;
using System.Collections.Generic;
using Project.Contexts.Game.Levels;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // A null slot is empty; outItems, levelOrder and flags are ignored.
    public static class LevelJsonImporter
    {
        public readonly struct ImportedLevel
        {
            public readonly string Name;
            public readonly LevelData Level;

            public ImportedLevel(string name, LevelData level)
            {
                Name = name;
                Level = level;
            }
        }

        public static List<ImportedLevel> Parse(string json, bool allVariants)
        {
            var file = JsonUtility.FromJson<FileDto>(json);
            if (file?.levels == null) throw new FormatException("JSON has no \"levels\" array.");

            var result = new List<ImportedLevel>();
            foreach (var level in file.levels)
            {
                var count = allVariants ? level.variants.Count : Mathf.Min(1, level.variants.Count);
                for (var v = 0; v < count; v++)
                {
                    var name = allVariants ? $"Level_{level.level:000}_v{v + 1}" : $"Level_{level.level:000}";
                    result.Add(new ImportedLevel(name, ToLevel(level.variants[v])));
                }
            }
            return result;
        }

        private static LevelData ToLevel(VariantDto variant)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            var capacity = 1;
            foreach (var dto in variant.bottles)
            {
                var bottle = new BottleData { Position = new Vector2(dto.x, dto.y) };
                // JsonUtility turns a null slot into a default object; real items always have type 1 or 2.
                foreach (var slot in dto.slots)
                    bottle.Slots.Add(slot.type == 0
                        ? SlotData.Empty
                        : new SlotData { Color = slot.color, Kind = slot.type == 2 ? SlotKind.Special : SlotKind.Normal, Hidden = slot.hidden });
                capacity = Mathf.Max(capacity, bottle.Slots.Count);
                level.Bottles.Add(bottle);
            }
            level.Capacity = capacity;
            return level;
        }

        // DTO names must match the JSON keys.
        [Serializable] private sealed class FileDto { public List<LevelDto> levels; }
        [Serializable] private sealed class LevelDto { public int level; public List<VariantDto> variants; }
        [Serializable] private sealed class VariantDto { public List<BottleDto> bottles; }
        [Serializable] private sealed class BottleDto { public float x; public float y; public List<SlotDto> slots; }
        [Serializable] private sealed class SlotDto { public int color; public int type; public bool hidden; }
    }
}
