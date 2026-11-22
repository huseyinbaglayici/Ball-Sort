using UnityEngine;

namespace Project.Contexts.LiveOps
{
    // Spots on the 1170x2532 event map art, in pixels from its top-left.
    [CreateAssetMenu(menuName = "Project/Orbit Rush Map Layout", fileName = "OrbitRushMapLayout")]
    public sealed class OrbitRushMapLayout : ScriptableObject
    {
        public static readonly Vector2 MapSize = new(1170f, 2532f);

        // Path tiles from the start pad to the treasure; the marker walks them.
        public Vector2[] Stops =
        {
            new(895, 2041), new(648, 1924), new(456, 1918), new(300, 1848), new(228, 1640),
            new(500, 1652), new(684, 1642), new(874, 1576), new(965, 1462), new(911, 1240),
            new(586, 1218), new(390, 1171), new(215, 1089), new(304, 975), new(582, 700)
        };

        // Stop index where each stage starts, then the last stage's end and the grand prize.
        public int[] Waypoints = { 0, 2, 4, 9, 11, 13, 14 };

        // The stage gifts (flags and chests) in stage order, then the grand prize treasure.
        public Vector2[] Rewards =
        {
            new(810, 1772), new(228, 1600), new(911, 1200), new(367, 1393), new(658, 975), new(582, 660)
        };

        // Anchor on the map rect for a design pixel.
        public static Vector2 Anchor(Vector2 pixel) => new(pixel.x / MapSize.x, 1f - pixel.y / MapSize.y);
    }
}
