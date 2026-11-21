using PrimeTween;
using UnityEngine;

namespace Project.Common.Tween
{
    // All presets use unscaled time so they still run while the game is paused.
    public static class TweenPresets
    {
        public const float FadeDuration = 0.15f;

        // Panel settle: starts 5% large and lands on 1 with a small dip.
        public const float SettleDuration = 0.15f;
        private const float SettleFrom = 1.05f;
        private static readonly AnimationCurve SettleCurve = new(
            new Keyframe(0f, 0f, 2f, 2f), new Keyframe(0.247f, 1.453f, 2.76f, 2.76f), new Keyframe(1f, 1f, -1.15f, -1.15f));

        // Burst: grows from 0.3 past 1.15 and back to 1 in a third of a second.
        public const float BurstDuration = 0.3333f;
        private static readonly AnimationCurve BurstCurve = new(
            new Keyframe(0f, 0.3f, 0f, 0f), new Keyframe(0.25f, 1.15f, 0f, 0f), new Keyframe(BurstDuration, 1f, 0f, 0f));

        public static PrimeTween.Tween PopupSettle(Transform target, float delay = 0f)
        {
            target.localScale = Vector3.one * SettleFrom;
            return PrimeTween.Tween.Custom(target, 0f, 1f, SettleDuration,
                (t, x) => t.localScale = Vector3.one * Mathf.LerpUnclamped(SettleFrom, 1f, SettleCurve.Evaluate(x)),
                Ease.Linear, startDelay: delay, useUnscaledTime: true);
        }

        public static PrimeTween.Tween PopupBurst(Transform target)
        {
            target.localScale = Vector3.one * BurstCurve.Evaluate(0f);
            return PrimeTween.Tween.Custom(target, 0f, BurstDuration, BurstDuration,
                (t, x) => t.localScale = Vector3.one * BurstCurve.Evaluate(x), Ease.Linear, useUnscaledTime: true);
        }

        public static PrimeTween.Tween FadeIn(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 0f, 1f, FadeDuration, Ease.OutQuad, useUnscaledTime: true);

        public static PrimeTween.Tween FadeOut(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 1f, 0f, FadeDuration, Ease.InQuad, useUnscaledTime: true);

        public static PrimeTween.Tween Punch(Transform target, float strength = 0.15f) =>
            PrimeTween.Tween.PunchScale(target, Vector3.one * strength, 0.3f, useUnscaledTime: true);
    }
}
