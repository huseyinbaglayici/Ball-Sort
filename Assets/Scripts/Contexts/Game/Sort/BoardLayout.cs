using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Rows are the level's distinct y values, bottom first; rows that tall tubes reach into pack around them.
    [Serializable]
    public sealed class BoardLayout
    {
        [Serializable]
        public sealed class Fit
        {
            public float CenterOffsetY;
            public float RowDistance;
            public float BottleWidth = 1.8f;
            public float BottleGap = 0.3f;
            [Tooltip("Pitch of a full row of six in the first levels, which spread shorter rows over the same width.")]
            public float SixBottlePitch;
            public float CameraPadding;
            public float MinWidth;
            public float CameraMargin;

            public float Pitch => BottleWidth + BottleGap;
        }

        [SerializeField] private Fit _threeRows = new()
        {
            CenterOffsetY = -1.7f, RowDistance = 6.3f, SixBottlePitch = 2.1f, CameraPadding = 0.7f, MinWidth = 13.2f,
            CameraMargin = 12f,
        };

        [SerializeField] private Fit _otherRows = new()
        {
            CenterOffsetY = -0.7f, RowDistance = 7f, SixBottlePitch = 2.05f, CameraPadding = 0f, MinWidth = 12.7f,
            CameraMargin = 12f,
        };

        [SerializeField, Range(0f, 1f)] private float _bandBottom = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _bandTop = 0.77f;

        // Levels before this one spread rows of fewer than six bottles over the width of six.
        public const int SpreadBeforeLevel = 15;

        // Row spacing of the level grid; a tall tube's y is the middle of the rows it spans.
        public const float GridRowPitch = 6.8f;

        // A tall tube reaches one row up per TubeArt.Slots balls beyond the first Slots.
        private const int RowUnit = TubeArt.Slots;
        private const int FullRow = 6;
        // Landscape gets no HUD margin, only side padding.
        private const float LandscapePadding = 55f;
        private const float BoundsHalfWidth = 1f;

        private const int ThreeRows = 3;

        public Vector2[] Place(IReadOnlyList<Vector2> positions, IReadOnlyList<int> capacities, int levelNumber,
            float aspect, out float[] rises, out float orthoSize) =>
            Place(positions, capacities, levelNumber < SpreadBeforeLevel, aspect, out rises, out orthoSize);

        // Centered on the camera; capacities: level bottles then added ones; rises: extra height over the standard art.
        public Vector2[] Place(IReadOnlyList<Vector2> positions, IReadOnlyList<int> capacities, bool spread,
            float aspect, out float[] rises, out float orthoSize)
        {
            var count = capacities.Count;
            var rows = BuildRows(positions, capacities);
            var fit = rows.Count == ThreeRows ? _threeRows : _otherRows;
            var hasTall = false;
            for (var b = 0; b < count; b++) hasTall |= IsTall(capacities[b]);

            var origins = new Vector2[count];
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var y = r * fit.RowDistance;
                var tall = hasTall ? TallReaching(rows, r, capacities) : null;
                if (tall is { Count: > 0 })
                {
                    // One place per tall tube reaching up from below; a bottle closer than a pitch to one moves right.
                    var x = float.MinValue;
                    foreach (var b in row)
                    {
                        x = x <= float.MinValue ? fit.Pitch * (tall.Count + row.Count - 1) * -0.5f : x + fit.Pitch;
                        foreach (var t in tall)
                            if (Mathf.Abs(origins[t].x - x) + 0.001f < fit.Pitch) x = origins[t].x + fit.Pitch;
                        origins[b] = new Vector2(x, y);
                    }
                    continue;
                }

                RowSpacing(row.Count, fit, spread && !hasTall, out var start, out var step);
                for (var i = 0; i < row.Count; i++) origins[row[i]] = new Vector2(start + step * i, y);
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var b = 0; b < count; b++)
            {
                BoundsOf(capacities[b], out var below, out var above);
                min = Vector2.Min(min, new Vector2(origins[b].x - BoundsHalfWidth, origins[b].y + below));
                max = Vector2.Max(max, new Vector2(origins[b].x + BoundsHalfWidth, origins[b].y + above));
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            for (var b = 0; b < count; b++) origins[b] += new Vector2(-center.x, fit.CenterOffsetY - center.y);

            var portrait = aspect < 1f;
            var width = Mathf.Max((portrait ? fit.CameraPadding : LandscapePadding) + size.x, fit.MinWidth);
            var height = (portrait ? fit.CameraMargin : 0f) + size.y;
            orthoSize = Mathf.Max(width / aspect * 0.5f, height * 0.5f);

            rises = new float[count];
            for (var r = 0; r < rows.Count; r++)
                foreach (var b in rows[r])
                {
                    var reach = Mathf.Min(RowsAbove(capacities[b]), rows.Count - 1 - r);
                    // A short tube (e.g. an added 2-slot one) keeps its own lower mouth.
                    var rise = TubeArt.Rise(capacities[b]);
                    rises[b] = reach > 0 ? Mathf.Max(rise, reach * fit.RowDistance) : rise;
                }

            KeepClearOfHud(origins, rises, ref orthoSize);
            return origins;
        }

        // Vertical bounds relative to the origin; bottles are two units wide.
        public static void BoundsOf(int capacity, out float below, out float above)
        {
            var lift = capacity switch
            {
                <= 1 => -1.3f,
                <= 6 => 0f,
                <= 8 => 1.5f,
                <= 15 => 2f,
                _ => 3.75f,
            };
            var extra = capacity <= 1 ? 2f : 0f;
            below = lift - capacity * 0.25f;
            above = lift + 2f + extra + capacity * 0.75f;
        }

        private static bool IsTall(int capacity) => capacity > RowUnit;

        private static int RowsAbove(int capacity) => IsTall(capacity) ? capacity / RowUnit - 1 : 0;

        private static void RowSpacing(int n, Fit fit, bool spread, out float start, out float step)
        {
            if (!spread)
            {
                step = fit.Pitch;
                start = step * (n - 1) * -0.5f;
                return;
            }

            var full = fit.SixBottlePitch * (FullRow - 1);
            var missing = (FullRow - n) * fit.BottleWidth;
            step = n > 1 ? (full - missing) / (n - 1) : 0f;
            start = n > 1 ? -(full * 0.5f - missing * 0.5f) : 0f;
        }

        // Tall tubes from lower rows reaching row r, nearest row first.
        private static List<int> TallReaching(List<List<int>> rows, int r, IReadOnlyList<int> capacities)
        {
            var found = new List<int>();
            for (var below = r - 1; below >= 0; below--)
                foreach (var b in rows[below])
                    if (RowsAbove(capacities[b]) >= r - below) found.Add(b);
            return found;
        }

        private static List<List<int>> BuildRows(IReadOnlyList<Vector2> positions, IReadOnlyList<int> capacities)
        {
            var levelBottles = positions.Count;
            float Key(float y) => Mathf.Round(y * 100f) / 100f;

            var keys = new SortedSet<float>();
            for (var b = 0; b < levelBottles; b++)
                if (!IsTall(capacities[b])) keys.Add(Key(positions[b].y));
            if (keys.Count == 0)
                for (var b = 0; b < levelBottles; b++) keys.Add(Key(positions[b].y));
            var rowYs = new List<float>(keys);

            var rows = new List<List<int>>();
            foreach (var _ in rowYs) rows.Add(new List<int>());
            for (var b = 0; b < levelBottles; b++) rows[RowOf(rowYs, positions[b].y, capacities[b])].Add(b);
            foreach (var row in rows)
                row.Sort((a, b) => positions[a].x != positions[b].x ? positions[a].x.CompareTo(positions[b].x) : a.CompareTo(b));

            for (var b = levelBottles; b < capacities.Count; b++) AddToRow(rows, capacities, b);
            return rows;
        }

        // A tall tube's y is the middle of the rows it spans, so it stands on the lowest of them.
        private static int RowOf(List<float> rowYs, float y, int capacity)
        {
            if (!IsTall(capacity))
            {
                var best = 0;
                for (var r = 1; r < rowYs.Count; r++)
                    if (Mathf.Abs(rowYs[r] - y) < Mathf.Abs(rowYs[best] - y)) best = r;
                return best;
            }

            var span = Mathf.CeilToInt(capacity / (float)RowUnit);
            var from = y - (span - 1) * GridRowPitch * 0.5f - 0.01f;
            var start = rowYs.FindIndex(rowY => rowY >= from);
            return start < 0 ? rowYs.Count - 1 : start;
        }

        // Lowest row with fewest places, right end; near tall tubes, left end if rows differ by 2+ and count is even.
        private static void AddToRow(List<List<int>> rows, IReadOnlyList<int> capacities, int bottle)
        {
            int Places(int r) => rows[r].Count + TallReaching(rows, r, capacities).Count;

            var target = 0;
            for (var r = 1; r < rows.Count; r++)
                if (Places(r) < Places(target)) target = r;

            var index = rows[target].Count;
            if (TallReaching(rows, target, capacities).Count > 0)
            {
                int least = int.MaxValue, most = int.MinValue;
                for (var r = 0; r < rows.Count; r++)
                {
                    least = Mathf.Min(least, Places(r));
                    most = Mathf.Max(most, Places(r));
                }
                if (most - least >= 2 && index % 2 == 0) index = 0;
            }
            rows[target].Insert(index, bottle);
        }

        // Art outside the band between HUD blocks: zoom out just enough and shift it in the least distance.
        private void KeepClearOfHud(Vector2[] origins, float[] rises, ref float orthoSize)
        {
            if (origins.Length == 0) return;
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var b = 0; b < origins.Length; b++)
            {
                var bottom = origins[b].y + TubeArt.BottomFromOrigin;
                low = Mathf.Min(low, bottom);
                high = Mathf.Max(high, bottom + TubeArt.MouthLocalY + rises[b]);
            }

            var band = _bandTop - _bandBottom;
            if (band <= 0f) return;
            orthoSize = Mathf.Max(orthoSize, (high - low) / (2f * band));
            var bandLow = (_bandBottom - 0.5f) * 2f * orthoSize;
            var bandHigh = (_bandTop - 0.5f) * 2f * orthoSize;
            var shift = 0f;
            if (high > bandHigh) shift = bandHigh - high;
            if (low + shift < bandLow) shift = bandLow - low;
            if (shift == 0f) return;
            for (var b = 0; b < origins.Length; b++) origins[b].y += shift;
        }
    }
}
