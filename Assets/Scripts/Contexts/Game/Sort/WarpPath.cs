using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Distance past the source cut equals distance below the target cut; speed is continuous across both phases.
    public readonly struct WarpPath
    {
        // Ease-in: starts at 0.3x the average speed and ends at 1.8x (the end slope).
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

        // fall: distance from fully out of the target ring down to the slot.
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

        // Speeds are given per whole span (speed times duration).
        private static float Hermite(float h, float startSpeed, float distance, float endSpeed)
        {
            var h2 = h * h;
            var h3 = h2 * h;
            return (h3 - 2f * h2 + h) * startSpeed + (-2f * h3 + 3f * h2) * distance + (h3 - h2) * endSpeed;
        }
    }
}
