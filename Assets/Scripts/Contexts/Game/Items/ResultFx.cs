using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Plays once over the board before the result panel; the far ring halves pass behind the tubes.
    public sealed class ResultFx : Item
    {
        // Lifetime of the longest grain, so nothing is cut when the item is released.
        private const float Tail = 1.5f;

        [SerializeField] private SpriteRenderer _flash;
        [SerializeField] private SpriteRenderer[] _ringsNear;
        [SerializeField] private SpriteRenderer[] _ringsFar;
        [SerializeField] private SpriteRenderer[] _glowsNear;
        [SerializeField] private SpriteRenderer[] _glowsFar;
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private ParticleSystem _motes;

        private Action<ResultFx> _onFinished;

        // width: the board's width in its own units, so the beat scales with the layout.
        public void Play(bool won, float width, BallAnimationConfig config, Action<ResultFx> onFinished)
        {
            Stop();
            _onFinished = onFinished;
            var s = config.Result;
            var f = config.Finish;
            if (won) PlayWin(s, f, width);
            else PlayLoss(s, width);
            var total = (won ? s.WinDelay + s.RingTime + s.RingStagger : s.LoseDelay + s.LoseRingTime) + Tail;
            PrimeTween.Tween.Delay(this, total, fx => fx.Finish());
        }

        private void PlayWin(ResultFxSettings s, FinishFxSettings f, float width)
        {
            PrimeTween.Tween.Delay(this, s.WinDelay, fx => fx.Burst(s, f, width));
            var flashSize = width * s.FlashSize;
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(s.FlashTime, 0.0001f), (fx, t) =>
                {
                    var a = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                    fx.Show(fx._flash, Color.Lerp(f.Gold, Color.white, 0.4f), a * s.FlashAlpha, flashSize, flashSize);
                }, PrimeTween.Ease.Linear, startDelay: s.WinDelay)
                .OnComplete(this, fx => fx._flash.enabled = false);
            for (var i = 0; i < _ringsNear.Length; i++)
            {
                var ring = i;
                var line = ring == 0 ? Color.Lerp(f.Gold, Color.white, 0.3f) : f.Core;
                PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(s.RingTime, 0.0001f), (fx, t) =>
                    {
                        var grow = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutCubic);
                        var size = width * Mathf.Lerp(s.RingFrom, s.RingTo, grow);
                        fx.Ring(ring, line, f.Violet, (1f - t) * Mathf.Clamp01(t * 8f), size, s.RingAspect);
                    }, PrimeTween.Ease.Linear, startDelay: s.WinDelay + s.RingStagger * ring)
                    .OnComplete(this, fx => fx.HideRing(ring));
            }
        }

        private void PlayLoss(ResultFxSettings s, float width)
        {
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(s.LoseRingTime, 0.0001f), (fx, t) =>
                {
                    var close = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutQuad);
                    var size = width * Mathf.Lerp(s.LoseRingFrom, s.LoseRingTo, close);
                    var alpha = s.LoseRingAlpha * Mathf.Clamp01(t * 5f) * (1f - t * t);
                    fx.Ring(0, s.LoseColor, s.LoseColor * 0.6f, alpha, size, s.RingAspect);
                }, PrimeTween.Ease.Linear, startDelay: s.LoseDelay)
                .OnComplete(this, fx => fx.HideRing(0));
            PrimeTween.Tween.Delay(this, s.LoseDelay, fx => fx.Sink(s, width));
        }

        // Star dust in the palette, thrown out and slowed; a few fast sparks ahead of it.
        private void Burst(ResultFxSettings s, FinishFxSettings f, float width)
        {
            var scale = width / 10f;
            for (var i = 0; i < s.DustCount; i++)
            {
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                var pick = UnityEngine.Random.value;
                var color = pick < 0.4f ? f.Gold : pick < 0.7f ? f.Core : f.Violet;
                _dust.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = new Vector3(direction.x, direction.y * 0.8f, 0f) * (UnityEngine.Random.Range(s.DustSpeed.x, s.DustSpeed.y) * scale),
                    startColor = color,
                    startSize = UnityEngine.Random.Range(0.25f, 0.5f),
                    startLifetime = UnityEngine.Random.Range(0.8f, 1.4f),
                    applyShapeToPosition = false,
                }, 1);
            }
            for (var i = 0; i < s.SparkCount; i++)
            {
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                _sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = (Vector3)direction * (UnityEngine.Random.Range(s.SparkSpeed.x, s.SparkSpeed.y) * scale),
                    startColor = Color.Lerp(f.Gold, Color.white, UnityEngine.Random.value),
                    startSize = 0.18f,
                    startLifetime = UnityEngine.Random.Range(0.3f, 0.5f),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        // Faint cool motes across the board, drifting down as they fade.
        private void Sink(ResultFxSettings s, float width)
        {
            var scale = width / 10f;
            for (var i = 0; i < s.MoteCount; i++)
            {
                var color = s.LoseColor;
                color.a = UnityEngine.Random.Range(0.5f, 0.85f);
                _motes.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f) * width, UnityEngine.Random.Range(-0.1f, 0.35f) * width, 0f),
                    velocity = new Vector3(0f, -UnityEngine.Random.Range(0.6f, 1.2f) * scale, 0f),
                    startColor = color,
                    startSize = UnityEngine.Random.Range(0.2f, 0.35f),
                    startLifetime = UnityEngine.Random.Range(0.8f, 1.2f),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        private void Ring(int index, Color line, Color glow, float alpha, float width, float aspect)
        {
            Show(_ringsNear[index], line, alpha, width, width * aspect);
            Show(_ringsFar[index], line, alpha * 0.7f, width, width * aspect);
            Show(_glowsNear[index], glow, alpha, width, width * aspect);
            Show(_glowsFar[index], glow, alpha * 0.7f, width, width * aspect);
        }

        private void HideRing(int index) =>
            _ringsNear[index].enabled = _ringsFar[index].enabled = _glowsNear[index].enabled = _glowsFar[index].enabled = false;

        private void Show(SpriteRenderer renderer, Color color, float alpha, float width, float height)
        {
            renderer.enabled = alpha > 0.001f;
            if (renderer.sprite == null) return;
            var native = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / native.x, height / native.y, 1f);
            renderer.color = new Color(color.r, color.g, color.b, color.a * alpha);
        }

        private void Finish()
        {
            var done = _onFinished;
            _onFinished = null;
            done?.Invoke(this);
        }

        private void Stop()
        {
            PrimeTween.Tween.StopAll(this);
            _flash.enabled = false;
            for (var i = 0; i < _ringsNear.Length; i++) HideRing(i);
            _dust.Clear();
            _sparks.Clear();
            _motes.Clear();
        }

        public override void OnDespawn()
        {
            Stop();
            _onFinished = null;
            base.OnDespawn();
        }
    }
}
