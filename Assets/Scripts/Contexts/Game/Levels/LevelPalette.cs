using UnityEngine;

namespace Project.Contexts.Game.Levels
{
    /// <summary>Maps a level's color index to a display color. Indexes past the list get a generated color.</summary>
    [CreateAssetMenu(menuName = "Project/Level Palette", fileName = "LevelPalette")]
    public sealed class LevelPalette : ScriptableObject
    {
        [SerializeField] private Color[] _colors =
        {
            new(0.93f, 0.26f, 0.26f), new(0.25f, 0.52f, 0.96f), new(0.30f, 0.80f, 0.35f), new(0.98f, 0.84f, 0.22f),
            new(0.62f, 0.32f, 0.88f), new(0.98f, 0.55f, 0.15f), new(0.30f, 0.85f, 0.88f), new(0.96f, 0.45f, 0.75f),
            new(0.55f, 0.35f, 0.20f), new(0.55f, 0.55f, 0.58f), new(0.10f, 0.40f, 0.25f), new(0.15f, 0.15f, 0.45f)
        };

        public int Count => _colors.Length;

        public Color Get(int index)
        {
            if (index >= 0 && index < _colors.Length) return _colors[index];
            return Color.HSVToRGB(Mathf.Repeat(index * 0.618f, 1f), 0.65f, 0.95f);
        }
    }
}
