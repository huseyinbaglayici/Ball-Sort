using System;
using Project.Contexts.Game.Sort;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Contexts.Game.Items
{
    // The blast of a bomb whose fuse ran out; parented to the board and drawn over everything on it.
    [RequireComponent(typeof(SortingGroup))]
    public sealed class BombBlastFx : Item
    {
        [SerializeField] private SpriteRenderer _blast;

        private PrimeTween.Sequence _play;

        // Grows in with an overshoot, holds while it grows a little, then fades out growing.
        public void Play(BallAnimationConfig config, Action<BombBlastFx> onDone)
        {
            Stop();
            _blast.enabled = true;
            _blast.color = Color.white;
            var t = _blast.transform;
            t.localScale = Vector3.one * config.ExplodeFrom;
            _play = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Scale(t, config.ExplodeFrom, config.ExplodePeak, config.ExplodeIn,
                    PrimeTween.Ease.OutBack))
                .Chain(PrimeTween.Tween.Scale(t, config.ExplodePeak, config.ExplodeHoldScale, config.ExplodeHold))
                .Chain(PrimeTween.Tween.Scale(t, config.ExplodeHoldScale, config.ExplodeEndScale, config.ExplodeOut,
                    PrimeTween.Ease.OutQuad))
                .Group(PrimeTween.Tween.Alpha(_blast, 1f, 0f, config.ExplodeOut, PrimeTween.Ease.InQuad))
                .ChainCallback(this, fx => onDone?.Invoke(fx));
        }

        private void Stop()
        {
            if (_play.isAlive) _play.Stop();
            _blast.enabled = false;
        }

        public override void OnDespawn() => Stop();
    }
}
