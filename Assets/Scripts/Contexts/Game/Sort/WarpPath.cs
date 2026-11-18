using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// One ball's path through a pair of portals, measured as how far the ball's center is past the source ring's cut
    /// line (negative before it); out of the target portal the ball is as far below the target cut line. The speed carries
    /// across the joint:
    /// <list type="bullet">
    /// <item>Toward: from where the ball is when the move starts (the selection lift frozen there, no anticipation) until
    /// it is all in the source ring, so all out of the target ring, over <see cref="BallAnimationConfig.WarpCross"/>. It
    /// follows an ease-in, 0.121t³ + 0.576t² + 0.303t: it sets off at once, at 0.3 times its
    /// average speed, and is pulled faster and faster, to 1.8 times it.</item>
    /// <item>Fall: to the slot over the entry time, starting at the speed it came out with: a deep slot keeps speeding up,
    /// like gravity; a near one slows down but still hits with at least the exit-keep share of that speed. A cubic with
    /// both end speeds set, kept from overshooting.</item>
    /// </list>
    /// </summary>
    public readonly struct WarpPath
    {
        // The toward curve and its slope at the end.
        private const float TowardA = 0.121f;
        private const float TowardB = 0.576f;
        private const float TowardC = 0.303f;
        private const float TowardEndSlope = 3f * TowardA + 2f * TowardB + TowardC;

        private readonly float _past0;
        private readonly float _toward;
        private readonly float _radius;
        private readonly float _fall;
        private readonly float _fallStart;
        private readonly float _hitSpeed;

        public readonly float TowardTime;
        public readonly float FallTime;

        /// <param name="past0">Where the ball starts, past the source cut line (usually negative: below it).</param>
        /// <param name="fall">Distance from all out of the target ring down to the slot.</param>
        public WarpPath(float past0, float radius, float fall, float entry, BallAnimationConfig config)
        {
            _past0 = past0;
            _radius = radius;
            _toward = Mathf.Max(radius - past0, 0f);
            TowardTime = Mathf.Max(config.WarpCross, 0.0001f);
            var exitSpeed = _toward * TowardEndSlope / TowardTime;
            _fall = Mathf.Max(fall, 0.0001f);
            FallTime = Mathf.Max(entry, 0.0001f);
            _fallStart = Mathf.Min(exitSpeed, 3f * _fall / FallTime);
            _hitSpeed = Mathf.Min(Mathf.Max(2f * _fall / FallTime - _fallStart, Mathf.Clamp01(config.ExitKeep) * _fallStart),
                3f * _fall / FallTime);
        }

        public float Duration => TowardTime + FallTime;

        /// <summary>Distance past the source cut line at <paramref name="time"/> seconds into the path.</summary>
        public float PastAt(float time)
        {
            if (time < TowardTime)
            {
                var t = time / TowardTime;
                return _past0 + _toward * (((TowardA * t + TowardB) * t + TowardC) * t);
            }
            var h = Mathf.Clamp01((time - TowardTime) / FallTime);
            return Mathf.Max(_past0, _radius) + Hermite(h, _fallStart * FallTime, _fall, _hitSpeed * FallTime);
        }

        /// <summary>
        /// Cubic from 0 to <paramref name="distance"/> over h in 0..1, with the start and end speeds given as distance per
        /// whole span (speed times duration).
        /// </summary>
        private static float Hermite(float h, float startSpeed, float distance, float endSpeed)
        {
            var h2 = h * h;
            var h3 = h2 * h;
            return (h3 - 2f * h2 + h) * startSpeed + (-2f * h3 + 3f * h2) * distance + (h3 - h2) * endSpeed;
        }
    }
}
