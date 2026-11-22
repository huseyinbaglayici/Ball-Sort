using System;
using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Level won: caps pulse, an orbit ring collapses into a gold burst, the mascot swings in under the ribbon, then the win panel.
    public sealed class WinFx : Item
    {
        // Lifetime of the longest grain, so nothing is cut when the item is released.
        private const float Tail = 1.5f;
        // The dim reaches past the screen on every side.
        private const float DimMargin = 1.5f;
        // The mascot leans toward its heading by this share, at most this far, and catches up at this rate.
        private const float LeanShare = 0.25f;
        private const float LeanRate = 10f;
        private const int GhostSpacing = 2;
        // Glow behind the mascot and the ribbon's text, in the mascot's and ribbon's own units.
        private const float MascotGlowScale = 1.6f;
        private const float RibbonTextLift = 0.16f;

        [SerializeField] private SpriteRenderer _dim;
        [SerializeField] private SpriteRenderer _flash;
        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _glowNear;
        [SerializeField] private SpriteRenderer _glowFar;
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private SpriteRenderer _mascot;
        [SerializeField] private SpriteRenderer _mascotGlow;
        [SerializeField] private SpriteRenderer[] _ghosts;
        [SerializeField] private SpriteRenderer _ribbon;
        [SerializeField] private TMP_Text _ribbonText;

        private static Sprite _dimSprite;
        private readonly Vector3[] _history = new Vector3[16];
        private readonly float[] _historyAngle = new float[16];
        private int _historyCount;
        private Action<WinFx> _onFinished;
        private float _lean;
        private Vector3 _previous;
        private Vector3 _mascotRest;
        private float _mascotScale;
        // The screen in the board's units, so the stage reads the same on a narrow and a wide board.
        private float _view;
        private float _viewHeight;

        private void Awake()
        {
            if (_dimSprite == null)
                _dimSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0,
                    SpriteMeshType.FullRect);
            _dim.sprite = _dimSprite;
        }

        // From the last landing to the panel: the time the panel should wait for the ribbon to settle.
        public static float PanelAt(BallAnimationConfig c, int completedTubes)
        {
            var w = c.Win;
            var slam = c.CompleteChargeDuration + c.CapPopDuration + c.CapSlamDuration;
            var ring = slam + Mathf.Max(completedTubes - 1, 0) * w.CapStagger + w.RingDelay;
            var burst = ring + w.RingTime;
            var landed = burst + w.MascotDelay + w.MascotTime;
            return landed + w.RibbonDelay + w.RibbonTime;
        }

        // tubes: every tube on the board; origin: the one that completed last; width: the board's width in its own units.
        public void Play(IReadOnlyList<BottleItem> tubes, int origin, float width, float panelAt, BallAnimationConfig config,
            Action<WinFx> onFinished)
        {
            Stop();
            _onFinished = onFinished;
            var w = config.Win;
            var f = config.Finish;
            MeasureView(width);

            // Caps ripple out from the solved tube; its own slam is its pulse.
            var slam = config.CompleteChargeDuration + config.CapPopDuration + config.CapSlamDuration;
            var originX = origin >= 0 && origin < tubes.Count ? tubes[origin].transform.localPosition.x : 0f;
            var others = new List<BottleItem>();
            foreach (var tube in tubes)
                if (tube.Index != origin) others.Add(tube);
            others.Sort((a, b) => Mathf.Abs(a.transform.localPosition.x - originX)
                .CompareTo(Mathf.Abs(b.transform.localPosition.x - originX)));
            for (var i = 0; i < others.Count; i++)
                others[i].PulseCap(slam + (i + 1) * w.CapStagger, w.CapPulseScale, w.CapPulseTime);

            var ringAt = slam + others.Count * w.CapStagger + w.RingDelay;
            var burstAt = ringAt + w.RingTime;
            var mascotAt = burstAt + w.MascotDelay;
            var landedAt = mascotAt + w.MascotTime;
            var ribbonAt = landedAt + w.RibbonDelay;

            Sweep(w, f, ringAt);
            PrimeTween.Tween.Delay(this, burstAt, fx => fx.Burst(w, f));
            Dim(w, f, burstAt);
            Mascot(w, f, mascotAt);
            Ribbon(w, ribbonAt);

            var total = Mathf.Max(panelAt + w.AfterPanel, ribbonAt + w.RibbonTime + Tail);
            PrimeTween.Tween.Delay(this, total, fx => fx.Finish());
        }

        // Falls back to the board when there is no camera.
        private void MeasureView(float width)
        {
            var camera = Camera.main;
            _view = width;
            _viewHeight = width * 2f;
            if (camera == null) return;
            var scale = Mathf.Max(transform.lossyScale.x, 0.0001f);
            _viewHeight = camera.orthographicSize * 2f / scale;
            _view = _viewHeight * camera.aspect;
        }

        // One tilted orbit ring closes on the centre while it turns; its far half passes behind the tubes.
        private void Sweep(WinFxSettings w, FinishFxSettings f, float at)
        {
            var width = _view;
            var line = Color.Lerp(f.Gold, Color.white, 0.3f);
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(w.RingTime, 0.0001f), (fx, t) =>
                {
                    var close = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.InCubic);
                    var size = width * Mathf.Lerp(w.RingFrom, w.RingTo, close);
                    var alpha = Mathf.Clamp01(t * 6f);
                    var angle = Quaternion.Euler(0f, 0f, w.RingTurn * t);
                    fx._ringNear.transform.localRotation = fx._ringFar.transform.localRotation = angle;
                    fx._glowNear.transform.localRotation = fx._glowFar.transform.localRotation = angle;
                    fx.Show(fx._ringNear, line, alpha, size, size * w.RingAspect);
                    fx.Show(fx._ringFar, line, alpha * 0.7f, size, size * w.RingAspect);
                    fx.Show(fx._glowNear, f.Violet, alpha, size, size * w.RingAspect);
                    fx.Show(fx._glowFar, f.Violet, alpha * 0.7f, size, size * w.RingAspect);
                }, PrimeTween.Ease.Linear, startDelay: at)
                .OnComplete(this, fx => fx.HideRing());
        }

        // Gold flash where the ring collapsed, star dust in the palette thrown out and slowed, fast sparks ahead of it.
        private void Burst(WinFxSettings w, FinishFxSettings f)
        {
            var flashSize = _view * w.FlashSize;
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(w.FlashTime, 0.0001f), (fx, t) =>
                {
                    var a = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                    fx.Show(fx._flash, Color.Lerp(f.Gold, Color.white, 0.4f), a * w.FlashAlpha, flashSize, flashSize);
                }, PrimeTween.Ease.Linear)
                .OnComplete(this, fx => fx._flash.enabled = false);
            var scale = _view / 10f;
            for (var i = 0; i < w.DustCount; i++)
            {
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                var pick = UnityEngine.Random.value;
                var color = pick < 0.4f ? f.Gold : pick < 0.7f ? f.Core : f.Violet;
                _dust.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = new Vector3(direction.x, direction.y * 0.8f, 0f) * (UnityEngine.Random.Range(w.DustSpeed.x, w.DustSpeed.y) * scale),
                    startColor = color,
                    startSize = UnityEngine.Random.Range(0.25f, 0.5f),
                    startLifetime = UnityEngine.Random.Range(0.8f, 1.4f),
                    applyShapeToPosition = false,
                }, 1);
            }
            for (var i = 0; i < w.SparkCount; i++)
            {
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                _sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = (Vector3)direction * (UnityEngine.Random.Range(w.SparkSpeed.x, w.SparkSpeed.y) * scale),
                    startColor = Color.Lerp(f.Gold, Color.white, UnityEngine.Random.value),
                    startSize = 0.18f,
                    startLifetime = UnityEngine.Random.Range(0.3f, 0.5f),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        // Deep indigo over the whole screen behind the tubes, from the burst on.
        private void Dim(WinFxSettings w, FinishFxSettings f, float at)
        {
            if (w.DimAlpha <= 0f) return;
            var camera = Camera.main;
            var dim = _dim.transform;
            if (camera != null) dim.position = new Vector3(camera.transform.position.x, camera.transform.position.y, dim.position.z);
            dim.localScale = new Vector3(_view * DimMargin, _viewHeight * DimMargin, 1f);
            var color = f.Deep;
            PrimeTween.Tween.Custom(this, 0f, w.DimAlpha, Mathf.Max(w.DimTime, 0.0001f), (fx, a) =>
                {
                    fx._dim.enabled = true;
                    fx._dim.color = new Color(color.r, color.g, color.b, a);
                }, PrimeTween.Ease.OutQuad, startDelay: at);
        }

        // Swings in from below on an arc, leaning into the turn, lands with a squash and bobs while it waits.
        private void Mascot(WinFxSettings w, FinishFxSettings f, float at)
        {
            var native = Mathf.Max(_mascot.sprite != null ? _mascot.sprite.bounds.size.x : 1f, 0.0001f);
            _mascotScale = _view * w.MascotSize / native;
            _mascotRest = new Vector3(w.MascotRest.x * _view, w.MascotRest.y * _viewHeight, 0f);
            var from = new Vector3(w.MascotFrom.x * _view, w.MascotFrom.y * _viewHeight, 0f);
            // The arc's control point: out to the side and above the rest, so it swings in from over the shoulder.
            var control = new Vector3(from.x * 0.85f, _mascotRest.y + _viewHeight * 0.15f, 0f);
            _previous = from;
            _lean = 0f;
            _historyCount = 0;
            var glowColor = f.Violet;
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(w.MascotTime, 0.0001f), (fx, t) =>
                {
                    var k = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutCubic);
                    var u = 1f - k;
                    var position = u * u * from + 2f * u * k * control + k * k * fx._mascotRest;
                    fx.Fly(position, w, glowColor, t);
                }, PrimeTween.Ease.Linear, startDelay: at)
                .OnComplete(this, fx => fx.Land(w));
        }

        private void Fly(Vector3 position, WinFxSettings w, Color glow, float t)
        {
            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var velocity = (position - _previous) / dt;
            _previous = position;
            var heading = velocity.sqrMagnitude > 0.01f ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 90f;
            // Lean toward the heading, upright at the end.
            var target = Mathf.Clamp(Mathf.DeltaAngle(90f, heading) * LeanShare, -w.MascotLean, w.MascotLean) * (1f - t);
            _lean = Mathf.Lerp(_lean, target, 1f - Mathf.Exp(-LeanRate * dt));
            var mascot = _mascot.transform;
            mascot.localPosition = position;
            mascot.localRotation = Quaternion.Euler(0f, 0f, _lean);
            // Grows in from a little smaller, as if coming closer.
            mascot.localScale = Vector3.one * (_mascotScale * Mathf.Lerp(0.8f, 1f, t));
            _mascot.enabled = true;
            var glowRenderer = _mascotGlow;
            glowRenderer.enabled = true;
            glowRenderer.transform.localPosition = position;
            glowRenderer.transform.localScale = mascot.localScale * MascotGlowScale;
            glowRenderer.color = new Color(glow.r, glow.g, glow.b, 0.55f * Mathf.Clamp01(t * 4f));
            Ghosts(position, _lean, mascot.localScale, w, glow, 1f - t);
        }

        // Violet ghosts of the mascot trail its path and fade as it slows.
        private void Ghosts(Vector3 position, float angle, Vector3 scale, WinFxSettings w, Color glow, float strength)
        {
            if (_historyCount == 0 || Time.frameCount % GhostSpacing == 0)
            {
                for (var i = Mathf.Min(_historyCount, _history.Length - 1); i > 0; i--)
                {
                    _history[i] = _history[i - 1];
                    _historyAngle[i] = _historyAngle[i - 1];
                }
                _history[0] = position;
                _historyAngle[0] = angle;
                _historyCount = Mathf.Min(_historyCount + 1, _history.Length);
            }
            for (var i = 0; i < _ghosts.Length; i++)
            {
                var slot = (i + 1) * 2;
                var ghost = _ghosts[i];
                if (slot >= _historyCount)
                {
                    ghost.enabled = false;
                    continue;
                }
                ghost.enabled = true;
                ghost.transform.localPosition = _history[slot];
                ghost.transform.localRotation = Quaternion.Euler(0f, 0f, _historyAngle[slot]);
                ghost.transform.localScale = scale;
                var alpha = w.TrailAlpha * strength * (1f - (float)i / _ghosts.Length);
                ghost.color = new Color(glow.r, glow.g, glow.b, alpha);
            }
        }

        private void Land(WinFxSettings w)
        {
            foreach (var ghost in _ghosts) ghost.enabled = false;
            var mascot = _mascot.transform;
            mascot.localPosition = _mascotRest;
            mascot.localRotation = Quaternion.identity;
            var rest = Vector3.one * _mascotScale;
            var squash = new Vector3(rest.x * w.MascotSquash.x, rest.y * w.MascotSquash.y, 1f);
            PrimeTween.Tween.Scale(mascot, rest, squash, Mathf.Max(w.MascotSquashTime, 0.0001f), PrimeTween.Ease.OutQuad)
                .Chain(PrimeTween.Tween.Scale(mascot, squash, rest, Mathf.Max(w.MascotRecover, 0.0001f), PrimeTween.Ease.OutBack))
                .ChainCallback(this, fx => fx.Bob(w));
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(w.MascotRecover, 0.0001f), (fx, t) =>
                fx._mascotGlow.transform.localScale = Vector3.one * (fx._mascotScale * MascotGlowScale * Mathf.Lerp(1f, 1.15f, Mathf.Sin(Mathf.PI * t))),
                PrimeTween.Ease.Linear);
        }

        private void Bob(WinFxSettings w)
        {
            var period = Mathf.Max(w.MascotBobPeriod, 0.0001f);
            PrimeTween.Tween.Custom(this, 0f, 1f, period, (fx, t) =>
                {
                    var lift = w.MascotBob * fx._mascotScale * Mathf.Sin(Mathf.PI * 2f * t);
                    fx._mascot.transform.localPosition = fx._mascotRest + Vector3.up * lift;
                    fx._mascotGlow.transform.localPosition = fx._mascot.transform.localPosition;
                }, PrimeTween.Ease.Linear, cycles: -1);
        }

        // Drops in over the mascot from a little higher, overshooting into place; the text settles with it.
        private void Ribbon(WinFxSettings w, float at)
        {
            var native = _ribbon.sprite != null ? _ribbon.sprite.bounds.size : Vector3.one;
            var scale = _view * w.RibbonWidth / Mathf.Max(native.x, 0.0001f);
            var mascotTop = _mascotRest.y + (_mascot.sprite != null ? _mascot.sprite.bounds.extents.y : 0f) * _mascotScale;
            var rest = new Vector3(0f, mascotTop + _view * w.RibbonGap + native.y * scale * 0.5f, 0f);
            var drop = _view * w.RibbonDrop;
            _ribbonText.text = Services.TryGet<LocalizationService>(out var loc) ? loc.Get("result.win") : "Great!";
            _ribbonText.fontSize = w.RibbonFontSize;
            _ribbonText.transform.localPosition = new Vector3(0f, RibbonTextLift, 0f);
            var ribbon = _ribbon.transform;
            PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(w.RibbonTime, 0.0001f), (fx, t) =>
                {
                    var k = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutBack);
                    fx._ribbon.enabled = true;
                    fx._ribbonText.enabled = true;
                    ribbon.localPosition = rest + Vector3.up * (drop * (1f - k));
                    ribbon.localScale = Vector3.one * (scale * Mathf.LerpUnclamped(0.7f, 1f, k));
                    var alpha = Mathf.Clamp01(t * 3f);
                    fx._ribbon.color = new Color(1f, 1f, 1f, alpha);
                    fx._ribbonText.alpha = alpha;
                }, PrimeTween.Ease.Linear, startDelay: at);
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
            PrimeTween.Tween.StopAll(_mascot.transform);
            _dim.enabled = _flash.enabled = _mascot.enabled = _mascotGlow.enabled = _ribbon.enabled = false;
            _ribbonText.enabled = false;
            foreach (var ghost in _ghosts) ghost.enabled = false;
            HideRing();
            _dust.Clear();
            _sparks.Clear();
        }

        public override void OnDespawn()
        {
            Stop();
            _onFinished = null;
            base.OnDespawn();
        }
    }
}
