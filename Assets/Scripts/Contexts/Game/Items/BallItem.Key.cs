using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A key ball: the key art it carries and the lift that hands it to its lockbox.
    public sealed partial class BallItem
    {
        // id null removes it; tint is the lock's Surface color.
        public void SetKey(string id, Color tint)
        {
            if (_key == null) return;
            // A used key finishes fading on its own.
            if (id == null && _keyUse.isAlive) return;
            if (id == KeyId && !_keyUse.isAlive && (id == null || _key.color == tint)) return;
            StopKeyUse();
            KeyId = id;
            if (id != null) _key.color = Color.Lerp(tint, Color.white, KeyLighten);
            ApplyLook();
        }

        // Where the key sits in the ball, for the flight that carries it to its lock.
        public Transform KeyMark => _key != null ? _key.transform : null;

        // Drawn width of the key mark, so the flight starts at the same size.
        public float KeyWorldWidth => _key != null && _key.sprite != null ? _key.sprite.bounds.size.x * _key.transform.lossyScale.x : 0f;

        // The key leaves the ball for its flight: gone from the orb at once.
        public void ReleaseKey()
        {
            if (KeyId == null) return;
            StopKeyUse();
            KeyId = null;
            ApplyLook();
        }

        // The key grows with a small overshoot and fades, then the ball is a plain orb.
        public void PlayKeyUse(BallAnimationConfig config)
        {
            if (KeyId == null || _keyUse.isAlive) return;
            _keyUse = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 1f, config.KeyUseScale, config.KeyUsePop,
                    (ball, s) => ball._keyRoot.localScale = Vector3.one * s, PrimeTween.Ease.OutBack))
                .Chain(PrimeTween.Tween.Custom(this, 1f, 0f, config.KeyUseFade, (ball, a) => ball.SetKeyAlpha(a)))
                .ChainCallback(this, ball =>
                {
                    ball.KeyId = null;
                    ball.ResetKey();
                    ball.ApplyLook();
                });
        }

        private void StopKeyUse()
        {
            if (_keyUse.isAlive) _keyUse.Stop();
            ResetKey();
        }

        private void ResetKey()
        {
            if (_keyRoot == null) return;
            _keyRoot.localScale = Vector3.one;
            SetKeyAlpha(1f);
        }

        private void SetKeyAlpha(float alpha)
        {
            _keyGlow.color = new Color(KeyShade.r, KeyShade.g, KeyShade.b, KeyShade.a * alpha);
            var c = _key.color;
            _key.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
