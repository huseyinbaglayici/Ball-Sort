using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Tests
{
    // Expected values are worked out by hand from the layout rules, not taken from the code.
    public sealed class BoardLayoutTests
    {
        private const float Aspect = 1170f / 2532f;
        private const float Tolerance = 1e-4f;

        private static Vector2[] Place(Vector2[] positions, int[] capacities, int level, out float[] rises,
            out float ortho) => new BoardLayout().Place(positions, capacities, level, Aspect, out rises, out ortho);

        private static void AssertPoint(Vector2 expected, Vector2 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, what + " x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, what + " y");
        }

        private static int[] Fours(int n) => Enumerable.Repeat(4, n).ToArray();

        [Test]
        public void TwoRowsOfSixArePackedAndCentered()
        {
            // An integer-grid layout of two rows of six (rows at y -1 and 6, columns at odd x).
            var positions = new List<Vector2>();
            foreach (var y in new[] { 6f, -1f })
                for (var x = -5; x <= 5; x += 2) positions.Add(new Vector2(x, y));

            var origins = Place(positions.ToArray(), Fours(12), 39, out var rises, out var ortho);

            // Rows 7 apart, pitch 1.8 + 0.3. Bounds x -6.25..6.25, y -1..12 (center 5.5), offset -0.7.
            for (var i = 0; i < 6; i++)
            {
                var x = -5.25f + 2.1f * i;
                AssertPoint(new Vector2(x, 7f - 5.5f - 0.7f), origins[i], $"top {i}");
                AssertPoint(new Vector2(x, 0f - 5.5f - 0.7f), origins[6 + i], $"bottom {i}");
            }
            // Width max(12.5 + 0, 12.7) wins over height (13 + 12) / 2.
            Assert.AreEqual(12.7f / Aspect * 0.5f, ortho, Tolerance);
            Assert.That(rises, Is.All.EqualTo(0f));
        }

        [Test]
        public void AddedTwoSlotTubeIsDrawnShort()
        {
            var positions = new[] { new Vector2(-2.35f, 0f), new Vector2(0f, 0f), new Vector2(2.35f, 0f) };

            Place(positions, new[] { 4, 4, 4, 2 }, 60, out var rises, out _);

            Assert.AreEqual(0f, rises[0], Tolerance);
            Assert.AreEqual(TubeArt.Rise(2), rises[3], Tolerance);
            Assert.Less(rises[3], 0f);
        }

        [Test]
        public void FirstLevelsSpreadShortRows()
        {
            var positions = new[] { new Vector2(-2.35f, 0f), new Vector2(0f, 0f), new Vector2(2.35f, 0f) };

            var early = Place(positions, Fours(3), 3, out _, out _);
            var later = Place(positions, Fours(3), BoardLayout.SpreadBeforeLevel, out _, out _);

            // Spread: step (2.05 * 5 - 3 * 1.8) / 2 = 2.425, centered. Later levels pack at 2.1.
            Assert.AreEqual(-2.425f, early[0].x, Tolerance);
            Assert.AreEqual(2.425f, early[2].x, Tolerance);
            Assert.AreEqual(-2.1f, later[0].x, Tolerance);
            Assert.AreEqual(2.1f, later[2].x, Tolerance);
        }

        [Test]
        public void ThreeRowsWithTallTubeKeepItsColumnFree()
        {
            // Level 39 v3: three rows; an 8-ball tube in the middle row, centered on the rows it spans.
            var positions = new[]
            {
                new Vector2(-4.7f, 6.8f), new Vector2(-2.35f, 6.8f), new Vector2(2.35f, 6.8f), new Vector2(4.7f, 6.8f),
                new Vector2(-4.7f, 0f), new Vector2(-2.35f, 0f), new Vector2(0f, 0f), new Vector2(2.35f, 0f),
                new Vector2(4.7f, 0f),
                new Vector2(-4.7f, -6.8f), new Vector2(-2.35f, -6.8f), new Vector2(2.35f, -6.8f), new Vector2(4.7f, -6.8f),
            };
            var capacities = Fours(13);
            capacities[6] = 8;

            var origins = Place(positions, capacities, 39, out var rises, out var ortho);

            // Rows 6.3 apart. Bounds y -1 .. 12.6 + 5: center 8.3, offset -1.7, so y - 10.
            var top = new[] { -4.2f, -2.1f, 2.1f, 4.2f };
            var middle = new[] { -4.2f, -2.1f, 0f, 2.1f, 4.2f };
            var bottom = new[] { -3.15f, -1.05f, 1.05f, 3.15f };
            for (var i = 0; i < 4; i++) AssertPoint(new Vector2(top[i], 2.6f), origins[i], $"top {i}");
            for (var i = 0; i < 5; i++) AssertPoint(new Vector2(middle[i], -3.7f), origins[4 + i], $"middle {i}");
            for (var i = 0; i < 4; i++) AssertPoint(new Vector2(bottom[i], -10f), origins[9 + i], $"bottom {i}");

            // Height (18.6 + 12) / 2 wins over width max(10.4 + 0.7, 13.2).
            Assert.AreEqual(15.3f, ortho, Tolerance);
            // The tall tube reaches the row above.
            Assert.AreEqual(6.3f, rises[6], Tolerance);
        }

        [Test]
        public void RowAboveTallTubeSkipsPlacesNearIt()
        {
            // Two rows; the tall tube is the second of four in the bottom row, so it is not centered.
            var positions = new[]
            {
                new Vector2(-4.7f, 3.4f), new Vector2(-2.35f, 3.4f), new Vector2(2.35f, 3.4f), new Vector2(4.7f, 3.4f),
                new Vector2(0f, 0f), new Vector2(-2.35f, -3.4f), new Vector2(2.35f, -3.4f), new Vector2(4.7f, -3.4f),
            };
            var capacities = Fours(8);
            capacities[4] = 8;

            var origins = Place(positions, capacities, 20, out _, out _);

            // The top place at -2.1 is too close to the tall tube, so it and the rest move right of it.
            var shiftX = origins[4].x + 1.05f;
            var expected = new[] { -4.2f, 1.05f, 3.15f, 5.25f };
            for (var i = 0; i < 4; i++) Assert.AreEqual(expected[i], origins[i].x - shiftX, Tolerance, $"top {i}");
            Assert.AreEqual(-3.15f, origins[5].x - shiftX, Tolerance);
        }

        [Test]
        public void AddedBottleJoinsTheShortestRowAtItsEnd()
        {
            var positions = new[]
            {
                new Vector2(-2.35f, 3.4f), new Vector2(0f, 3.4f), new Vector2(2.35f, 3.4f),
                new Vector2(-1.175f, -3.4f), new Vector2(1.175f, -3.4f),
            };

            var origins = Place(positions, Fours(6), 30, out _, out _);

            // The bottom row goes from two to three, the added one at the right end, packed and centered.
            Assert.AreEqual(origins[3].y, origins[5].y, Tolerance);
            Assert.AreEqual(origins[0].x, origins[3].x, Tolerance);
            Assert.AreEqual(origins[2].x, origins[5].x, Tolerance);
        }

        [Test]
        public void TallBoardsStayBetweenTheHudBlocks()
        {
            // Five rows of six: the plain fit would run under the top HUD.
            var positions = new List<Vector2>();
            for (var r = -2; r <= 2; r++)
                for (var c = 0; c < 6; c++) positions.Add(new Vector2((c - 2.5f) * 2.35f, r * 6.8f));

            var origins = Place(positions.ToArray(), Fours(30), 60, out var rises, out var ortho);

            var low = origins.Min(o => o.y) + TubeArt.BottomFromOrigin;
            var high = origins.Max(o => o.y) + TubeArt.BottomFromOrigin + TubeArt.MouthLocalY;
            Assert.GreaterOrEqual(0.5f + low / (2f * ortho), 0.15f - Tolerance);
            Assert.LessOrEqual(0.5f + high / (2f * ortho), 0.77f + Tolerance);
            Assert.AreEqual(7f, origins[6].y - origins[0].y, Tolerance);
        }

        [TestCase(4, -1f, 5f)]
        [TestCase(8, -0.5f, 9.5f)]
        [TestCase(12, -1f, 13f)]
        [TestCase(16, -0.25f, 17.75f)]
        [TestCase(2, -0.5f, 3.5f)]
        public void BoundsDependOnCapacity(int capacity, float below, float above)
        {
            BoardLayout.BoundsOf(capacity, out var b, out var a);
            Assert.AreEqual(below, b, Tolerance);
            Assert.AreEqual(above, a, Tolerance);
        }
    }
}
