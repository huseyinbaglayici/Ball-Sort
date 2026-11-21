using System;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // The beat before the result panel: an orbital burst on a win, a small contraction on a loss.
    [Serializable]
    public sealed class ResultFxSettings
    {
        [Header("Win")]
        [Tooltip("From the winning landing; it peaks before the panel opens at WinPopupDelay.")]
        public float WinDelay = 0.6f;
        [Tooltip("Flash behind the burst: size as a share of the board width, peak alpha and time.")]
        public float FlashSize = 1.1f;
        [Range(0f, 1f)] public float FlashAlpha = 0.8f;
        public float FlashTime = 0.35f;
        [Tooltip("Two orbit rings widen from this share of the board width to the next, the second a beat later.")]
        public float RingFrom = 0.2f;
        public float RingTo = 1.15f;
        public float RingTime = 0.5f;
        public float RingStagger = 0.08f;
        [Range(0.1f, 1f)] public float RingAspect = 0.3f;
        [Tooltip("Star dust and sparks thrown out from the centre.")]
        public int DustCount = 60;
        public Vector2 DustSpeed = new(3f, 8f);
        public int SparkCount = 24;
        public Vector2 SparkSpeed = new(10f, 16f);

        [Header("Loss")]
        [Tooltip("From the panel's first frame, like the panel's own entrance delay.")]
        public float LoseDelay = 0.1f;
        [Tooltip("One cool ring closes in from this share of the board width to the next and fades.")]
        public float LoseRingFrom = 1.1f;
        public float LoseRingTo = 0.35f;
        public float LoseRingTime = 0.4f;
        [Range(0f, 1f)] public float LoseRingAlpha = 0.9f;
        public Color LoseColor = new(0.55f, 0.5f, 0.95f, 1f);
        [Tooltip("Faint motes that sink across the board.")]
        public int MoteCount = 18;
    }
}
