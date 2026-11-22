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
        [Tooltip("The won level shrinks into the orb with Home around it, then fades into the orb.")]
        public float ShrinkTime = 0.55f;
        public float ClearTime = 0.2f;
        [Tooltip("Home starts this much zoomed in about the orb and settles over the whole close.")]
        public float ClosePush = 0.15f;

        [Header("Edge")]
        [Tooltip("Soft edge as a share of the hole's radius, never thinner than this many units.")]
        public float FeatherShare = 0.35f;
        public float FeatherMin = 80f;
        public Color Dark = new(0.03f, 0.02f, 0.09f, 1f);
        [Tooltip("Thin rim where the soft edge starts: violet inside to white-cyan outside, in units, low alpha.")]
        public Color RimInner = new(0.55f, 0.35f, 1f, 1f);
        public Color RimOuter = new(0.75f, 0.97f, 1f, 1f);
        public float RimWidth = 3f;
        public float RimAlpha = 0.35f;

        [Header("Feel")]
        [Tooltip("Home content pushes in by this share as the hole grows, and pulls back as the dark clears.")]
        public float PushScale = 0.04f;
        [Tooltip("The clear inside is dimmed this much when the hole starts to move, recovering as it settles.")]
        public float Dim = 0.12f;

        public float OpenTotal => DarkenTime + GrowTime;
        public float CloseTotal => ShrinkTime + ClearTime;
    }
}
