using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A bomb ball: its fuse look, the warning pulse, a defuse and the explosion.
    public sealed partial class BallItem
    {
        // ring: the orb's Surface color. A defuse in progress finishes; an undo puts the bomb back at once.
        public void SetBomb(BombLook look, int fuse, Color ring, BallAnimationConfig config)
        {
            if (_bombBase == null) return;
            if (look == BombLook.None && _defusing) return;
            var changed = look != Bomb || _bombPlay.isAlive || _exploded;
            // Lit by this move: the light fades in; at level start or after an undo it is just on.
            var newlyLit = look == BombLook.Lit && Bomb == BombLook.Passive;
            StopBombPlay();
            Bomb = look;
            _bombRing.color = ring;
            _bombCount.text = fuse.ToString();
            var fast = fuse <= config.BombFastFuse;
            if (look != BombLook.Lit) StopPulse();
            else if (changed || fast != _pulseFast || !_pulse.isAlive)
            {
                if (changed) _litAt = newlyLit ? Time.time : float.NegativeInfinity;
                Pulse(config, fast);
            }
            ApplyLook();
        }

        private void Pulse(BallAnimationConfig config, bool fast)
        {
            if (_pulse.isAlive) _pulse.Stop();
            _pulseFast = fast;
            var period = Mathf.Max(fast ? config.BombPulseFastPeriod : config.BombPulsePeriod, 0.02f);
            var fadeIn = config.BombLitFadeIn;
            var low = config.BombPulseMin;
            _pulse = PrimeTween.Tween.Custom(this, 0f, 1f, period, (ball, k) =>
            {
                var wave = Mathf.Lerp(low, 1f, 0.5f + 0.5f * Mathf.Cos(k * Mathf.PI * 2f));
                var fade = fadeIn > 0f ? Mathf.Clamp01((Time.time - ball._litAt) / fadeIn) : 1f;
                ball._bombFx.color = new Color(1f, 1f, 1f, wave * fade);
            }, cycles: -1);
        }

        private void StopPulse()
        {
            if (_pulse.isAlive) _pulse.Stop();
            if (_bombFx != null) _bombFx.color = Color.white;
        }

        // The bomb splits open and fades; the orb pops in under it.
        public void PlayDefuse(BallAnimationConfig config)
        {
            if (_bombBase == null || Bomb == BombLook.None) return;
            StopBombPlay();
            StopPulse();
            Bomb = BombLook.None;
            _defusing = true;
            _defuse.enabled = true;
            _defuse.color = Color.white;
            _defuse.transform.localScale = Vector3.one * config.DefuseFrom;
            SetPartsScale(0f);
            ApplyLook();
            _bombPlay = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, config.DefuseFrom, 1f, config.DefuseIn,
                    (ball, s) => ball._defuse.transform.localScale = Vector3.one * s, PrimeTween.Ease.OutBack))
                .ChainDelay(config.DefuseHold)
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.DefuseOut, (ball, k) =>
                {
                    ball._defuse.transform.localScale = Vector3.one * Mathf.Lerp(1f, config.DefuseOutScale, k);
                    ball._defuse.color = new Color(1f, 1f, 1f, 1f - k);
                }))
                .Insert(config.DefuseOrbDelay, PrimeTween.Tween.Custom(this, config.DefuseOrbFrom, 1f,
                    config.DefuseOrbDuration, (ball, s) => ball.SetPartsScale(s), PrimeTween.Ease.OutBack))
                .ChainCallback(this, ball => ball.StopBombPlay());
        }

        // The bomb trembles and swells, then it and the orb vanish and onBlast gets the blast's world position.
        public void PlayExplode(BallAnimationConfig config, System.Action<Vector3> onBlast)
        {
            if (_bombBase == null || Bomb == BombLook.None) return;
            StopBombPlay();
            _bombPlay = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.ExplodeShake, (ball, k) =>
                {
                    var swing = Mathf.Sin(k * TrembleCycles * Mathf.PI * 2f);
                    ball.transform.localRotation = Quaternion.Euler(0f, 0f, config.ExplodeShakeDegrees * k * swing);
                    ball.SetPartsScale(Mathf.Lerp(1f, config.ExplodeShakeScale, k));
                }))
                .ChainCallback(this, ball =>
                {
                    ball.transform.localRotation = Quaternion.identity;
                    ball.SetPartsScale(1f);
                    ball._exploded = true;
                    ball.ApplyLook();
                    onBlast?.Invoke(ball._visual.position);
                });
        }

        private void StopBombPlay()
        {
            var playing = _bombPlay.isAlive;
            if (playing) _bombPlay.Stop();
            if (_defuse != null) _defuse.enabled = false;
            if (playing || _defusing || _exploded)
            {
                transform.localRotation = Quaternion.identity;
                SetPartsScale(1f);
            }
            _defusing = false;
            _exploded = false;
        }
    }
}
