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
        // Top of the empty cap.
        private const float MouthPx = 10f;
        // Widest point: the empty cap.
        private const float WidthPx = 339f;

        private const float BallPx = 200f;
        private const float BallGapPx = 3f;
        private const float SlotSpacingPx = BallPx + BallGapPx;
        private const float BottomSlotPx = 840f;

        // The orb art is drawn with a 181 px sphere; scaled up to BallPx until art at that size replaces it.
        public const float OrbArtScale = 1.105f;
        public const float OrbPixelsPerUnit = PixelsPerUnit / OrbArtScale;

        // The completed cap (384x384, centered pivot) sits 512 px above the tube art's center.
        private const float CapOffsetPx = 512f;

        // Glass rows above and below these stay fixed when a tube stretches.
        public const float GlassTopBorderPx = 150f;
        public const float GlassBottomBorderPx = 154f;

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

        // Ball light 0, glow 1, body 2-6 + BallSortingSpan per stack place (top 21), +20 flying; all under FrontOrder.
        public const int BallSortingSpan = 5;
        public const int BackOrder = -10;
        public const int EndPieceOrder = -9;
        // Front glass is drawn twice, under and over the balls; the shares add up to the art where no ball is.
        public const int GlassBehindOrder = -3;
        public const int FrontOrder = 50;
        public const float GlassOverBalls = 0.05f;
        public const int GlowOrder = 55;
        // Additive copy of the glow that flashes on completion.
        public const int GlowFlashOrder = 56;
        // The cap sits behind the balls; only its front rim (RimFront) is drawn over them.
        public const int EmptyCapOrder = -5;
        // Over flying balls too, so a ball in the mouth looks half inside.
        public const int RimFrontOrder = 65;
        public const int CompletedCapOrder = 71;
        public const int GemOrder = 72;
        // Ice over every tube layer, its badge orb and break FX above it.
        public const int FrozenOrder = 80;
        public const int FrozenBadgeOrder = 81;
        public const int MeltOrder = 87;
        // Ball overlays share the rim's order; their depth keeps them in a fixed order on top of the body.
        public const int BallOverlayOrder = 6;
        public const float BallOverlayDepth = 0.001f;
        // Board-level groups: a shutter over idle tubes only, the blast over everything.
        public const int ShutterGroupOrder = 1;
        public const int BlastGroupOrder = 30;

        // Obstacle art drawn for this tube and a 200 px orb.
        public const float ArtHeightPx = 1024f;
        public const float ArtWidthPx = 384f;

        // Local y in the art's space (center origin) of a pixel row counted from the art's top.
        public static float ArtY(float px) => (CenterPx - px) / PixelsPerUnit;

        // Extra mouth height for a tube not Slots balls tall (negative if shorter); the straight middle takes it.
        public static float Rise(int capacity) => (capacity - Slots) * SlotHeight;

        // Widest width, and height from bottom to mouth.
        public static Vector2 Size => new(WidthPx / PixelsPerUnit, MouthLocalY);

        // Slot 0 is the bottom.
        public static Vector3 SlotLocalPosition(int slot) =>
            new(0f, (BottomPx - BottomSlotPx + SlotSpacingPx * slot) / PixelsPerUnit, 0f);
    }
}
