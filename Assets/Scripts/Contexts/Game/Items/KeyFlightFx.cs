using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A freed key glides on one smooth curve from its ball, past the screen's centre, into its padlock's keyhole.
    public sealed class KeyFlightFx : Item
    {
        // Speed profile samples; the curve eases in, slows at the apex and eases into the keyhole.
        private const int ProfileSamples = 128;
        // Follow-through lean toward the heading: share, maximum angle and catch-up rate.
        private const float LeanShare = 0.3f;
        private const float MaxLean = 20f;
        private const float LeanRate = 9f;
        // Ghost copies trail the key every few frames and vanish as it slows.
        private const int GhostSpacing = 1;
        private const float GhostAlpha = 0.4f;
        // Stretch along the motion per unit of speed, as a share of the key's width per second.
        private const float StretchPerSpeed = 0.035f;
        private const float MaxStretch = 0.3f;
        // Share of the key's length that slides into the lock.
        private const float InsertDepth = 0.45f;
        // The keyhole sits a little below the padlock body's centre, as a share of its width.
        private const float KeyholeDrop = 0.08f;
        // The click star at the keyhole: its size as a share of the padlock's width and its turn.
        private const float ClickSize = 0.55f;
        private const float ClickTurn = 25f;
        private const float ClickTime = 0.18f;
        // Pushed in, the key reads a touch smaller, as if moving away.
        private const float InsertShrink = 0.92f;
        // Edge-on width of the key at the end of its quarter turn.
        private const float TwistEdge = 0.18f;

        [SerializeField] private SpriteRenderer _key;
        [SerializeField] private SpriteRenderer _flash;
        [SerializeField] private SpriteRenderer[] _ghosts;
        [SerializeField] private SpriteMask _keyhole;

        private static float[] _profile;
        private readonly Vector3[] _history = new Vector3[32];
        private readonly float[] _historyAngle = new float[32];
        private readonly float[] _historyScale = new float[32];
        private int _historyCount;
        private Action _onInserted;
        private Action<KeyFlightFx> _onDone;
        private PrimeTween.Sequence _play;
        private float _lean;
        // Lying flat at the apex, pointing toward the lock's side: 0 to the right, 180 to the left.
        private float _apexAngle;
        private Vector3 _previous;
        private Color _keyColor;
        private Vector3 _keyholeTop;
        private float _lockWidth;

        private static Sprite _maskSprite;

        // The keyhole mask is a plain unit square made once.
        private void Awake()
        {
            if (_maskSprite == null)
                _maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0,
                    SpriteMeshType.FullRect);
            _keyhole.sprite = _maskSprite;
        }

        public static float Duration(BallAnimationConfig c) =>
            c.KeyFlyToCentre + c.KeyFlyToLock + c.KeyFlyInsertTime + c.KeyFlyTurn;

        // Board-local points; size is the key's drawn width, lockWidth the padlock's, tint the lock color.
        public void Play(Vector3 from, Vector3 centre, Vector3 lockAt, float size, float lockWidth, Color tint,
            BallAnimationConfig config, Action onInserted, Action<KeyFlightFx> onDone)
        {
            Stop();
            BuildProfile();
            _onInserted = onInserted;
            _onDone = onDone;
            var c = config;
            _keyColor = Color.Lerp(tint, Color.white, 0.6f);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var native = Mathf.Max(_key.sprite != null ? _key.sprite.bounds.size.x : 1f, 0.0001f);
            var start = size;
            var apex = size * c.KeyFlyCentreScale;
            var end = lockWidth * c.KeyFlyLockScale;
            lockAt += Vector3.down * (lockWidth * KeyholeDrop);
            // One eased curve ends with the key inside the hole, decelerating all the way with no stop at the mouth.
            var hover = lockAt + Vector3.up * (end * 0.5f);
            var seated = hover + Vector3.down * (end * InsertDepth);
            var path = new Path(from, centre, seated, c.KeyFlyArc);
            _keyholeTop = lockAt;
            _lockWidth = lockWidth;
            _lean = 0f;
            _apexAngle = hover.x >= centre.x ? 0f : 180f;
            _previous = from;
            _historyCount = 0;
            var flight = Mathf.Max(c.KeyFlyToCentre + c.KeyFlyToLock + c.KeyFlyInsertTime, 0.0001f);

            _play = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, flight,
                    (fx, k) => fx.Fly(path, Sample(k), start, apex, end * InsertShrink, native), PrimeTween.Ease.Linear))
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, Mathf.Max(c.KeyFlyTurn, 0.0001f),
                    (fx, k) => fx.Twist(seated, end * InsertShrink, native, k), PrimeTween.Ease.Linear))
                .ChainCallback(this, fx => fx.Click())
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, ClickTime,
                    (fx, k) => fx.Fade(lockWidth, k), PrimeTween.Ease.Linear))
                .ChainCallback(this, fx => fx.Finish());
        }

        // One flight frame: eased curve position, lagging tip-first rotation, depth swell and speed stretch.
        private void Fly(Path path, float s, float start, float apex, float end, float native)
        {
            var position = path.At(s);
            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var velocity = (position - _previous) / dt;
            _previous = position;
            // Tip up as it leaves the ball, flat at the apex, tip down over the keyhole: two smooth quarter turns.
            var downAngle = _apexAngle > 90f ? 270f : -90f;
            var baseAngle = s < 0.5f
                ? Mathf.Lerp(90f, _apexAngle, Smooth(s * 2f))
                : Mathf.Lerp(_apexAngle, downAngle, Smooth((s - 0.5f) * 2f));
            if (velocity.sqrMagnitude > 0.0001f)
            {
                var heading = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                var lean = Mathf.Clamp(Mathf.DeltaAngle(baseAngle, heading) * LeanShare, -MaxLean, MaxLean);
                _lean = Mathf.Lerp(_lean, lean, 1f - Mathf.Exp(-LeanRate * dt));
            }
            // The lean fades out as the key lines up with the keyhole.
            var angle = baseAngle + _lean * (1f - Smooth(Mathf.InverseLerp(0.75f, 1f, s)));
            // Toward the camera at the apex.
            var swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(s / 0.92f));
            var size = Mathf.Lerp(start, end, Smooth(s)) + (apex - Mathf.Lerp(start, end, 0.5f)) * swell * swell;
            var stretch = Mathf.Min(velocity.magnitude * StretchPerSpeed / Mathf.Max(size, 0.0001f), MaxStretch);
            Pose(position, angle, size / native, 1f + stretch, 1f / (1f + stretch), 1f);
            // The keyhole mask is on for the descent, so the tip vanishes into the lock as it crosses the hole.
            Keyhole(s > 0.6f);
            Trail(position, angle, size / native, velocity.magnitude / Mathf.Max(size * 6f, 0.0001f));
        }

        private void Keyhole(bool on)
        {
            _keyhole.enabled = on;
            if (!on) return;
            _keyhole.transform.localPosition = _keyholeTop + Vector3.down * (_lockWidth * 0.6f);
            _keyhole.transform.localScale = new Vector3(_lockWidth * 0.9f, _lockWidth * 1.2f, 1f);
        }

        // A quarter turn in the lock: the bow narrows edge-on as if seen turning in depth.
        private void Twist(Vector3 seated, float size, float native, float k)
        {
            var e = PrimeTween.Easing.Evaluate(k, PrimeTween.Ease.InOutSine);
            Pose(seated, -90f, size / native, 1f, Mathf.Lerp(1f, TwistEdge, e), 1f);
        }

        // The lock takes the key: the open starts on this frame, under a small flash at the keyhole.
        private void Click()
        {
            var inserted = _onInserted;
            _onInserted = null;
            inserted?.Invoke();
        }

        // The key fades into the lock as a crisp star pops at the keyhole, turns a little and shrinks away.
        private void Fade(float lockWidth, float k)
        {
            _key.color = new Color(_keyColor.r, _keyColor.g, _keyColor.b, 1f - Smooth(k));
            var pop = k < 0.4f
                ? PrimeTween.Easing.Evaluate(k / 0.4f, PrimeTween.Ease.OutBack)
                : 1f - PrimeTween.Easing.Evaluate((k - 0.4f) / 0.6f, PrimeTween.Ease.InQuad);
            var s = lockWidth * ClickSize * pop;
            Stretch(_flash, _keyholeTop, s, s, Color.white, 1f);
            _flash.transform.localRotation = Quaternion.Euler(0f, 0f, ClickTurn * k);
            if (k >= 1f) _flash.enabled = false;
        }

        // Scale along the key's own axis and across it, in its rotated frame.
        private void Pose(Vector3 position, float degrees, float scale, float along, float across, float alpha)
        {
            var t = _key.transform;
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, 0f, degrees);
            t.localScale = new Vector3(scale * along, scale * across, 1f);
            _key.enabled = true;
            _key.color = new Color(_keyColor.r, _keyColor.g, _keyColor.b, alpha);
        }

        // Ghosts at past poses, fainter with age and gone when the key is slow.
        private void Trail(Vector3 position, float angle, float scale, float speed)
        {
            for (var i = _history.Length - 1; i > 0; i--)
            {
                _history[i] = _history[i - 1];
                _historyAngle[i] = _historyAngle[i - 1];
                _historyScale[i] = _historyScale[i - 1];
            }
            _history[0] = position;
            _historyAngle[0] = angle;
            _historyScale[0] = scale;
            _historyCount = Mathf.Min(_historyCount + 1, _history.Length);
            var visible = Mathf.Clamp01(speed);
            for (var g = 0; g < _ghosts.Length; g++)
            {
                var index = (g + 1) * GhostSpacing;
                var ghost = _ghosts[g];
                if (index >= _historyCount || visible < 0.02f)
                {
                    ghost.enabled = false;
                    continue;
                }
                ghost.enabled = true;
                var t = ghost.transform;
                t.localPosition = _history[index];
                t.localRotation = Quaternion.Euler(0f, 0f, _historyAngle[index]);
                var shrink = 1f - 0.08f * (g + 1);
                t.localScale = new Vector3(_historyScale[index] * shrink, _historyScale[index] * shrink, 1f);
                ghost.color = new Color(_keyColor.r, _keyColor.g, _keyColor.b, GhostAlpha * visible * (1f - (float)g / _ghosts.Length));
            }
        }

        private void FadeTrail()
        {
            foreach (var ghost in _ghosts) ghost.enabled = false;
            _historyCount = 0;
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

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        // Cumulative speed profile: eases out of the ball and into the keyhole, fastest through the centre.
        private static void BuildProfile()
        {
            if (_profile != null) return;
            _profile = new float[ProfileSamples + 1];
            var sum = 0f;
            for (var i = 1; i <= ProfileSamples; i++)
            {
                var t = (i - 0.5f) / ProfileSamples;
                sum += Mathf.Sin(Mathf.PI * t);
                _profile[i] = sum;
            }
            for (var i = 1; i <= ProfileSamples; i++) _profile[i] /= sum;
        }

        private static float Sample(float k)
        {
            var x = Mathf.Clamp01(k) * ProfileSamples;
            var i = Mathf.Min((int)x, ProfileSamples - 1);
            return Mathf.Lerp(_profile[i], _profile[i + 1], x - i);
        }

        // Two cubic arcs meeting at the apex with one tangent; the second ends straight down onto the keyhole.
        private readonly struct Path
        {
            private readonly Vector3 _a0, _a1, _a2, _apex, _b1, _b2, _b3;

            public Path(Vector3 from, Vector3 apex, Vector3 hover, float arc)
            {
                var across = hover - from;
                var tangent = new Vector3(across.x, 0f, 0f) * 0.35f;
                _a0 = from;
                _a1 = from + Vector3.up * (arc * 0.8f);
                _a2 = apex - tangent;
                _apex = apex;
                _b1 = apex + tangent;
                _b2 = hover + Vector3.up * (arc * 0.45f);
                _b3 = hover;
            }

            public Vector3 At(float s)
            {
                if (s < 0.5f) return Cubic(_a0, _a1, _a2, _apex, s * 2f);
                return Cubic(_apex, _b1, _b2, _b3, (s - 0.5f) * 2f);
            }

            private static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                var u = 1f - t;
                return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            }
        }

        private void Finish()
        {
            var done = _onDone;
            _onDone = null;
            HideAll();
            done?.Invoke(this);
        }

        private void HideAll()
        {
            _key.enabled = _flash.enabled = false;
            _keyhole.enabled = false;
            FadeTrail();
        }

        // Cut short with no callbacks (undo, restart).
        public void Stop()
        {
            if (_play.isAlive) _play.Stop();
            _onInserted = null;
            _onDone = null;
            HideAll();
        }

        public override void OnDespawn()
        {
            Stop();
            base.OnDespawn();
        }
    }
}
