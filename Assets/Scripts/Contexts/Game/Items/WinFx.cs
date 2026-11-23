using System;
using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using Project.Core.Localization;
using Project.Core.Services;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Level won: caps pulse, an orbit ring bursts, then the stage skeleton plays until the coins fly.
    public sealed class WinFx : Item
    {
        // Lifetime of the longest grain, so nothing is cut when the item is released.
        private const float Tail = 1.5f;
        // The dim reaches past the screen on every side.
        private const float DimMargin = 1.5f;
        // Skeleton units per source pixel and the mascot's drawn width, used to scale the stage to the screen.
        private const float SkeletonScale = 0.01f;
        private const float MascotPx = 512f;
        private const string IntroAnimation = "intro";
        private const string CelebrateAnimation = "celebrate";

        [SerializeField] private SpriteRenderer _dim;
        [SerializeField] private SpriteRenderer _flash;
        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _glowNear;
        [SerializeField] private SpriteRenderer _glowFar;
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private ParticleSystem _sparks;
        // Salvos after the ribbon's slam.
        [SerializeField] private ParticleSystem _comets;
        [SerializeField] private ParticleSystem _stars;
        [SerializeField] private ParticleSystem _orbSparks;
        [SerializeField] private ParticleSystem _rings;
        [SerializeField] private ParticleSystem _stardust;
        [SerializeField] private SkeletonAnimation _stage;
        [SerializeField] private MeshRenderer _stageRenderer;
        [SerializeField] private TMP_Text _ribbonText;
        [SerializeField] private TitleLetters _letters;

        private static Sprite _dimSprite;
        private Action<WinFx> _onFinished;
        private WinFxSettings _w;
        private readonly List<Color> _orbColors = new();
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

        // From the last landing to the coins: the time to wait for the ribbon and its last letter to settle.
        public static float PanelAt(BallAnimationConfig c, int completedTubes)
        {
            var w = c.Win;
            var slam = c.CompleteChargeDuration + c.CapPopDuration + c.CapSlamDuration;
            var ring = slam + Mathf.Max(completedTubes - 1, 0) * w.CapStagger + w.RingDelay;
            var burst = ring + w.RingTime;
            var landed = burst + w.MascotDelay + w.MascotTime;
            var ribbon = landed + w.RibbonDelay;
            return ribbon + w.RibbonTime + w.LetterDelay + w.LettersTime;
        }

        // tubes: all board tubes; origin: the last one completed; width: board width in its own units.
        public void Play(IReadOnlyList<BottleItem> tubes, int origin, float width, float panelAt, BallAnimationConfig config,
            Action<WinFx> onFinished)
        {
            Stop();
            _onFinished = onFinished;
            var w = config.Win;
            _w = w;
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
            var slamAt = ribbonAt + w.RibbonTime;
            var lettersAt = slamAt + w.LetterDelay;
            _orbColors.Clear();
            foreach (var tube in tubes)
                if (tube.BallCount > 0 && !_orbColors.Contains(tube.Balls[0].Color)) _orbColors.Add(tube.Balls[0].Color);

            Sweep(w, f, ringAt);
            PrimeTween.Tween.Delay(this, burstAt, fx => fx.Burst(w, f));
            Dim(w, f, burstAt);
            PrepareStage(w);
            PrimeTween.Tween.Delay(this, burstAt, fx => fx.PlayStage());
            PrimeTween.Tween.Delay(this, lettersAt, fx => fx._letters.Play(0f, w));
            for (var i = 0; i < 3; i++)
            {
                var salvo = i;
                PrimeTween.Tween.Delay(this, slamAt + w.SalvoGap * i, fx => fx.Salvo(salvo, w, f));
            }

            var total = Mathf.Max(panelAt + w.AfterPanel, Mathf.Max(lettersAt + w.LettersTime, slamAt + w.SalvoGap * 2f + 3f) + Tail);
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

        // Salvo 0: comets and stars from the right; 1: comets and sparks from the left; 2: rings and stardust.
        private void Salvo(int index, WinFxSettings w, FinishFxSettings f)
        {
            var scale = _view / 10f;
            var side = index == 0 ? 1f : -1f;
            if (index < 2)
            {
                for (var i = 0; i < w.CometCount; i++)
                {
                    var from = new Vector3(side * _view * UnityEngine.Random.Range(0.38f, 0.5f), -_viewHeight * UnityEngine.Random.Range(0.3f, 0.42f), 0f);
                    var velocity = new Vector3(-side * UnityEngine.Random.Range(3f, 5f), UnityEngine.Random.Range(14f, 18f), 0f) * scale;
                    Emit(_comets, from, velocity, i % 2 == 0 ? f.Gold : f.Core, UnityEngine.Random.Range(0.28f, 0.4f), UnityEngine.Random.Range(1.1f, 1.5f));
                }
            }
            if (index == 0)
            {
                for (var i = 0; i < w.StarCount; i++)
                    Emit(_stars, Spot(0.1f, 0.48f, 0.08f, 0.42f), Vector3.zero, i % 3 == 0 ? f.Gold : Color.white, UnityEngine.Random.Range(0.6f, 1f), UnityEngine.Random.Range(0.5f, 0.7f));
            }
            else if (index == 1)
            {
                for (var i = 0; i < w.OrbSparkCount; i++)
                {
                    var from = new Vector3(side * _view * 0.45f, _viewHeight * UnityEngine.Random.Range(0.05f, 0.15f), 0f);
                    var velocity = new Vector3(-side * UnityEngine.Random.Range(3f, 8f), UnityEngine.Random.Range(4f, 9f), 0f) * scale;
                    var color = _orbColors.Count > 0 ? _orbColors[i % _orbColors.Count] : f.Violet;
                    Emit(_orbSparks, from, velocity, Color.Lerp(color, Color.white, 0.1f), UnityEngine.Random.Range(0.2f, 0.34f), UnityEngine.Random.Range(1f, 1.5f));
                }
                for (var i = 0; i < w.StarCount; i++)
                    Emit(_stars, Spot(-0.48f, -0.1f, 0.08f, 0.42f), Vector3.zero, i % 3 == 0 ? f.Gold : Color.white, UnityEngine.Random.Range(0.6f, 1f), UnityEngine.Random.Range(0.5f, 0.7f));
            }
            else
            {
                for (var i = 0; i < w.RingCount; i++)
                    Emit(_rings, Spot(-0.4f, 0.4f, 0.3f, 0.46f), Vector3.zero, i % 2 == 0 ? f.Violet : f.Core, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.7f, 0.9f));
                for (var i = 0; i < w.StardustCount; i++)
                {
                    var from = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f) * _view, _viewHeight * UnityEngine.Random.Range(0.45f, 0.6f), 0f);
                    var velocity = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), -UnityEngine.Random.Range(1f, 2.2f), 0f) * scale;
                    var pick = UnityEngine.Random.value;
                    Emit(_stardust, from, velocity, pick < 0.5f ? f.Core : pick < 0.8f ? f.Violet : f.Gold, UnityEngine.Random.Range(0.08f, 0.16f), UnityEngine.Random.Range(2f, 3f));
                }
            }
        }

        // A random spot in a band of the screen, in shares of its width and height.
        private Vector3 Spot(float x0, float x1, float y0, float y1) =>
            new(_view * UnityEngine.Random.Range(x0, x1), _viewHeight * UnityEngine.Random.Range(y0, y1), 0f);

        private static void Emit(ParticleSystem system, Vector3 at, Vector3 velocity, Color color, float size, float life) =>
            system.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false,
            }, 1);

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

        // The stage scales so the mascot takes its share of screen width; the title follows the ribbon bone.
        private void PrepareStage(WinFxSettings w)
        {
            var scale = _view * w.MascotSize / (MascotPx * SkeletonScale);
            var stage = _stage.transform;
            stage.localPosition = Vector3.zero;
            stage.localRotation = Quaternion.identity;
            stage.localScale = Vector3.one * scale;
            _ribbonText.text = Services.TryGet<LocalizationService>(out var loc) ? loc.Get("result.win") : "Great!";
            _ribbonText.fontSize = w.RibbonFontSize;
            _ribbonText.alpha = 1f;
        }

        // Intro once, then the celebrate loop; both are authored in the skeleton from the same settings.
        private void PlayStage()
        {
            _stage.Initialize(false);
            var skeleton = _stage.Skeleton;
            if (skeleton == null) return;
            skeleton.SetupPose();
            var state = _stage.AnimationState;
            state.ClearTracks();
            state.SetAnimation(0, IntroAnimation, false);
            state.AddAnimation(0, CelebrateAnimation, true, 0f);
            _stage.Update(0f);
            _stageRenderer.enabled = true;
            // The letters are hidden until their own arrival; the bone follower keeps the title on the band.
            _letters.Play(float.MaxValue, _w);
            _ribbonText.enabled = true;
        }

        // The mascot's orb in world space while the stage shows: its centre and radius.
        public bool TryGetOrb(out Vector3 centre, out float radius)
        {
            centre = default;
            radius = 0f;
            var skeleton = _stageRenderer.enabled ? _stage.Skeleton : null;
            var slot = skeleton?.FindSlot("orb");
            if (slot == null || !(slot.AppliedPose.Attachment is Spine.RegionAttachment region)) return false;
            var bone = slot.Bone.AppliedPose;
            bone.LocalToWorld(region.X, region.Y, out var x, out var y);
            var stage = _stage.transform;
            centre = stage.TransformPoint(x, y, 0f);
            radius = region.Width * Mathf.Abs(region.ScaleX) * 0.5f * bone.WorldScaleX * Mathf.Abs(stage.lossyScale.x);
            return true;
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
            _dim.enabled = _flash.enabled = false;
            _stageRenderer.enabled = false;
            _ribbonText.enabled = false;
            _letters.Stop();
            if (_stage.AnimationState != null) _stage.AnimationState.ClearTracks();
            HideRing();
            _dust.Clear();
            _sparks.Clear();
            _comets.Clear();
            _stars.Clear();
            _orbSparks.Clear();
            _rings.Clear();
            _stardust.Clear();
        }

        public override void OnDespawn()
        {
            Stop();
            _onFinished = null;
            base.OnDespawn();
        }
    }
}
