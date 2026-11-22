using PrimeTween;
using UnityEngine;

namespace Project.Contexts.Main
{
    // Timing of the level-to-level swipe. Seconds, game time.
    [CreateAssetMenu(menuName = "Project/Level Swipe Settings", fileName = "LevelSwipeSettings")]
    public sealed class LevelSwipeSettings : ScriptableObject
    {
        [Tooltip("The finished board speeds off to the left.")]
        public float OutTime = 0.28f;
        public Ease OutEase = Ease.InQuad;

        [Tooltip("The next board comes in from the right and settles into its seat; taps wait for it.")]
        public float InTime = 0.38f;
        public Ease InEase = Ease.OutCubic;

        [Tooltip("Travel as a share of the screen width, so the board leaves the screen whole.")]
        public float Distance = 1.1f;

        public float Total => OutTime + InTime;
    }
}
