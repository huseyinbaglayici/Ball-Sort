using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Plays once over the board as the fail panel opens; the far ring halves pass behind the tubes.
    public sealed class ResultFx : Item
    {
        // Lifetime of the longest mote, so nothing is cut when the item is released.
        private const float Tail = 1.5f;

        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _glowNear;
        [SerializeField] private SpriteRenderer _glowFar;
        [SerializeField] private ParticleSystem _motes;

        private Action<ResultFx> _onFinished;

        // width: the board's width in its own units, so the beat scales with the layout.
        public void Play(float width, BallAnimationConfig config, Action<ResultFx> onFinished)
        {
            Stop();
            _onFinished = onFinished;
            var s = config.Result;
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(s.LoseRingTime, 0.0001f), (fx, t) =>
                {
                    var close = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutQuad);
                    var size = width * Mathf.Lerp(s.LoseRingFrom, s.LoseRingTo, close);
                    var alpha = s.LoseRingAlpha * Mathf.Clamp01(t * 5f) * (1f - t * t);
                    fx.Ring(s.LoseColor, s.LoseColor * 0.6f, alpha, size, s.RingAspect);
                }, PrimeTween.Ease.Linear, startDelay: s.LoseDelay)
                .OnComplete(this, fx => fx.HideRing());
            PrimeTween.Tween.Delay(this, s.LoseDelay, fx => fx.Sink(s, width));
            PrimeTween.Tween.Delay(this, s.LoseDelay + s.LoseRingTime + Tail, fx => fx.Finish());
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

        private void Ring(Color line, Color glow, float alpha, float width, float aspect)
        {
            Show(_ringNear, line, alpha, width, width * aspect);
            Show(_ringFar, line, alpha * 0.7f, width, width * aspect);
            Show(_glowNear, glow, alpha, width, width * aspect);
            Show(_glowFar, glow, alpha * 0.7f, width, width * aspect);
        }

        private void HideRing() => _ringNear.enabled = _ringFar.enabled = _glowNear.enabled = _glowFar.enabled = false;

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
            HideRing();
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
