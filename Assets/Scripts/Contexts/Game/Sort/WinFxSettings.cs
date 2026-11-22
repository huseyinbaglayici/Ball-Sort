using System;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // The level-won beat over the board, from the last landing to the coins: caps, orbit sweep, burst, mascot, ribbon.
    // The mascot and ribbon values are baked into the stage skeleton by the prefab builder; rebuild the prefabs after tuning them.
    [Serializable]
    public sealed class WinFxSettings
    {
        [Header("Caps")]
        [Tooltip("From the last tube's cap slam, every completed cap pulses in turn, left to right.")]
        public float CapStagger = 0.05f;
        public float CapPulseScale = 1.35f;
        public float CapPulseTime = 0.22f;

        [Header("Orbit sweep")]
        [Tooltip("One orbit ring starts after the last cap pulse at this share of the screen width and collapses to the centre.")]
        public float RingDelay = 0.05f;
        public float RingFrom = 1.1f;
        public float RingTo = 0.04f;
        public float RingTime = 0.45f;
        [Range(0.1f, 1f)] public float RingAspect = 0.3f;
        [Tooltip("The ring turns this many degrees while it closes.")]
        public float RingTurn = 40f;

        [Header("Burst")]
        [Tooltip("Where the ring collapses: a gold flash sized to the screen width, star dust and sparks.")]
        public float FlashSize = 0.9f;
        [Range(0f, 1f)] public float FlashAlpha = 0.8f;
        public float FlashTime = 0.35f;
        public int DustCount = 60;
        public Vector2 DustSpeed = new(3f, 8f);
        public int SparkCount = 24;
        public Vector2 SparkSpeed = new(10f, 16f);
        [Tooltip("A brief dim behind the tubes from the burst on, so the mascot and ribbon read over the board.")]
        [Range(0f, 1f)] public float DimAlpha = 0.45f;
        public float DimTime = 0.25f;

        [Header("Mascot")]
        [Tooltip("From the burst: the mascot swings in from off screen on an arc and rests over the board's centre, sized to the screen width.")]
        public float MascotDelay = 0.05f;
        public float MascotTime = 0.55f;
        public float MascotSize = 0.5f;
        [Tooltip("Rest point from the board centre and start point, in screen widths and heights.")]
        public Vector2 MascotRest = new(0f, -0.02f);
        public Vector2 MascotFrom = new(0.75f, -0.6f);
        public float MascotLean = 18f;
        [Tooltip("Landing squash, then a slow bob while it waits for the panel.")]
        public Vector2 MascotSquash = new(1.12f, 0.88f);
        public float MascotSquashTime = 0.09f;
        public float MascotRecover = 0.22f;
        public float MascotBob = 0.03f;
        public float MascotBobPeriod = 1.4f;

        [Header("Ribbon")]
        [Tooltip("From the mascot's landing: the ribbon drops in above it and settles, sized to the screen width, this gap over the mascot.")]
        public float RibbonDelay = 0.1f;
        public float RibbonTime = 0.32f;
        public float RibbonWidth = 0.85f;
        public float RibbonGap = 0.02f;
        public float RibbonDrop = 0.1f;
        public float RibbonFontSize = 18f;

        [Header("Hand-off")]
        [Tooltip("The effect ends this long after the panel opens; the panel's dim already covers it.")]
        public float AfterPanel = 0.5f;
    }
}
