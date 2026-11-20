using System;
using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    // Filled by Tools/Project/Create Board Prefabs.
    [CreateAssetMenu(menuName = "Project/Level Palette", fileName = "LevelPalette")]
    public sealed class LevelPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Name;
            public Color Color;
            // Tint for white obstacle parts tied to this color.
            public Color Surface;
            public OrbSkin Orb;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public int Count => _entries.Length;

        public Color Get(int index)
        {
            if (index >= 0 && index < _entries.Length) return _entries[index].Color;
            return Color.HSVToRGB(Mathf.Repeat(index * 0.618f, 1f), 0.65f, 0.95f);
        }

        public Color GetSurface(int index) =>
            index >= 0 && index < _entries.Length ? _entries[index].Surface : Get(index);

        public string Name(int index) => index >= 0 && index < _entries.Length ? _entries[index].Name : null;

        // Out-of-range indices (bad level data) wrap around the palette.
        public OrbSkin GetOrb(int index) => _entries[(int)Mathf.Repeat(index, _entries.Length)].Orb;
    }
}
