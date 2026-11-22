using System;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    public enum FreeMoveHint
    {
        // Every tube that can take the lifted balls opens its warp portal and breathes in a tap-me idle.
        Portals,
        // Every tube that can take the lifted balls breathes in the same idle, with the slots they would land in outlined.
        SlotOutlines,
    }

    // Booster looks: the undo's cap-off, the extra tube's descent from orbit and the Free Move target hints.
    [Serializable]
    public sealed class BoosterFxSettings
    {
        [Header("Undo")]
        [Tooltip("Undoing a completing move lifts the cap off and fades it while the open rim comes back: time and lift in board units.")]
        public float UncapTime = 0.16f;
        public float UncapLift = 0.5f;

        [Header("Extra tube")]
        [Tooltip("From the booster press to the settled tube; the descent takes what the touchdown squash and settle leave.")]
        public float AppearTime = 0.75f;
        [Tooltip("The new tube starts this far above its place and this far to the side, then glides down on a soft arc.")]
        public float DescentHeight = 2.6f;
        public float DescentSide = 0.5f;
        [Tooltip("It grows from this share of its size and fades in over this share of the descent.")]
        [Range(0f, 1f)] public float DescentScaleFrom = 0.82f;
        [Range(0f, 1f)] public float DescentFadeShare = 0.45f;
        [Tooltip("Touchdown: a small squash, then a settle.")]
        public Vector2 LandSquash = new(1.05f, 0.95f);
        public float LandSquashTime = 0.06f;
        public float LandSettleTime = 0.12f;
        [Tooltip("All tubes slide to their new places together, linearly, as the view widens linearly over this time.")]
        public float RelayoutTime = 0.34f;

        [Header("Touchdown ring")]
        [Tooltip("Ring round the new tube's foot: width in board units and height over width, like the tube base in perspective.")]
        public float TouchRingWidth = 2.7f;
        [Range(0.1f, 1f)] public float TouchRingAspect = 0.28f;
        [Tooltip("Height of the ring's center over the tube bottom, so it sits round the base plate.")]
        public float TouchRingY = 0.12f;
        [Tooltip("It opens quickly, then tightens to this share as it fades out: open and total times.")]
        public float TouchRingOpen = 0.08f;
        [Range(0.5f, 1f)] public float TouchRingTighten = 0.86f;
        public float TouchRingTime = 0.6f;
        [Tooltip("Near and far halves of the thin line and of its glow; the far half passes behind the tube base.")]
        public Sprite TouchLineNear;
        public Sprite TouchLineFar;
        public Sprite TouchGlowNear;
        public Sprite TouchGlowFar;
        [Tooltip("Gold glint that runs once round the ring: sprite and size in board units.")]
        public Sprite Glint;
        public float GlintSize = 0.36f;
        public Color TouchLineColor = new(0.86f, 0.96f, 1f, 1f);
        public Color TouchGlowColor = new(0.55f, 0.35f, 1f, 1f);
        public Color GlintColor = new(1f, 0.84f, 0.4f, 1f);

        [Header("Free Move")]
        [Tooltip("How the tubes that can take the lifted balls are shown while Free Move is armed.")]
        public FreeMoveHint Hint = FreeMoveHint.Portals;
        [Tooltip("Tap-me idle of each target tube: breath share and period; its balls breathe a little more, bottom first.")]
        [Range(0f, 0.05f)] public float IdleTubeBreath = 0.012f;
        [Range(0f, 0.08f)] public float IdleBallBreath = 0.025f;
        public float IdlePeriod = 1.2f;
        [Tooltip("Phase step between balls as a share of the period, bottom to top.")]
        [Range(0f, 0.5f)] public float IdleBallPhase = 0.08f;
        [Tooltip("Focus: target tubes ease their front glass to this tint, every other tube except the source to the dim one.")]
        public Color FocusTargetTint = Color.white;
        public Color FocusDimTint = new(0.35f, 0.35f, 0.35f, 1f);
        public float FocusTime = 0.2f;
        [Tooltip("Empty-slot rim, built like an orb: crisp rim, inner glow lit from the lower right, gloss on the upper left.")]
        public Sprite SlotRim;
        public Sprite SlotInner;
        public Sprite SlotGloss;
        public Color SlotRimColor = new(0.8f, 0.95f, 1f, 1f);
        public Color SlotInnerColor = new(0.55f, 0.35f, 1f, 0.55f);
        public Color SlotGlossColor = new(1f, 1f, 1f, 0.35f);
        [Tooltip("Rims breathe with the idle, each in step with the ball that would sit in its slot: alpha dip and scale swell.")]
        [Range(0f, 0.5f)] public float SlotBreathAlpha = 0.25f;
        [Range(0f, 0.1f)] public float SlotBreathScale = 0.02f;
        public float HintFadeTime = 0.12f;
    }
}
