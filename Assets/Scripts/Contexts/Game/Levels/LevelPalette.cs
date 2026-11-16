using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    /// <summary>Maps a level's color index to a display color. Indexes past the list get a generated color.</summary>
    [CreateAssetMenu(menuName = "Project/Level Palette", fileName = "LevelPalette")]
    public sealed class LevelPalette : ScriptableObject
    {
        // 0-7 are the art team's color scale. 8-11 (fuchsia, white, emerald, sky blue) are filled in the same style
        // until the art team extends the scale; levels use up to 10 colors.
        [SerializeField] private Color[] _colors =
        {
            new(1f, 0.0706f, 0.051f), new(0.1098f, 0.1216f, 1f), new(0.702f, 1f, 0.051f), new(1f, 0.7686f, 0.051f),
            new(0.6588f, 0.2118f, 1f), new(1f, 0.4196f, 0.051f), new(0f, 1f, 0.9216f), new(1f, 0.4392f, 0.851f),
            new(1f, 0.051f, 0.5412f), new(0.949f, 0.949f, 1f), new(0.051f, 1f, 0.3686f), new(0.051f, 0.651f, 1f)
        };

        public int Count => _colors.Length;

        public Color Get(int index)
        {
            if (index >= 0 && index < _colors.Length) return _colors[index];
            return Color.HSVToRGB(Mathf.Repeat(index * 0.618f, 1f), 0.65f, 0.95f);
        }
    }
}
