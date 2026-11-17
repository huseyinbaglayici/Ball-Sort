using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// One ball's path through a pair of portals, measured as how far the ball's center is past the source ring's cut
    /// line (negative before it); out of the target portal the ball is as far below the target cut line. The path never
    /// stops, and the speed carries across each joint:
    /// <list type="bullet">
    /// <item>Approach: up the tube until the ball touches the ring's edge, speeding up from rest. Its time is what is
    /// left of the cross time after the pass, so a deep ball just travels faster.</item>
    /// <item>Pass: from touching the source ring to all out of the target ring, at one steady speed over about the
    /// portal pass time, so the share going into one ring is the share coming out of the other. (Slower when the ball
    /// starts already touching the ring, so it fills the cross time.)</item>
    /// <item>Fall: to the slot over the entry time, starting at the pass speed: a deep slot keeps speeding up, like
    /// gravity; a near one slows down but still hits with at least the exit-keep share of that speed.</item>
    /// </list>
    /// Approach and fall are cubics with both end speeds set, kept from overshooting.
    /// </summary>
    public readonly struct WarpPath
    {
        private readonly float _past0;
        private readonly float _touch;
        private readonly float _approach;
        private readonly float _radius;
        private readonly float _passSpeed;
        private readonly float _fall;
        private readonly float _fallStart;
        private readonly float _hitSpeed;

        public readonly float ApproachTime;
        public readonly float PassTime;
        public readonly float FallTime;

        /// <param name="past0">Where the ball starts, past the source cut line (usually negative: below it).</param>
        /// <param name="fall">Distance from all out of the target ring down to the slot.</param>
        public WarpPath(float past0, float radius, float fall, float entry, BallAnimationConfig config)
        {
            _past0 = past0;
            _radius = radius;
            _touch = Mathf.Max(past0, -radius);
            _approach = _touch - past0;
            var cross = Mathf.Max(config.WarpCross, 0.0001f);
            _passSpeed = Mathf.Min(2f * radius / Mathf.Max(config.PortalPass, 0.0001f),
                (3f * _approach + radius - _touch) / cross);
            PassTime = (radius - _touch) / _passSpeed;
            ApproachTime = Mathf.Max(cross - PassTime, 0f);
            _fall = Mathf.Max(fall, 0.0001f);
            FallTime = Mathf.Max(entry, 0.0001f);
            _fallStart = Mathf.Min(_passSpeed, 3f * _fall / FallTime);
            _hitSpeed = Mathf.Min(Mathf.Max(2f * _fall / FallTime - _passSpeed, Mathf.Clamp01(config.ExitKeep) * _passSpeed),
                3f * _fall / FallTime);
        }

        public float Duration => ApproachTime + PassTime + FallTime;

        /// <summary>Distance past the source cut line at <paramref name="time"/> seconds into the path.</summary>
        public float PastAt(float time)
        {
            if (time < ApproachTime)
                return _past0 + Hermite(time / Mathf.Max(ApproachTime, 0.0001f), 0f, _approach, _passSpeed * ApproachTime);
            if (time < ApproachTime + PassTime) return _touch + _passSpeed * (time - ApproachTime);
            var h = Mathf.Clamp01((time - ApproachTime - PassTime) / FallTime);
            return _radius + Hermite(h, _fallStart * FallTime, _fall, _hitSpeed * FallTime);
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
