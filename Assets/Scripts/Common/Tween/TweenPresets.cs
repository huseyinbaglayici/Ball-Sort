using PrimeTween;
using UnityEngine;

namespace Project.Common.Tween
{
    // All presets use unscaled time so they still run while the game is paused.
    public static class TweenPresets
    {
        public const float FadeDuration = 0.15f;

        // Popup open: after a short beat the panel grows in with a 3% overshoot while the dim fades up.
        public const float PopupDelay = 0.1f;
        public const float PopupScaleDuration = 0.3f;
        public const float PopupDimDuration = 0.4f;
        public const float PopupCloseDuration = 0.3f;

        private static readonly AnimationCurve PopupScaleCurve = new(
            new Keyframe(0f, 0f, 2f, 2f), new Keyframe(0.57f, 1.03f, 0.4f, 0.4f), new Keyframe(1f, 1f, 0.32f, 0.32f));

        public static PrimeTween.Tween PopupScaleIn(Transform target)
        {
            target.localScale = Vector3.zero;
            return PrimeTween.Tween.Custom(target, 0f, 1f, PopupScaleDuration,
                (t, x) => t.localScale = Vector3.one * PopupScaleCurve.Evaluate(x), Ease.Linear, startDelay: PopupDelay,
                useUnscaledTime: true);
        }

        public static PrimeTween.Tween PopupDimIn(CanvasGroup group)
        {
            group.alpha = 0f;
            return PrimeTween.Tween.Alpha(group, 0f, 1f, PopupDimDuration, Ease.OutQuad, startDelay: PopupDelay,
                useUnscaledTime: true);
        }

        public static PrimeTween.Tween PopupDimOut(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, group.alpha, 0f, PopupCloseDuration, Ease.OutQuad, useUnscaledTime: true);

        public static PrimeTween.Tween FadeIn(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 0f, 1f, FadeDuration, Ease.OutQuad, useUnscaledTime: true);

        public static PrimeTween.Tween FadeOut(CanvasGroup group) =>
            PrimeTween.Tween.Alpha(group, 1f, 0f, FadeDuration, Ease.InQuad, useUnscaledTime: true);

        public static PrimeTween.Tween Punch(Transform target, float strength = 0.15f) =>
            PrimeTween.Tween.PunchScale(target, Vector3.one * strength, 0.3f, useUnscaledTime: true);
    }
}
