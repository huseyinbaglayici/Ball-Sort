using System;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // The beat as the fail panel opens: a small contraction over the board.
    [Serializable]
    public sealed class ResultFxSettings
    {
        [Tooltip("From the panel's first frame, like the panel's own entrance delay.")]
        public float LoseDelay = 0.1f;
        [Tooltip("One cool ring closes in from this share of the board width to the next and fades.")]
        public float LoseRingFrom = 1.1f;
        public float LoseRingTo = 0.35f;
        public float LoseRingTime = 0.4f;
        [Range(0.1f, 1f)] public float RingAspect = 0.3f;
        [Range(0f, 1f)] public float LoseRingAlpha = 0.9f;
        public Color LoseColor = new(0.55f, 0.5f, 0.95f, 1f);
        [Tooltip("Faint motes that sink across the board.")]
        public int MoteCount = 18;
    }
}
