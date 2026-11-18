using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Pixel values are from the top of the 384x1024 art; local values are in tube space, origin at its bottom.
    public static class TubeArt
    {
        public const float PixelsPerUnit = 190f;

        // How many balls the art is drawn for.
        public const int Slots = 4;

        private const float CenterPx = 512f;
        private const float BottomPx = 1014f;
        private const float MouthPx = 10f;
        // Widest point: the mouth's top gold band.
        private const float WidthPx = 342f;

        // Balls are 184 px, the largest that fits four with a 3 px gap, so they never overlap at rest or lifted.
        public const float OrbSpherePx = 180f;
        private const float BallPx = 184f;
        private const float BallGapPx = 3f;
        private const float SlotSpacingPx = BallPx + BallGapPx;
        private const float BottomSlotPx = 897f - BallPx / 2f;

        // The completed cap (384x384, centered pivot) sits 492 px above the tube art's center.
        private const float CapOffsetPx = 492f;

        public const float SlotHeight = SlotSpacingPx / PixelsPerUnit;

        public const float BallDiameter = BallPx / PixelsPerUnit;

        // Only the faint last tenth of neighbouring selection glows may overlap.
        private const float LiftedGlowApart = 0.9f;

        // Spaced so neighbouring selection glows only touch at their faint tails; never tighter than the stack.
        public static float LiftedSpacing(float glowScale) =>
            Mathf.Max(SlotHeight, BallDiameter * (1f + (glowScale - 1f) * LiftedGlowApart));

        // A stacked ball stretches upward from its bottom, so it may fill the gap but never reach the ball above.
        public const float MaxStackStretch = 1f + BallGapPx / BallPx;

        // Local y of the art's center (the Container).
        public const float ContainerLocalY = (BottomPx - CenterPx) / PixelsPerUnit;

        public const float MouthLocalY = (BottomPx - MouthPx) / PixelsPerUnit;

        // Relative to the Container.
        public const float CapLocalY = CapOffsetPx / PixelsPerUnit;

        // Art bottom relative to a bottle's origin, so tubes sit where Sort.BoardLayout expects them.
        public const float BottomFromOrigin = -0.1385f;

        // Ball light 0, glow 1, body 2-7 + BallSortingSpan per stack place (top 25), +20 flying; all under FrontOrder.
        public const int BallSortingSpan = 6;
        public const int BackOrder = -10;
        // Under the balls so the mouth's back rim and inside stay behind them.
        public const int MouthOrder = -5;
        // Glass is drawn twice, under and over the balls; the halves sum to the original art where there is no ball.
        public const int GlassBehindOrder = -3;
        public const int FrontOrder = 50;
        public const float GlassOverBalls = 0.05f;
        public const int OutlineOrder = 60;
        // Additive completion glow, over the glass and its outline.
        public const int OutlineGlowOrder = 61;
        // Front half of the mouth, over flying balls too, so a ball in the mouth looks half inside.
        public const int RimFrontOrder = 65;
        // Additive light behind the completed cap.
        public const int CapGlowOrder = 69;
        public const int StopperOrder = 70;
        public const int GemOrder = 71;

        // Extra mouth height for a tube not Slots balls tall (negative if shorter); the straight middle takes it.
        public static float Rise(int capacity) => (capacity - Slots) * SlotHeight;

        // Widest width, and height from bottom to mouth.
        public static Vector2 Size => new(WidthPx / PixelsPerUnit, MouthLocalY);

        // Slot 0 is the bottom.
        public static Vector3 SlotLocalPosition(int slot) =>
            new(0f, (BottomPx - BottomSlotPx + SlotSpacingPx * slot) / PixelsPerUnit, 0f);
    }
}
