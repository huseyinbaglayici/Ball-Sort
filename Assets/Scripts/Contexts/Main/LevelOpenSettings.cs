using UnityEngine;

namespace Project.Contexts.Main
{
    // Timing of the round-mask reveal between Home and a level. Seconds, unscaled.
    [CreateAssetMenu(menuName = "Project/Level Open Settings", fileName = "LevelOpenSettings")]
    public sealed class LevelOpenSettings : ScriptableObject
    {
        [Header("Open")]
        [Tooltip("The screen darkens around the orb, then the level shows through a hole growing out of it.")]
        public float DarkenTime = 0.25f;
        public float GrowTime = 0.6f;

        [Header("Close")]
        [Tooltip("The hole shrinks onto the orb, then the dark clears off Home.")]
        public float ShrinkTime = 0.5f;
        public float ClearTime = 0.25f;

        [Header("Edge")]
        [Tooltip("Soft edge as a share of the hole's radius, never thinner than this many units.")]
        public float FeatherShare = 0.35f;
        public float FeatherMin = 80f;
        public Color Dark = new(0.03f, 0.02f, 0.09f, 1f);

        public float OpenTotal => DarkenTime + GrowTime;
        public float CloseTotal => ShrinkTime + ClearTime;
    }
}
