using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// Measurements of the tube art in Art/Sprites/Tube (384x1024 px, pivot at the center) and what follows from them:
    /// ball size, slot positions, the mouth and the sorting of the tube layers. Shared by <see cref="BottleItem"/>, the
    /// ball and board views and the prefab builder. Pixel values are measured from the top of the image; local values
    /// are in tube space, whose origin is the bottom of the art.
    /// </summary>
    public static class TubeArt
    {
        public const float PixelsPerUnit = 190f;

        /// <summary>How many balls the art is drawn for.</summary>
        public const int Slots = 4;

        private const float CenterPx = 512f;
        private const float BottomPx = 1014f;
        private const float MouthPx = 10f;
        // The widest point, the mouth's top gold band.
        private const float WidthPx = 342f;

        // The four balls are as large as the glass allows: the stack runs from where the glass turns straight under the
        // mouth (153 px; the mouth's front rim ends at 150) down to 910 px, where the bottom ball rests on the floor ring
        // just past the glass bottom (900 px). Stacked balls overlap a little: centers are 0.984 ball diameters apart.
        // (Width is not the limit: the glass is 290 px wide inside, but four 290 px balls would not fit its height.)
        private const float StackTopPx = 153f;
        private const float StackBottomPx = 910f;
        private const float SpacingPerBall = (770f - 286f) / 3f / 164f;
        private const float BallPx = (StackBottomPx - StackTopPx) / (1f + (Slots - 1) * SpacingPerBall);
        private const float SlotSpacingPx = BallPx * SpacingPerBall;
        private const float BottomSlotPx = StackBottomPx - BallPx * 0.5f;

        // The completed cap (384x384, pivot at the center) sits 492 px above the tube art's center.
        private const float CapOffsetPx = 492f;

        /// <summary>Distance between two stacked ball centers.</summary>
        public const float SlotHeight = SlotSpacingPx / PixelsPerUnit;

        /// <summary>Diameter of one ball, as drawn in the tube art.</summary>
        public const float BallDiameter = BallPx / PixelsPerUnit;

        /// <summary>Local y of the art's center (the Container).</summary>
        public const float ContainerLocalY = (BottomPx - CenterPx) / PixelsPerUnit;

        /// <summary>Local y of the tube mouth.</summary>
        public const float MouthLocalY = (BottomPx - MouthPx) / PixelsPerUnit;

        /// <summary>Local y of the completed cap in the Container.</summary>
        public const float CapLocalY = CapOffsetPx / PixelsPerUnit;

        /// <summary>
        /// Height of the art bottom relative to a bottle's origin in the board layout (negative: below it), so the tubes
        /// sit where <see cref="Sort.BoardLayout"/> expects them.
        /// </summary>
        public const float BottomFromOrigin = -0.1385f;

        // Sorting inside the tube's group. Balls use 1 with their highlight at -1/-2, and +20 while flying.
        public const int BackOrder = -10;
        // The whole mouth, under the balls and their highlight, so its back rim and inside stay behind them.
        public const int MouthOrder = -5;
        public const int FrontOrder = 50;
        public const int OutlineOrder = 60;
        // The front half of the mouth, over the balls (1, 21 while flying), so a ball in the mouth is half inside.
        public const int RimFrontOrder = 65;
        public const int StopperOrder = 70;
        public const int GemOrder = 71;

        /// <summary>Width of the art at its widest point and height from its bottom to the mouth.</summary>
        public static Vector2 Size => new(WidthPx / PixelsPerUnit, MouthLocalY);

        /// <summary>Local position of a ball center in its slot, from the bottom (slot 0).</summary>
        public static Vector3 SlotLocalPosition(int slot) =>
            new(0f, (BottomPx - BottomSlotPx + SlotSpacingPx * slot) / PixelsPerUnit, 0f);
    }
}
