using System;
using Project.Contexts.Application.Economy;
using UnityEngine;

namespace Project.Contexts.Home
{
    // Home poster for each chapter of levels; past the last chapter the list starts over.
    [CreateAssetMenu(menuName = "Project/Chapter Art", fileName = "ChapterArt")]
    public sealed class ChapterArt : ScriptableObject
    {
        [Serializable]
        public sealed class Chapter
        {
            public Sprite Poster;
            // Centre of the orb in Luno's hand as a share of the poster from its top-left corner.
            public Vector2 Orb = new(0.5f, 0.5f);
            // Orb width as a share of the poster width.
            public float OrbSize = 0.06f;
        }

        [SerializeField, Min(1)] private int _levelsPerChapter = CampaignRules.LevelsPerMetaPeriod;
        [SerializeField] private Chapter[] _chapters = Array.Empty<Chapter>();

        public int Count => _chapters.Length;

        public Chapter this[int index] => _chapters[index];

        public int IndexOf(int level) => IndexOf(level, _levelsPerChapter, _chapters.Length);

        public static int IndexOf(int level, int levelsPerChapter, int count) =>
            count <= 0 ? -1 : (Math.Max(1, level) - 1) / Math.Max(1, levelsPerChapter) % count;
    }
}
