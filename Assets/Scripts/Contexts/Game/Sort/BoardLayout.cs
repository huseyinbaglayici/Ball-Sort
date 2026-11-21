using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Rows are the level's distinct y values, bottom first; boards with tall tubes keep the level's grid columns.
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

        // Free band between the HUD blocks, in screen height (the HUD at 1170x2532); used when the caller passes none.
        [SerializeField, Range(0f, 1f)] private float _bandBottom = 0.14f;
        [SerializeField, Range(0f, 1f)] private float _bandTop = 0.79f;

        // Highest drawn point of a tube above its bottom: the completed cap's gem (102 px over the cap's center).
        private const float ArtTop = TubeArt.ContainerLocalY + TubeArt.CapLocalY + 102f / TubeArt.PixelsPerUnit;

        // Levels before this one spread rows of fewer than six bottles over the width of six.
        public const int SpreadBeforeLevel = 15;

        // Spacing of the level grid; a tall tube's y is the middle of the rows it spans.
        public const float GridPitch = 2.35f;
        public const float GridRowPitch = 6.8f;

        // A tall tube reaches one row up per TubeArt.Slots balls beyond the first Slots.
        private const int RowUnit = TubeArt.Slots;
        private const int FullRow = 6;
        // Landscape gets no HUD margin, only side padding.
        private const float LandscapePadding = 55f;
        private const float BoundsHalfWidth = 1f;

        private const int ThreeRows = 3;

        public Vector2[] Place(IReadOnlyList<Vector2> positions, IReadOnlyList<int> capacities, int levelNumber,
            float aspect, out float[] rises, out float orthoSize, Vector2? band = null) =>
            Place(positions, capacities, levelNumber < SpreadBeforeLevel, aspect, out rises, out orthoSize, band);

        // Centered on the camera; capacities lists level bottles then added ones; band is the free HUD gap (0-1).
        public Vector2[] Place(IReadOnlyList<Vector2> positions, IReadOnlyList<int> capacities, bool spread,
            float aspect, out float[] rises, out float orthoSize, Vector2? band = null)
        {
            var count = capacities.Count;
            var levelBottles = positions.Count;
            var rows = BuildRows(positions, capacities);
            var fit = rows.Count == ThreeRows ? _threeRows : _otherRows;
            var hasTall = false;
            for (var b = 0; b < levelBottles; b++) hasTall |= IsTall(capacities[b]);

            var origins = new Vector2[count];
            Vector2 size;
            if (!hasTall)
            {
                // An added bottle joins a row and the whole board is laid out again.
                for (var b = levelBottles; b < count; b++) AddToRow(rows, capacities, b);
                for (var r = 0; r < rows.Count; r++) PackRow(rows[r], r * fit.RowDistance, fit, spread, 0f, origins);
                size = Center(rows, capacities, fit, origins);
            }
            else
            {
                for (var r = 0; r < rows.Count; r++) PlaceOnGrid(rows[r], r * fit.RowDistance, positions, fit, origins);
                size = Center(rows, capacities, fit, origins);
                // Added bottles go in one at a time around the board as it stands.
                for (var b = levelBottles; b < count; b++) size = AddBeside(rows, capacities, b, fit, origins);
            }

            var portrait = aspect < 1f;
            var width = Mathf.Max((portrait ? fit.CameraPadding : LandscapePadding) + size.x, fit.MinWidth);
            var height = (portrait ? fit.CameraMargin : 0f) + size.y;
            orthoSize = Mathf.Max(width / aspect * 0.5f, height * 0.5f);

            // Every tube keeps its own height; a tall one stands on its row and grows into the rows above.
            rises = new float[count];
            for (var b = 0; b < count; b++) rises[b] = TubeArt.Rise(capacities[b]);

            KeepClearOfHud(origins, rises, band ?? new Vector2(_bandBottom, _bandTop), ref orthoSize);
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

        // Packed and centered on centerX.
        private static void PackRow(List<int> row, float y, Fit fit, bool spread, float centerX, Vector2[] origins)
        {
            RowSpacing(row.Count, fit, spread, out var start, out var step);
            for (var i = 0; i < row.Count; i++) origins[row[i]] = new Vector2(centerX + start + step * i, y);
        }

        // Level bottles keep their grid column.
        private static void PlaceOnGrid(List<int> row, float y, IReadOnlyList<Vector2> positions, Fit fit,
            Vector2[] origins)
        {
            var scale = fit.Pitch / GridPitch;
            foreach (var b in row) origins[b] = new Vector2(positions[b].x * scale, y);
        }

        // Moves the board so its bounds center on x 0 and on CenterOffsetY; returns the bounds size.
        private static Vector2 Center(List<List<int>> rows, IReadOnlyList<int> capacities, Fit fit, Vector2[] origins)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var row in rows)
            foreach (var b in row)
            {
                BoundsOf(capacities[b], out var below, out var above);
                min = Vector2.Min(min, new Vector2(origins[b].x - BoundsHalfWidth, origins[b].y + below));
                max = Vector2.Max(max, new Vector2(origins[b].x + BoundsHalfWidth, origins[b].y + above));
            }

            var shift = new Vector2(-(min.x + max.x) * 0.5f, fit.CenterOffsetY - (min.y + max.y) * 0.5f);
            foreach (var row in rows)
            foreach (var b in row) origins[b] += shift;
            return max - min;
        }

        // Added bottle on a tall-tube board: one pitch past its neighbour and any tall tube, rows rebalanced, then recentred.
        private static Vector2 AddBeside(List<List<int>> rows, IReadOnlyList<int> capacities, int bottle, Fit fit,
            Vector2[] origins)
        {
            var (r, index) = AddToRow(rows, capacities, bottle);
            var row = rows[r];
            var y = origins[row[index == 0 ? 1 : 0]].y;
            var tall = TallReaching(rows, r, capacities);
            if (tall.Count == 0 && !HasTall(row, capacities))
            {
                PackRow(row, y, fit, false, 0f, origins);
                return Center(rows, capacities, fit, origins);
            }

            var x = index == 0 ? origins[row[1]].x - fit.Pitch : origins[row[index - 1]].x + fit.Pitch;
            foreach (var t in tall)
                if (Mathf.Abs(origins[t].x - x) < fit.Pitch) x = origins[t].x + (index == 0 ? -fit.Pitch : fit.Pitch);
            origins[bottle] = new Vector2(x, y);

            float narrowest = float.MaxValue, widest = float.MinValue;
            for (var i = 0; i < rows.Count; i++)
            {
                var half = (rows[i].Count + TallReaching(rows, i, capacities).Count) * fit.Pitch * 0.5f;
                narrowest = Mathf.Min(narrowest, half);
                widest = Mathf.Max(widest, half);
            }
            var shift = widest - narrowest;
            if (shift > 0f)
                for (var i = 0; i < rows.Count; i++)
                    if (HasTall(rows[i], capacities) || TallReaching(rows, i, capacities).Count > 0)
                        foreach (var b in rows[i]) origins[b].x -= shift;
            return Center(rows, capacities, fit, origins);
        }

        private static bool HasTall(List<int> row, IReadOnlyList<int> capacities)
        {
            foreach (var b in row)
                if (IsTall(capacities[b])) return true;
            return false;
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
        private static (int Row, int Index) AddToRow(List<List<int>> rows, IReadOnlyList<int> capacities, int bottle)
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
            return (target, index);
        }

        // Zoom out just enough to fit art (up to a completed cap) inside the HUD band and center it; a fitting board stays.
        private static void KeepClearOfHud(Vector2[] origins, float[] rises, Vector2 band, ref float orthoSize)
        {
            if (origins.Length == 0 || band.y <= band.x) return;
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var b = 0; b < origins.Length; b++)
            {
                var bottom = origins[b].y + TubeArt.BottomFromOrigin;
                low = Mathf.Min(low, bottom);
                high = Mathf.Max(high, bottom + ArtTop + rises[b]);
            }

            var view = 2f * orthoSize;
            if (0.5f + low / view >= band.x && 0.5f + high / view <= band.y) return;
            orthoSize = Mathf.Max(orthoSize, (high - low) / (2f * (band.y - band.x)));
            var middle = ((band.x + band.y) * 0.5f - 0.5f) * 2f * orthoSize;
            var shift = middle - (low + high) * 0.5f;
            for (var b = 0; b < origins.Length; b++) origins[b].y += shift;
        }
    }
}
