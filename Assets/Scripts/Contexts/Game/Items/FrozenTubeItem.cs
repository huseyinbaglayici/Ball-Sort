using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Ice over a whole tube with the needed color's orb on its badge; parented to the tube's art.
    public sealed class FrozenTubeItem : Item
    {
        // Badge center in the art, from its top.
        private const float BadgeYPx = 515f;
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int CrackLightId = Shader.PropertyToID("_CrackLight");
        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private static readonly int CutsId = Shader.PropertyToID("_Cuts");
        private static readonly int CrackOriginId = Shader.PropertyToID("_CrackOrigin");
        private static readonly int PieceId = Shader.PropertyToID("_Piece");
        // Cuts between the pieces in sprite v, bottom to top; the badge sits in the middle piece.
        private static readonly float[] Cuts = { -1f, 0.34f, 0.66f, 2f };
        // Each piece's slide direction: sideways in turn, the top one rising a little.
        private static readonly Vector2[] Slides = { new(-1f, -0.4f), new(1f, -0.1f), new(-0.8f, 0.35f) };
        private const float RingAspect = 0.32f;

        [SerializeField] private SpriteRenderer _frozen;
        [SerializeField] private Transform _badge;
        // Orb layers on the badge, as on a ball: outer glow, core and inner glow take the color.
        [SerializeField] private SpriteRenderer _badgeOuterGlow;
        [SerializeField] private SpriteRenderer _badgeCore;
        [SerializeField] private SpriteRenderer _badgeInnerGlow;
        [SerializeField] private SpriteRenderer[] _badgeLayers;
        // Break FX: the ice cut into pieces, orbit ring halves and a hot core at the badge, vapour and the rim flash.
        [SerializeField] private SpriteRenderer[] _pieces;
        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _hot;
        [SerializeField] private SpriteRenderer[] _vapour;
        [SerializeField] private SpriteRenderer _rimGlow;

        private static MaterialPropertyBlock _block;
        private PrimeTween.Sequence _play;
        private float _haloAlpha;
        private float _iceAlpha = 1f;
        private float _badgeScale = 1f;

        public bool IsPlaying => _play.isAlive;

        // rise: the tube's extra height over the standard art; the ice stretches with it, the badge stays its size.
        public void Setup(OrbSkin orb, float rise, BallAnimationConfig config)
        {
            Stop();
            transform.localPosition = new Vector3(0f, rise * 0.5f, 0f);
            transform.localScale = Vector3.one;
            var stretch = new Vector3(1f, 1f + rise * TubeArt.PixelsPerUnit / TubeArt.ArtHeightPx, 1f);
            _frozen.transform.localScale = stretch;
            foreach (var piece in _pieces) piece.transform.localScale = stretch;
            _badge.localPosition = new Vector3(0f, TubeArt.ArtY(BadgeYPx), 0f);
            // The orb art's sphere is TubeArt.BallDiameter wide at scale 1.
            _badgeScale = config.FrozenBadgeOrbDiameter / TubeArt.BallDiameter;
            _badge.localScale = Vector3.one * _badgeScale;
            _badgeOuterGlow.sprite = orb.OuterGlow;
            _badgeCore.sprite = orb.Core;
            _badgeInnerGlow.sprite = orb.InnerGlow;
            OrbTone.Apply(_badgeCore, orb);
            _haloAlpha = config.OrbHaloAlpha;
            // The crack starts at the badge, in the ice's own uv.
            var origin = new Vector4(0.5f, 1f - BadgeYPx / TubeArt.ArtHeightPx, 0f, 0f);
            // The crack strokes end in the cuts the pieces split along.
            var cuts = new Vector4(Cuts[1], Cuts[2], config.FrozenPieceJag, 0f);
            SetVector(_frozen, CrackOriginId, origin);
            SetVector(_frozen, CutsId, cuts);
            for (var i = 0; i < _pieces.Length; i++)
            {
                SetVector(_pieces[i], CrackOriginId, origin);
                SetVector(_pieces[i], CutsId, cuts);
                SetVector(_pieces[i], PieceId, new Vector4(Cuts[i], Cuts[i + 1], 0f, 1f));
            }
            SetIceAlpha(1f);
        }

        // Wobble while a ring closes on the badge; the hit cracks the ice with light, it thaws, splits and steams away.
        public void PlayMelt(BallAnimationConfig config, Action onClear, Action onDone)
        {
            Stop();
            var wobble = config.MeltWobbleDuration;
            var chargeAt = Mathf.Max(wobble - config.FrozenChargeTime, 0f);
            var splitAt = wobble + config.MeltDuration;
            var clearAt = splitAt + config.MeltOutDuration;
            var fxEnd = Mathf.Max(config.FrozenPieceTime, config.FrozenVapourTime, config.FrozenRimTime);
            _play = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, wobble, (ice, k) =>
                {
                    var w = config.MeltWobble * (1f - k) * Mathf.Sin(k * config.MeltWobbleCycles * Mathf.PI * 2f);
                    ice.transform.localScale = new Vector3(1f + w, 1f - w, 1f);
                }))
                .ChainCallback(this, ice => ice.transform.localScale = Vector3.one)
                .Insert(chargeAt, PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(wobble - chargeAt, 0.0001f),
                    (ice, k) => ice.Charge(config, k)))
                .Insert(wobble, PrimeTween.Tween.Custom(this, 0f, 1f, config.FrozenHorizonTime, (ice, k) => ice.Hit(config, k)))
                .Insert(wobble, PrimeTween.Tween.Custom(this, 0f, 1f, config.MeltDuration, (ice, k) => ice.Thaw(config, k)))
                .InsertCallback(splitAt, this, ice => ice.Split())
                .Insert(splitAt, PrimeTween.Tween.Custom(this, 0f, 1f, fxEnd, (ice, t) => ice.Shatter(config, t * fxEnd)))
                .InsertCallback(clearAt, () => onClear?.Invoke())
                .ChainCallback(() => onDone?.Invoke());
        }

        // The ring snaps in round the badge while the frost brightens.
        private void Charge(BallAnimationConfig config, float k)
        {
            var badge = config.FrozenBadgeOrbDiameter;
            var size = badge * Mathf.Lerp(config.FrozenRingFrom, config.FrozenRingTo, 1f - (1f - k) * (1f - k));
            Ring(config.Finish.Core, Mathf.Clamp01(k * 3f), size, _badge.localPosition);
            SetFloat(_frozen, ChargeId, k * k);
        }

        // Horizon ring out to the tube's width, hot flash at the badge.
        private void Hit(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var center = _badge.localPosition;
            var badge = config.FrozenBadgeOrbDiameter;
            var grow = 1f - (1f - k) * (1f - k) * (1f - k);
            var fade = 1f - k * k;
            var reach = TubeArt.ArtWidthPx / TubeArt.PixelsPerUnit * config.FrozenHorizonTo;
            Ring(Color.Lerp(Color.white, f.Core, k), fade * 0.8f, Mathf.Lerp(badge * config.FrozenRingTo, reach, grow), center);
            var flash = badge * Mathf.Lerp(0.9f, 1.4f, grow);
            Stretch(_hot, center, flash, flash, Color.white, (1f - grow) * 0.6f);
            SetFloat(_frozen, ChargeId, 1f - grow);
            var punch = 1f + config.FrozenPunch * (1f - grow);
            transform.localScale = new Vector3(punch, punch, 1f);
            if (k >= 1f) _ringNear.enabled = _ringFar.enabled = _hot.enabled = false;
        }

        // Crack light sweeps from the badge out along every stroke, then holds while the ice thins.
        private void Thaw(BallAnimationConfig config, float k)
        {
            var crack = Mathf.Clamp01(k * config.MeltDuration / Mathf.Max(config.FrozenCrackTime, 0.0001f));
            SetFloat(_frozen, CrackId, 1f - (1f - crack) * (1f - crack));
            SetFloat(_frozen, CrackLightId, 1f);
            SetIceAlpha(Mathf.Lerp(1f, config.MeltHalfAlpha, k));
        }

        // The whole ice gives way to its pieces, each still lit.
        private void Split()
        {
            _frozen.enabled = false;
            foreach (var piece in _pieces)
            {
                piece.enabled = true;
                piece.color = new Color(1f, 1f, 1f, _iceAlpha);
            }
        }

        // Pieces slide and fade, vapour rises, the rim flashes gold and the badge orb pops away.
        private void Shatter(BallAnimationConfig config, float time)
        {
            var f = config.Finish;
            transform.localScale = Vector3.one;
            var k = Mathf.Clamp01(time / Mathf.Max(config.FrozenPieceTime, 0.0001f));
            var out3 = 1f - (1f - k) * (1f - k) * (1f - k);
            for (var i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                var slide = Slides[i];
                var t = piece.transform;
                t.localPosition = new Vector3(slide.x * config.FrozenPieceSlide * out3,
                    slide.y * config.FrozenPieceSlide * out3 - config.FrozenPieceDrop * k * k, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, -slide.x * config.FrozenPieceSpin * out3);
                var alpha = _iceAlpha * (1f - k * k);
                piece.enabled = alpha > 0.001f;
                piece.color = new Color(1f, 1f, 1f, alpha);
                SetFloat(piece, CrackId, 1f);
                SetFloat(piece, CrackLightId, 1f - out3);
            }

            var pop = Mathf.Clamp01(time / Mathf.Max(config.FrozenBadgePopTime, 0.0001f));
            var swell = 1f + (config.FrozenBadgePop - 1f) * Mathf.Sin(pop * Mathf.PI * 0.5f);
            _badge.localScale = Vector3.one * (_badgeScale * swell);
            SetBadgeAlpha(_iceAlpha * (1f - pop * pop));

            var v = Mathf.Clamp01(time / Mathf.Max(config.FrozenVapourTime, 0.0001f));
            var width = TubeArt.ArtWidthPx / TubeArt.PixelsPerUnit;
            var mist = Color.Lerp(f.Core, Color.white, 0.4f);
            for (var i = 0; i < _vapour.Length; i++)
            {
                var vk = Mathf.Clamp01(v * (1f + i * 0.35f) - i * 0.15f);
                var rise = config.FrozenVapourRise * (1f - (1f - vk) * (1f - vk));
                var shape = Mathf.Lerp(0.9f, 1.4f, vk) * width;
                var alpha = config.FrozenVapourAlpha * Mathf.Sin(Mathf.PI * Mathf.Sqrt(vk)) * (i == 0 ? 1f : 0.7f);
                Stretch(_vapour[i], _badge.localPosition + new Vector3((i - 0.5f) * width * 0.25f, rise, 0f),
                    shape, shape * (1.6f - i * 0.3f), mist, alpha);
            }

            var r = Mathf.Clamp01(time / Mathf.Max(config.FrozenRimTime, 0.0001f));
            var flash = r < 0.2f ? r / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (r - 0.2f) / 0.8f);
            var top = _frozen.sprite.bounds.max.y * _frozen.transform.localScale.y;
            Stretch(_rimGlow, new Vector3(0f, top, 0f), width, config.FrozenRimHeight,
                Color.Lerp(config.CompleteGlowColor, Color.white, 0.35f), flash * config.FrozenRimAlpha);
        }

        private void SetIceAlpha(float alpha)
        {
            _iceAlpha = alpha;
            _frozen.color = new Color(1f, 1f, 1f, alpha);
            SetBadgeAlpha(alpha);
        }

        private void SetBadgeAlpha(float alpha)
        {
            foreach (var layer in _badgeLayers)
                layer.color = new Color(1f, 1f, 1f, layer == _badgeOuterGlow ? alpha * _haloAlpha : alpha);
        }

        private void Ring(Color color, float alpha, float width, Vector3 center)
        {
            Stretch(_ringNear, center, width, width * RingAspect, color, alpha);
            Stretch(_ringFar, center, width, width * RingAspect, color, alpha * 0.6f);
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

        // Read first: the renderer keeps its sprite texture in this block.
        private static void SetFloat(Renderer renderer, int id, float value)
        {
            renderer.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetFloat(id, value);
            renderer.SetPropertyBlock(_block);
        }

        private static void SetVector(Renderer renderer, int id, Vector4 value)
        {
            renderer.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetVector(id, value);
            renderer.SetPropertyBlock(_block);
        }

        // Back to the ice at rest without running callbacks.
        public void Stop()
        {
            if (_play.isAlive) _play.Stop();
            transform.localScale = Vector3.one;
            _frozen.enabled = true;
            SetFloat(_frozen, CrackId, 0f);
            SetFloat(_frozen, ChargeId, 0f);
            foreach (var piece in _pieces)
            {
                piece.enabled = false;
                piece.transform.localPosition = Vector3.zero;
                piece.transform.localRotation = Quaternion.identity;
            }
            _ringNear.enabled = _ringFar.enabled = _hot.enabled = _rimGlow.enabled = false;
            foreach (var mist in _vapour) mist.enabled = false;
            _badge.localScale = Vector3.one * _badgeScale;
            SetIceAlpha(1f);
        }

        public override void OnDespawn() => Stop();
    }
}
