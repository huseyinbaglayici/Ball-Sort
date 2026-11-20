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
        [SerializeField] private SpriteMask _mask;

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

        public bool IsPlaying => _play.isAlive;

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

        private void LateUpdate() => Follow();

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

        // Padlock drops, curtain rolls up, housing and frame fade; onClear when the curtain is up, onDone when all is.
        public void PlayOpen(BallAnimationConfig config, Action onClear, Action onDone)
        {
            Stop();
            Follow();
            var shake = config.ShutterShakeDuration;
            var rollAt = shake + config.ShutterRollDelay;
            var clearAt = rollAt + config.ShutterRollDuration;
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
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.LockDropDuration, (shutter, k) =>
                {
                    shutter.PlacePadlock(Vector3.down * (config.LockDropDistance * k * k), config.LockDropDegrees * k);
                    var a = 1f - k;
                    shutter._padlockBase.color = new Color(1f, 1f, 1f, a);
                    var c = shutter._padlockBody.color;
                    shutter._padlockBody.color = new Color(c.r, c.g, c.b, a);
                }))
                .Insert(rollAt, PrimeTween.Tween.Custom(this, 0f, config.ShutterRollDistance, config.ShutterRollDuration,
                    (shutter, y) => shutter._curtain.transform.localPosition = new Vector3(0f, curtainY + y, 0f),
                    PrimeTween.Ease.InOutSine))
                .InsertCallback(clearAt, () => onClear?.Invoke())
                .Insert(clearAt, PrimeTween.Tween.Custom(this, 1f, 0f, config.ShutterFadeDuration, (shutter, a) =>
                {
                    shutter._frame.color = new Color(1f, 1f, 1f, a);
                    shutter._housing.color = new Color(1f, 1f, 1f, a);
                }))
                .ChainCallback(() => onDone?.Invoke());
        }

        // Back to closed at rest without running callbacks.
        public void Stop()
        {
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
