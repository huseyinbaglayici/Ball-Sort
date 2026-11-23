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
        [Tooltip("The frame holds still while the mascot's orb grows over it from the hand, then Home opens out of the middle.")]
        public float ZoomTime = 0.65f;
        public float RevealTime = 0.6f;
        [Tooltip("Orb light at its core, the flat tint it settles to, its halo and how much the frame darkens behind it.")]
        public Color OrbCore = new(0.95f, 0.55f, 1f, 1f);
        public Color ZoomTint = new(0.16f, 0.08f, 0.38f, 1f);
        public float OrbGlow = 0.6f;
        public float SceneDim = 0.35f;
        [Tooltip("Home starts this much zoomed in and settles as it opens.")]
        public float ClosePush = 0.03f;

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
        public float CloseTotal => ZoomTime + RevealTime;
    }
}
