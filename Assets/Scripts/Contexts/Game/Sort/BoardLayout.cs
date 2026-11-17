using System;
using System.Collections.Generic;
using Project.Contexts.Game.Levels;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// Places the bottles of a level and finds the camera size that fits them. Only the rows of the level count
    /// (bottles grouped by y, bottom row first, ordered by x); the stored x/y spacing is the level editor's and is not
    /// used. Each row is centered with a pitch of bottle width + gap, rows are a fixed distance apart, and the board is
    /// centered on the bounds of all bottles plus a fixed y offset. Three-row boards use their own spacing.
    /// </summary>
    [Serializable]
    public sealed class BoardLayout
    {
        [SerializeField] private float _bottleWidth = 1.8f;
        [SerializeField] private float _bottleGap = 0.3f;
        [SerializeField] private float _rowSpacing = 6.5f;
        [SerializeField] private float _rowSpacingThreeRows = 6.3f;
        [SerializeField] private float _centerOffsetY = -0.52f;
        [SerializeField] private float _centerOffsetYThreeRows = -1.7f;
        [SerializeField] private float _cameraPadding = 0.7f;
        [SerializeField] private float _minWidth = 13.2f;
        [SerializeField] private float _cameraMargin = 15f;
        [SerializeField] private float _cameraMarginThreeRows = 12f;

        // A bottle's bounds are 2 units wide and run from -h/4 to 3h/4 + 2 around its origin (h = capacity).
        private const float BoundsHalfWidth = 1f;
        private const float BoundsTopMargin = 2f;

        /// <summary>
        /// Returns each bottle's origin in board space, and in <paramref name="orthoSize"/> the orthographic size of a
        /// camera with <paramref name="aspect"/> that shows the whole board.
        /// </summary>
        public Vector2[] Place(LevelData level, float aspect, out float orthoSize)
        {
            var count = level.Bottles.Count;
            var rows = new SortedDictionary<float, List<int>>();
            for (var b = 0; b < count; b++)
            {
                var key = Mathf.Round(level.Bottles[b].Position.y * 100f) / 100f;
                if (!rows.TryGetValue(key, out var row)) rows[key] = row = new List<int>();
                row.Add(b);
            }

            var threeRows = rows.Count == 3;
            var rowPitch = threeRows ? _rowSpacingThreeRows : _rowSpacing;
            var pitch = _bottleWidth + _bottleGap;
            var origins = new Vector2[count];
            var r = 0;
            foreach (var row in rows.Values)
            {
                row.Sort((a, b) => level.Bottles[a].Position.x.CompareTo(level.Bottles[b].Position.x));
                var start = -0.5f * pitch * (row.Count - 1);
                for (var i = 0; i < row.Count; i++) origins[row[i]] = new Vector2(start + pitch * i, rowPitch * r);
                r++;
            }

            float h = level.Capacity;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in origins)
            {
                min = Vector2.Min(min, new Vector2(o.x - BoundsHalfWidth, o.y - h * 0.25f));
                max = Vector2.Max(max, new Vector2(o.x + BoundsHalfWidth, o.y + h * 0.75f + BoundsTopMargin));
            }

            var shift = new Vector2(0f, threeRows ? _centerOffsetYThreeRows : _centerOffsetY) - (min + max) * 0.5f;
            for (var b = 0; b < count; b++) origins[b] += shift;

            var size = max - min;
            var width = Mathf.Max(size.x + _cameraPadding, _minWidth);
            var height = size.y + (threeRows ? _cameraMarginThreeRows : _cameraMargin);
            orthoSize = Mathf.Max(width / aspect * 0.5f, height * 0.5f);
            return origins;
        }
    }
}
