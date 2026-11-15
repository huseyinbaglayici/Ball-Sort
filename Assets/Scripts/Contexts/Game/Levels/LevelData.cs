using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    public enum SlotKind
    {
        Normal,
        Special
    }

    /// <summary>One cell of a bottle. <see cref="Color"/> is a palette index; <see cref="EmptyColor"/> means no item.</summary>
    [Serializable]
    public struct SlotData
    {
        public const int EmptyColor = -1;

        public int Color;
        public SlotKind Kind;
        public bool Hidden;

        public bool IsEmpty => Color < 0;

        public static SlotData Empty => new() { Color = EmptyColor };
    }

    [Serializable]
    public sealed class BottleData
    {
        /// <summary>Layout position in world units (same y = same row).</summary>
        public Vector2 Position;

        /// <summary>Bottom to top. Count always equals the level's capacity.</summary>
        public List<SlotData> Slots = new();

        public static BottleData CreateEmpty(int capacity, Vector2 position)
        {
            var bottle = new BottleData { Position = position };
            for (var i = 0; i < capacity; i++) bottle.Slots.Add(SlotData.Empty);
            return bottle;
        }
    }

    /// <summary>A sort puzzle layout: bottles of equal capacity filled with colored items.</summary>
    [CreateAssetMenu(menuName = "Project/Level", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [SerializeField, Min(1)] private int _capacity = 4;
        [SerializeField] private List<BottleData> _bottles = new();

        public List<BottleData> Bottles => _bottles;

        public int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = Mathf.Max(1, value);
                foreach (var bottle in _bottles)
                {
                    while (bottle.Slots.Count < _capacity) bottle.Slots.Add(SlotData.Empty);
                    if (bottle.Slots.Count > _capacity) bottle.Slots.RemoveRange(_capacity, bottle.Slots.Count - _capacity);
                }
            }
        }

        public int ColorCount
        {
            get
            {
                var colors = new HashSet<int>();
                foreach (var bottle in _bottles)
                foreach (var slot in bottle.Slots)
                    if (!slot.IsEmpty) colors.Add(slot.Color);
                return colors.Count;
            }
        }
    }
}
