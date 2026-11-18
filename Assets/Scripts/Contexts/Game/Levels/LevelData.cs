using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    public enum SlotKind
    {
        Normal,
        // Hidden ball: its color shows only once it comes open at an accessible mouth.
        Special
    }

    // Color is a palette index; EmptyColor means no item.
    [Serializable]
    public struct SlotData
    {
        public const int EmptyColor = -1;

        public int Color;
        public SlotKind Kind;
        public bool Hidden;

        // Lockbox id this key opens; empty when the ball is not a key.
        public string Key;

        // Transfers left before this bomb explodes; 0 when the ball is not a bomb.
        public int Fuse;

        // Already burning at the start; otherwise it lights once the ball comes open at a mouth.
        public bool FuseLit;

        public bool IsEmpty => Color < 0;
        public bool IsKey => !string.IsNullOrEmpty(Key);
        public bool IsBomb => Fuse > 0;

        public static SlotData Empty => new() { Color = EmptyColor };
    }

    [Serializable]
    public sealed class BottleData
    {
        // World units; same y means same row.
        public Vector2 Position;

        // Bottom to top; the count is the capacity.
        public List<SlotData> Slots = new();

        public int Capacity => Slots.Count;

        public static BottleData CreateEmpty(int capacity, Vector2 position)
        {
            var bottle = new BottleData { Position = position };
            for (var i = 0; i < capacity; i++) bottle.Slots.Add(SlotData.Empty);
            return bottle;
        }
    }

    // Unusable until one accessible completed bottle holds Need balls of Color.
    [Serializable]
    public sealed class CurtainData
    {
        public int Bottle;
        public int Color;
        public int Need = 4;
    }

    // Closes its bottles until the key ball with the same Id comes open at an accessible mouth.
    [Serializable]
    public sealed class LockboxData
    {
        public string Id;
        public List<int> Bottles = new();
    }

    [CreateAssetMenu(menuName = "Project/Level", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [SerializeField, Min(1)] private int _capacity = 4;
        [SerializeField] private List<BottleData> _bottles = new();
        [SerializeField] private List<CurtainData> _curtains = new();
        [SerializeField] private List<LockboxData> _lockboxes = new();

        public List<BottleData> Bottles => _bottles;
        public List<CurtainData> Curtains => _curtains;
        public List<LockboxData> Lockboxes => _lockboxes;

        // Setting it gives every bottle that capacity.
        public int Capacity
        {
            get
            {
                var max = 0;
                foreach (var bottle in _bottles) max = Mathf.Max(max, bottle.Capacity);
                return max > 0 ? max : _capacity;
            }
            set
            {
                _capacity = Mathf.Max(1, value);
                foreach (var bottle in _bottles) Resize(bottle, _capacity);
            }
        }

        public static void Resize(BottleData bottle, int capacity)
        {
            while (bottle.Slots.Count < capacity) bottle.Slots.Add(SlotData.Empty);
            if (bottle.Slots.Count > capacity) bottle.Slots.RemoveRange(capacity, bottle.Slots.Count - capacity);
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
