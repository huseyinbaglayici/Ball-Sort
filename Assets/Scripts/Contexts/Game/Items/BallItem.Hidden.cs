using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A hidden ball: the shell over its color, and the charge, crack and break that reveal it.
    public sealed partial class BallItem
    {
        // Used by Undo: the shell comes back at once. Only question mark balls can hide.
        public void Hide()
        {
            if (IsHidden || _shell == null) return;
            IsHidden = true;
            StopReveal();
            _shellShown = true;
            _highlight.SetTint(Color.white);
            ApplyLook();
        }

        // The shell trembles, squashes as a ring snaps in, cracks with light and breaks; the real orb pops in under the shards.
        public void Reveal(OrbSkin orb, Color color, BallAnimationConfig config)
        {
            if (!IsHidden) return;
            StopReveal();
            IsHidden = false;
            Color = color;
            SetOrb(orb);
            var shake = config.HiddenShakeDuration;
            var crackAt = Mathf.Max(shake - config.HiddenCrackTime, 0f);
            var chargeAt = Mathf.Max(crackAt - config.HiddenChargeTime, 0f);
            var orbAt = shake + config.HiddenOrbDelay;
            _reveal = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(chargeAt, 0.0001f), (ball, k) => ball.TrembleShell(k, config)))
                .Chain(PrimeTween.Tween.Custom(this, chargeAt, shake, Mathf.Max(shake - chargeAt, 0.0001f),
                    (ball, t) => ball.ChargeShell(t - chargeAt, t - crackAt, config)))
                .ChainCallback(this, ball => ball.BreakShell(color, config))
                .Insert(shake, PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenBreakDuration, (ball, k) => ball._revealFx.Halves(config, k)))
                .Insert(shake, PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenHorizonTime, (ball, k) => ball._revealFx.Hit(config, k)))
                .Insert(orbAt, PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenOrbDuration,
                    (ball, k) => ball.SetPartsScale(Mathf.LerpUnclamped(config.HiddenOrbFrom, 1f, BackOut(k, config.HiddenOrbOvershoot)))))
                .Insert(orbAt, PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenFlareTime,
                    (ball, k) => ShellBreakFx.Flare(ball._core, k < 0.2f ? k / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.2f) / 0.8f))))
                .Insert(orbAt + config.HiddenOrbDuration * config.HiddenSparkAt, PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenSparkTime,
                    (ball, k) => ball._revealFx.Spark(config, k)))
                .ChainCallback(this, ball => ball.EndReveal());
        }

        // Charge then crack: times since each began.
        private void ChargeShell(float sinceCharge, float sinceCrack, BallAnimationConfig config)
        {
            var charge = Mathf.Clamp01(sinceCharge / Mathf.Max(config.HiddenChargeTime, 0.0001f));
            SquashShell(charge, config);
            _revealFx.Charge(config, charge);
            var crack = Mathf.Clamp01(sinceCrack / Mathf.Max(config.HiddenCrackTime, 0.0001f));
            ShellBreakFx.Crack(_shell, 1f - (1f - crack) * (1f - crack) * (1f - crack));
        }

        // Ease out with a settable overshoot.
        private static float BackOut(float k, float overshoot)
        {
            var t = k - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        // Grows harder toward the end.
        private void TrembleShell(float k, BallAnimationConfig config)
        {
            var swing = Mathf.Sin(k * TrembleCycles * Mathf.PI * 2f);
            var t = _shell.transform;
            t.localScale = Vector3.one * Mathf.Lerp(1f, config.HiddenShakeScale, k);
            t.localRotation = Quaternion.Euler(0f, 0f, config.HiddenShakeDegrees * k * swing);
            t.localPosition = new Vector3(config.HiddenShakeShift * k * Mathf.Sin(k * TrembleCycles * Mathf.PI * 3f),
                0f, t.localPosition.z);
        }

        // Still and pressed flat from the tremble's peak size, bottom kept.
        private void SquashShell(float k, BallAnimationConfig config)
        {
            var t = _shell.transform;
            var squash = Vector2.Lerp(Vector2.one, config.HiddenSquash, k * k) * config.HiddenShakeScale;
            t.localScale = new Vector3(squash.x, squash.y, 1f);
            t.localRotation = Quaternion.identity;
            t.localPosition = new Vector3(0f, (squash.y - 1f) * TubeArt.BallDiameter * 0.5f, t.localPosition.z);
        }

        private void BreakShell(Color color, BallAnimationConfig config)
        {
            ResetShell();
            _shellShown = false;
            _revealFx.Break();
            SetPartsScale(config.HiddenOrbFrom);
            _highlight.SetTint(color);
            ApplyLook();
        }

        private void EndReveal()
        {
            if (_revealFx != null) _revealFx.Hide();
            ShellBreakFx.Flare(_core, 0f);
            SetPartsScale(1f);
        }

        // Drops any pending reveal step without finishing it.
        private void StopReveal()
        {
            if (_reveal.isAlive)
            {
                _reveal.Stop();
                SetPartsScale(1f);
                ShellBreakFx.Flare(_core, 0f);
            }
            transform.localRotation = Quaternion.identity;
            if (_shell == null) return;
            ResetShell();
            _revealFx.Hide();
            _shellShown = IsHidden;
            ApplyLook();
        }

        private void ResetShell()
        {
            var t = _shell.transform;
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            t.localPosition = new Vector3(0f, 0f, t.localPosition.z);
            ShellBreakFx.Crack(_shell, 0f);
        }
    }
}
