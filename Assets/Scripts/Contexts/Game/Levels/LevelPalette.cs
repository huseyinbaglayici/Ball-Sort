using System;
using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    /// <summary>
    /// Maps a level's color index to its orb art and its display color (the color the art is drawn in, used by the
    /// level editor). Filled by Tools/Project/Create Board Prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Level Palette", fileName = "LevelPalette")]
    public sealed class LevelPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Name;
            public Color Color;
            public OrbSkin Orb;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public int Count => _entries.Length;

        public Color Get(int index)
        {
            if (index >= 0 && index < _entries.Length) return _entries[index].Color;
            return Color.HSVToRGB(Mathf.Repeat(index * 0.618f, 1f), 0.65f, 0.95f);
        }

        /// <summary>The orb art of a color. An index past the palette (bad level data) repeats the palette.</summary>
        public OrbSkin GetOrb(int index) => _entries[(int)Mathf.Repeat(index, _entries.Length)].Orb;
    }
}
