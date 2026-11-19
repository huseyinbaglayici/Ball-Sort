using PrimeTween;
using UnityEngine;

namespace Project.Common.Tween
{
    // All presets use unscaled time so they still run while the game is paused.
    public static class TweenPresets
    {
        public const float PopDuration = 0.15f;
        public const float FadeDuration = 0.15f;

        private static readonly AnimationCurve PopCurve = new(
            new Keyframe(0f, 0.95f), new Keyframe(0.452f, 1.035f), new Keyframe(1f, 1f));

        public static PrimeTween.Tween PopIn(Transform target)
        {
            target.localScale = Vector3.one * PopCurve.Evaluate(0f);
            return PrimeTween.Tween.Custom(target, 0f, 1f, PopDuration,
                (t, x) => t.localScale = Vector3.one * PopCurve.Evaluate(x), Ease.Linear, useUnscaledTime: true);
        }

        public static PrimeTween.Tween PopOut(Transform target) =>
            PrimeTween.Tween.Scale(target, Vector3.one, Vector3.one * 0.95f, PopDuration, Ease.InQuad, useUnscaledTime: true);

        public static PrimeTween.Tween FadeIn(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 0f, 1f, FadeDuration, Ease.OutQuad, useUnscaledTime: true);

        public static PrimeTween.Tween FadeOut(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 1f, 0f, FadeDuration, Ease.InQuad, useUnscaledTime: true);

        public static PrimeTween.Tween Press(Transform target) =>
            PrimeTween.Tween.Scale(target, Vector3.one * 0.95f, 0.07f, Ease.OutQuad, useUnscaledTime: true);

        public static PrimeTween.Tween Release(Transform target) =>
            PrimeTween.Tween.Scale(target, Vector3.one, 0.15f, Ease.OutBack, useUnscaledTime: true);

        public static PrimeTween.Tween Punch(Transform target, float strength = 0.15f) =>
            PrimeTween.Tween.PunchScale(target, Vector3.one * strength, 0.3f, useUnscaledTime: true);
    }
}
