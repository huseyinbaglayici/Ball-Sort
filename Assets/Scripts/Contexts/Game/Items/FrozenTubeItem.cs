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

        [SerializeField] private SpriteRenderer _frozen;
        [SerializeField] private SpriteRenderer _melt;
        [SerializeField] private Transform _badge;
        // Orb layers on the badge, as on a ball: outer glow, core and inner glow take the color.
        [SerializeField] private SpriteRenderer _badgeOuterGlow;
        [SerializeField] private SpriteRenderer _badgeCore;
        [SerializeField] private SpriteRenderer _badgeInnerGlow;
        [SerializeField] private SpriteRenderer[] _badgeLayers;

        private PrimeTween.Sequence _play;
        private float _haloAlpha;

        public bool IsPlaying => _play.isAlive;

        // rise: the tube's extra height over the standard art; the ice stretches with it, the badge stays its size.
        public void Setup(OrbSkin orb, float rise, BallAnimationConfig config)
        {
            Stop();
            transform.localPosition = new Vector3(0f, rise * 0.5f, 0f);
            transform.localScale = Vector3.one;
            var stretch = new Vector3(1f, 1f + rise * TubeArt.PixelsPerUnit / TubeArt.ArtHeightPx, 1f);
            _frozen.transform.localScale = stretch;
            _melt.transform.localScale = stretch;
            _badge.localPosition = new Vector3(0f, TubeArt.ArtY(BadgeYPx), 0f);
            // The orb art's sphere is TubeArt.BallDiameter wide at scale 1.
            _badge.localScale = Vector3.one * (config.FrozenBadgeOrbDiameter / TubeArt.BallDiameter);
            _badgeOuterGlow.sprite = orb.OuterGlow;
            _badgeCore.sprite = orb.Core;
            _badgeInnerGlow.sprite = orb.InnerGlow;
            OrbTone.Apply(_badgeCore, orb);
            _haloAlpha = config.OrbHaloAlpha;
            SetIceAlpha(1f);
            _melt.enabled = false;
        }

        // Wobble, then the ice fades while the melt art flashes in; onClear when the ice is gone, onDone when all is.
        public void PlayMelt(BallAnimationConfig config, Action onClear, Action onDone)
        {
            Stop();
            var wobble = config.MeltWobbleDuration;
            var clearAt = wobble + config.MeltDuration + config.MeltOutDuration;
            _play = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, wobble, (ice, k) =>
                {
                    var w = config.MeltWobble * (1f - k) * Mathf.Sin(k * config.MeltWobbleCycles * Mathf.PI * 2f);
                    ice.transform.localScale = new Vector3(1f + w, 1f - w, 1f);
                }))
                .ChainCallback(this, ice =>
                {
                    ice.transform.localScale = Vector3.one;
                    ice._melt.enabled = true;
                    ice._melt.color = new Color(1f, 1f, 1f, 0f);
                })
                .Chain(PrimeTween.Tween.Custom(this, 1f, config.MeltHalfAlpha, config.MeltDuration,
                    (ice, a) => ice.SetIceAlpha(a)))
                .Chain(PrimeTween.Tween.Custom(this, config.MeltHalfAlpha, 0f, config.MeltOutDuration,
                    (ice, a) => ice.SetIceAlpha(a)))
                .Insert(wobble, PrimeTween.Tween.Custom(this, 0f, 1f, config.MeltShardIn,
                    (ice, a) => ice._melt.color = new Color(1f, 1f, 1f, a)))
                .Insert(wobble + config.MeltShardIn, PrimeTween.Tween.Custom(this, 1f, 0f, config.MeltShardOut,
                    (ice, a) => ice._melt.color = new Color(1f, 1f, 1f, a)))
                .InsertCallback(clearAt, () => onClear?.Invoke())
                .ChainCallback(() => onDone?.Invoke());
        }

        private void SetIceAlpha(float alpha)
        {
            _frozen.color = new Color(1f, 1f, 1f, alpha);
            foreach (var layer in _badgeLayers)
                layer.color = new Color(1f, 1f, 1f, layer == _badgeOuterGlow ? alpha * _haloAlpha : alpha);
        }

        // Back to the ice at rest without running callbacks.
        public void Stop()
        {
            if (_play.isAlive) _play.Stop();
            transform.localScale = Vector3.one;
            _melt.enabled = false;
            SetIceAlpha(1f);
        }

        public override void OnDespawn() => Stop();
    }
}
