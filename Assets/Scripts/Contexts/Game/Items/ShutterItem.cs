using System;
using Project.Contexts.Game.Sort;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Contexts.Game.Items
{
    // One shutter over a row of adjacent tubes; parented to the board, it follows the end tubes in its own sorting group.
    [RequireComponent(typeof(SortingGroup))]
    public sealed class ShutterItem : Item
    {
        // Rows of the art, from its top: padlock center and the line the curtain rolls up behind.
        private const float LockYPx = 540f;
        private const float RollLinePx = 148f;
        // Swing pivot, the top of the shackle, above the padlock's center.
        private const float ShacklePx = 114f;
        // The mask reaches this far below the art so a tall tube's curtain stays inside it.
        private const float MaskDepth = 40f;

        [SerializeField] private SpriteRenderer _curtain;
        [SerializeField] private SpriteRenderer _frame;
        [SerializeField] private SpriteRenderer _housing;
        // Swings and drops; holds the padlock's base and its tinted body.
        [SerializeField] private Transform _padlock;
        [SerializeField] private SpriteRenderer _padlockBase;
        [SerializeField] private SpriteRenderer _padlockBody;
        // A dark plate and a soft gold glow behind the padlock, so it reads against the curtain.
        [SerializeField] private SpriteRenderer _lockShade;
        [SerializeField] private SpriteRenderer _lockGlow;
        [SerializeField] private SpriteMask _mask;
        // Open FX: orbit ring halves and glows round the lock, the seam and its spill, the rim glow and the sparks.
        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _glowNear;
        [SerializeField] private SpriteRenderer _glowFar;
        [SerializeField] private SpriteRenderer _seam;
        [SerializeField] private SpriteRenderer _spill;
        [SerializeField] private SpriteRenderer _rimGlow;
        [SerializeField] private ParticleSystem _sparks;
        // Charge and hit at the lock: soft core glow, hot core, photon ring and two orbiting beads; a glow under the seam.
        [SerializeField] private SpriteRenderer _coreGlow;
        [SerializeField] private SpriteRenderer _coreHot;
        [SerializeField] private SpriteRenderer _photon;
        [SerializeField] private SpriteRenderer[] _beads;
        [SerializeField] private SpriteRenderer _seamGlow;

        private static Sprite _maskSprite;
        private Transform _first;
        private Transform _last;
        private float _rise;
        private Vector2 _curtainSize;
        private Vector2 _frameSize;
        private Vector2 _housingSize;
        private PrimeTween.Sequence _play;
        private PrimeTween.Tween _rattle;
        private Vector3 _rattleOffset;
        private float _width;
        // Curtain edge when the roll starts: the spill never reaches below it.
        private float _seamBase;
        // Where the lock let go; the horizon ring stays there while the padlock drops.
        private Vector3 _releasedAt;
        // Ring flattening, like the tube base in perspective.
        private const float RingAspect = 0.32f;
        // The open's rings stay soft so the key's insertion leads.
        private const float SoftRing = 0.6f;
        // The padlock is drawn this much larger than its art; its glow breathes over this period.
        private const float LockScale = 1.15f;
        private const float GlowPeriod = 2.4f;
        private static readonly Color ShadeColor = new(0.05f, 0.03f, 0.18f, 0.8f);
        private static readonly Color GlowColor = new(1f, 0.8f, 0.35f, 0.45f);
        // Bead orders in the shutter group: in front of the padlock (3, 4) on the near half, behind it on the far half.
        private const int BeadNearOrder = 7;
        private const int BeadFarOrder = 2;

        public bool IsPlaying => _play.isAlive;

        // The keyhole, where a flying key goes in.
        public Vector3 LockWorld => _padlockBody.bounds.center;
        public float LockWorldWidth => _padlockBody.bounds.size.x;

        private void Awake()
        {
            _curtainSize = _curtain.size;
            _frameSize = _frame.size;
            _housingSize = _housing.size;
            if (_maskSprite == null)
                _maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 1f), 4f, 0,
                    SpriteMeshType.FullRect);
            _mask.sprite = _maskSprite;
        }

        // first and last are the end tubes' art roots; rise is the tubes' extra height. color tints the padlock.
        public void Setup(Transform first, Transform last, float rise, Color color)
        {
            Stop();
            _first = first;
            _last = last;
            _rise = rise;
            _padlockBody.color = color;
            Follow();
        }

        private void LateUpdate()
        {
            Follow();
            if (!_play.isAlive) LockLight(1f);
        }

        // Plate and glow at the given strength; the glow shimmers slowly at rest.
        private void LockLight(float alpha)
        {
            _lockShade.color = new Color(ShadeColor.r, ShadeColor.g, ShadeColor.b, ShadeColor.a * alpha);
            var breath = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.PI * 2f / GlowPeriod);
            _lockGlow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * alpha * breath);
        }

        // Spans both end tubes with the art's width over each; the housing keeps its height, the rest stretches.
        private void Follow()
        {
            if (_first == null || _last == null || transform.parent == null) return;
            var parent = transform.parent;
            var a = parent.InverseTransformPoint(_first.position);
            var b = parent.InverseTransformPoint(_last.position);
            transform.localPosition = (a + b) * 0.5f + _rattleOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            var width = Mathf.Abs(b.x - a.x) + TubeArt.ArtWidthPx / TubeArt.PixelsPerUnit;
            _width = width;
            var height = TubeArt.ArtHeightPx / TubeArt.PixelsPerUnit;
            _frame.size = new Vector2(width, _frameSize.y + _rise);
            _frame.transform.localPosition = new Vector3(0f, _rise * 0.5f, 0f);
            _curtain.size = new Vector2(width, _curtainSize.y + _rise);
            if (!_play.isAlive) _curtain.transform.localPosition = new Vector3(0f, _rise * 0.5f, 0f);
            _housing.size = new Vector2(width, _housingSize.y);
            _housing.transform.localPosition = new Vector3(0f, _rise, 0f);
            var line = TubeArt.ArtY(RollLinePx) + _rise;
            _mask.transform.localPosition = new Vector3(0f, line, 0f);
            _mask.transform.localScale = new Vector3(width, line + height * 0.5f + MaskDepth / TubeArt.PixelsPerUnit, 1f);
            if (!_play.isAlive) PlacePadlock(Vector3.zero, 0f);
        }

        private void PlacePadlock(Vector3 offset, float degrees)
        {
            _padlock.localPosition = new Vector3(0f, TubeArt.ArtY(LockYPx - ShacklePx) + _rise * 0.5f, 0f) + offset;
            _padlock.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // Tapped while locked: a short tick sideways.
        public void Rattle(BallAnimationConfig config)
        {
            if (_play.isAlive || _rattle.isAlive) return;
            var amount = config.ShutterRattle * 2f;
            _rattle = PrimeTween.Tween.Custom(this, 0f, 1f, 0.24f, (shutter, k) =>
                    shutter._rattleOffset = Vector3.right * (amount * (1f - k) * Mathf.Sin(k * Mathf.PI * 6f)))
                .OnComplete(this, shutter => shutter._rattleOffset = Vector3.zero);
        }

        // Shake, pause, then padlock drop and roll together; onClear when the curtain is up, onDone after the fade.
        public void PlayOpen(BallAnimationConfig config, Action onClear, Action onDone)
        {
            Stop();
            Follow();
            _seamBase = transform.InverseTransformPoint(_curtain.bounds.min).y;
            _releasedAt = LockCenter();
            var shake = config.ShutterShakeDuration;
            var rollAt = shake + config.ShutterRollDelay;
            var clearAt = config.ShutterClearTime;
            var curtainY = _rise * 0.5f;
            _play = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, shake, (shutter, k) =>
                {
                    var swing = Mathf.Sin(k * config.ShutterShakeCycles * Mathf.PI * 2f);
                    shutter.PlacePadlock(Vector3.zero, config.ShutterShakeDegrees * k * swing);
                    shutter._rattleOffset = Vector3.right * (config.ShutterRattle *
                        Mathf.Sin(k * config.ShutterShakeCycles * Mathf.PI * 4f));
                }))
                .ChainCallback(this, shutter => shutter._rattleOffset = Vector3.zero)
                .Insert(rollAt, PrimeTween.Tween.Custom(this, 0f, 1f, config.LockDropDuration, (shutter, k) =>
                {
                    shutter.PlacePadlock(Vector3.down * (config.LockDropDistance * k * k), config.LockDropDegrees * k);
                    var a = 1f - k;
                    shutter._padlockBase.color = new Color(1f, 1f, 1f, a);
                    var c = shutter._padlockBody.color;
                    shutter._padlockBody.color = new Color(c.r, c.g, c.b, a);
                    shutter.LockLight(a);
                }))
                .Insert(rollAt, PrimeTween.Tween.Custom(this, 0f, config.ShutterRollDistance, config.ShutterRollDuration,
                    (shutter, y) => shutter._curtain.transform.localPosition = new Vector3(0f, curtainY + y, 0f),
                    PrimeTween.Ease.InOutSine))
                .Insert(0f, PrimeTween.Tween.Custom(this, 0f, 1f, rollAt, (shutter, k) => shutter.Charge(config, k)))
                .InsertCallback(rollAt, this, shutter => shutter.Release(config))
                .Insert(rollAt, PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(config.ShutterFxHorizonTime, 0.0001f),
                    (shutter, k) => shutter.Hit(config, k)))
                .Insert(rollAt, PrimeTween.Tween.Custom(this, 0f, 1f, config.ShutterRollDuration,
                    (shutter, k) => shutter.Seam(config, k)))
                .InsertCallback(clearAt, () => onClear?.Invoke())
                .Insert(clearAt, PrimeTween.Tween.Custom(this, 1f, 0f, config.ShutterFadeDuration, (shutter, a) =>
                {
                    shutter._frame.color = new Color(1f, 1f, 1f, a);
                    shutter._housing.color = new Color(1f, 1f, 1f, a);
                }))
                // The tubes are usable at clearAt; only the glow lingers past it.
                .Insert(clearAt, PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(config.ShutterFxLinger, 0.0001f),
                    (shutter, k) => shutter.Linger(config, k)))
                .ChainCallback(() => onDone?.Invoke());
        }

        // Charge: an orbit ring spins in round the padlock with two beads, its core brightens and the lock squashes.
        private void Charge(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var lockWidth = LockWidth();
            var center = LockCenter();
            var size = lockWidth * Mathf.Lerp(config.ShutterFxSnapFrom, config.ShutterFxSnapTo, k * k);
            var alpha = Mathf.Clamp01(k * 4f);
            Ring(f.Core, f.Violet, alpha * SoftRing, size, center);
            Orbit(f, k, Mathf.Min(size, _width * 1.05f), center, alpha * SoftRing);
            var heat = k * k;
            Stretch(_coreGlow, center, lockWidth * Mathf.Lerp(1.6f, 1.1f, heat), lockWidth * Mathf.Lerp(1.6f, 1.1f, heat),
                Color.Lerp(f.Violet, f.Core, heat), 0.35f * heat);
            Stretch(_coreHot, center, lockWidth * 0.7f, lockWidth * 0.7f, Color.white, 0.25f * heat);
            var squash = config.ShutterFxSquash;
            _padlock.localScale = new Vector3(Mathf.Lerp(1f, squash.x, heat), Mathf.Lerp(1f, squash.y, heat), 1f) * LockScale;
        }

        // The lock lets go: the beads are gone, gold sparks fly off on arcs.
        private void Release(BallAnimationConfig config)
        {
            var center = LockCenter();
            _releasedAt = center;
            foreach (var bead in _beads) bead.enabled = false;
            for (var i = 0; i < config.ShutterFxSparks; i++)
            {
                var angle = Mathf.Lerp(-10f, 190f, (i + UnityEngine.Random.value) / Mathf.Max(config.ShutterFxSparks, 1)) * Mathf.Deg2Rad;
                var speed = UnityEngine.Random.Range(config.ShutterFxSparkSpeed.x, config.ShutterFxSparkSpeed.y);
                _sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = center,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startColor = Color.Lerp(config.Finish.Gold, Color.white, UnityEngine.Random.Range(0f, 0.5f)),
                    startSize = UnityEngine.Random.Range(0.12f, 0.2f),
                    startLifetime = UnityEngine.Random.Range(0.3f, 0.5f),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        // The hit: a hot core flash, a crisp horizon ring with its photon ring widening, the lock and frame pop back.
        private void Hit(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var lockWidth = LockWidth();
            var grow = PrimeTween.Easing.Evaluate(k, PrimeTween.Ease.OutCubic);
            var size = lockWidth * Mathf.Lerp(config.ShutterFxSnapTo, config.ShutterFxHorizonTo, grow);
            var fade = 1f - k * k;
            Ring(Color.white, f.Violet, fade * SoftRing, size, _releasedAt);
            var photon = Mathf.Min(size * 0.85f, _width);
            Stretch(_photon, _releasedAt, photon, photon * 0.62f, Color.Lerp(f.Core, f.Gold, 0.35f), fade * 0.45f);
            var flash = lockWidth * config.ShutterFxFlashSize * Mathf.Lerp(0.6f, 1f, grow);
            Stretch(_coreHot, _releasedAt, flash, flash, Color.white, (1f - grow) * 0.45f);
            Stretch(_coreGlow, _releasedAt, flash * 1.4f, flash * 1.4f, f.Core, (1f - grow) * 0.3f);
            var pop = PrimeTween.Easing.Evaluate(k, PrimeTween.Ease.OutBack);
            var squash = config.ShutterFxSquash;
            _padlock.localScale = new Vector3(Mathf.LerpUnclamped(squash.x, 1f, pop), Mathf.LerpUnclamped(squash.y, 1f, pop), 1f) * LockScale;
            var punch = 1f + config.ShutterFxPunch * (1f - grow);
            _frame.transform.localScale = _housing.transform.localScale = new Vector3(punch, punch, 1f);
            if (k >= 1f) _coreHot.enabled = _coreGlow.enabled = _photon.enabled = false;
        }

        // A bright seam rides the curtain's lower edge; violet energy fills the opened glass under it.
        private void Seam(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var edge = transform.InverseTransformPoint(_curtain.bounds.min).y;
            var alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.15f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.8f) / 0.2f)));
            Stretch(_seam, new Vector3(0f, edge, 0f), _width, config.ShutterFxSeamHeight, f.Core, alpha * config.ShutterFxSeamAlpha);
            Stretch(_seamGlow, new Vector3(0f, edge, 0f), _width * 1.02f, config.ShutterFxSeamHeight * 5f, f.Core, alpha * 0.45f);
            Spill(config, edge, Mathf.Min(1f, k / 0.15f) * config.ShutterFxSpillAlpha);
        }

        // As the tubes come alive the spill sinks away and their rims flash in the finish's gold.
        private void Linger(BallAnimationConfig config, float k)
        {
            var edge = transform.InverseTransformPoint(_curtain.bounds.min).y;
            Spill(config, edge, (1f - Mathf.SmoothStep(0f, 1f, k)) * config.ShutterFxSpillAlpha);
            var flash = k < 0.2f ? k / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.2f) / 0.8f);
            var gold = config.CompleteGlowColor;
            Stretch(_rimGlow, new Vector3(0f, _housing.transform.localPosition.y, 0f), _width, config.ShutterFxRimHeight,
                Color.Lerp(gold, Color.white, 0.35f), flash * config.ShutterFxRimAlpha);
            if (k >= 1f) _spill.enabled = _rimGlow.enabled = _seam.enabled = _seamGlow.enabled = false;
        }

        // Fills the opened glass from the shutter's foot up to the edge, never below the tubes.
        private void Spill(BallAnimationConfig config, float edge, float alpha)
        {
            var height = Mathf.Max(edge - _seamBase, 0f);
            Stretch(_spill, new Vector3(0f, _seamBase + height * 0.5f, 0f), _width, height * 1.1f, config.Finish.Violet, alpha);
        }

        // Two beads run round the closing ring, behind the lock on the far half.
        private void Orbit(FinishFxSettings f, float k, float width, Vector3 center, float alpha)
        {
            for (var i = 0; i < _beads.Length; i++)
            {
                var bead = _beads[i];
                var angle = -Mathf.PI * 0.5f + Mathf.PI * (2f * k * 1.5f + i);
                var near = Mathf.Sin(angle) < 0f;
                var position = center + new Vector3(Mathf.Cos(angle) * width * 0.42f, Mathf.Sin(angle) * width * RingAspect * 0.42f, 0f);
                var size = LockWidth() * 0.32f;
                Stretch(bead, position, size, size, Color.Lerp(f.Core, f.Gold, 0.3f), alpha * (near ? 1f : 0.55f));
                bead.sortingOrder = near ? BeadNearOrder : BeadFarOrder;
            }
        }

        private float LockWidth() => _padlockBody.bounds.size.x / Mathf.Max(transform.lossyScale.x, 0.0001f);

        private Vector3 LockCenter() => transform.InverseTransformPoint(_padlockBody.bounds.center);

        // Kept inside the shutter's own width.
        private void Ring(Color line, Color glow, float alpha, float width, Vector3 center)
        {
            width = Mathf.Min(width, _width * 1.05f);
            foreach (var (renderer, color, share) in new[] { (_ringNear, line, 1f), (_ringFar, line, 0.7f), (_glowNear, glow, 1f), (_glowFar, glow, 0.7f) })
                Stretch(renderer, center, width, width * RingAspect, color, alpha * share);
        }

        private static void Stretch(SpriteRenderer renderer, Vector3 position, float width, float height, Color color, float alpha)
        {
            renderer.enabled = alpha > 0.001f && renderer.sprite != null;
            if (!renderer.enabled) return;
            var native = renderer.sprite.bounds.size;
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(width / native.x, height / native.y, 1f);
            renderer.color = new Color(color.r, color.g, color.b, color.a * alpha);
        }

        private void HideFx()
        {
            _ringNear.enabled = _ringFar.enabled = _glowNear.enabled = _glowFar.enabled = false;
            _seam.enabled = _seamGlow.enabled = _spill.enabled = _rimGlow.enabled = false;
            _coreGlow.enabled = _coreHot.enabled = _photon.enabled = false;
            foreach (var bead in _beads) bead.enabled = false;
            _sparks.Clear();
            _padlock.localScale = Vector3.one * LockScale;
            LockLight(1f);
            _frame.transform.localScale = _housing.transform.localScale = Vector3.one;
        }

        // Back to closed at rest without running callbacks.
        public void Stop()
        {
            HideFx();
            if (_play.isAlive) _play.Stop();
            if (_rattle.isAlive) _rattle.Stop();
            _rattleOffset = Vector3.zero;
            _frame.color = _housing.color = _padlockBase.color = Color.white;
            var c = _padlockBody.color;
            _padlockBody.color = new Color(c.r, c.g, c.b, 1f);
            _curtain.transform.localPosition = new Vector3(0f, _rise * 0.5f, 0f);
            PlacePadlock(Vector3.zero, 0f);
        }

        public override void OnDespawn()
        {
            Stop();
            _first = _last = null;
        }
    }
}
