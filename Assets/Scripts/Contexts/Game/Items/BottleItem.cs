using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using Project.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Project.Contexts.Game.Items
{
    // Order always matches SortBoard; tweens only move balls. Flying balls hang from the root so tubes never drag.
    [RequireComponent(typeof(BoxCollider2D), typeof(SortingGroup))]
    public sealed class BottleItem : Item, IPointerClickHandler
    {
        private const int RaisedGroupOrder = 10;
        // Above the source tube, so a flying ball never passes behind its origin.
        private const int ReceivingGroupOrder = 11;
        // The last ball in the selection wave still gets at least this share of the lift time.
        private const float MinWaveShare = 0.55f;
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");

        [SerializeField] private SpriteRenderer _back;
        [SerializeField] private SpriteRenderer _front;
        [SerializeField] private SpriteRenderer _frontBehind;
        [SerializeField] private SpriteRenderer _glow;
        // Additive copy of the glow, tinted through its color.
        [SerializeField] private SpriteRenderer _glowFlash;
        [SerializeField] private SpriteRenderer _emptyCap;
        // Front half of the empty cap, drawn over the balls.
        [SerializeField] private SpriteRenderer _rimFront;
        [SerializeField] private SpriteRenderer _completedCap;
        [SerializeField] private SpriteRenderer _gem;
        [SerializeField] private BoxCollider2D _collider;
        [SerializeField] private SortingGroup _sortingGroup;

        private readonly List<BallItem> _balls = new();
        private readonly List<BallItem> _lifted = new();
        private bool _inMotion;
        private bool _receiving;
        private bool _completed;
        private Transform _content;
        // Body sprites with their art size and position, for stretching to other capacities.
        private readonly List<(SpriteRenderer Renderer, Vector2 Size, Vector3 Position)> _body = new();
        private Vector3 _emptyCapPosition;
        private BallAnimationConfig _config;
        // Tween target for the completion's scheduled ball nudges and hops, so undo stops them together.
        private readonly object _momentClock = new();

        public int Index { get; private set; }
        public int Capacity { get; private set; }
        public IReadOnlyList<BallItem> Balls => _balls;

        // Follows the cap through its pop, slam and the tube's hop.
        public Transform Gem => _gem.transform;

        // Extra height over the TubeArt.Slots art; set by the board.
        public float Rise { get; private set; }

        public float MouthLocalY => TubeArt.MouthLocalY + Rise;

        public Vector2 Size => new(TubeArt.Size.x, MouthLocalY);
        public int BallCount => _balls.Count;

        public int LiftedCount => _lifted.Count;

        public TubeReactions Reactions { get; private set; }

        // The art's center; tube-sized overlays parent here so they share the tube's reactions and sorting.
        public Transform Art => _back.transform.parent;

        // Hidden under a shutter, whose edge it would otherwise peek past.
        public void SetGlowVisible(bool visible) => _glow.enabled = visible;

        private void Awake()
        {
            _content = new GameObject("Balls").transform;
            _content.SetParent(transform, false);
            Reactions = new TubeReactions(_back.transform.parent, _content);

            foreach (var part in new[] { _back, _front, _frontBehind, _glow, _glowFlash })
                _body.Add((part, part.sprite.bounds.size, part.transform.localPosition));
            _emptyCapPosition = _emptyCap.transform.localPosition;
            SetUvRect(_front);
            SetUvRect(_frontBehind);
        }

        // The glass shader needs the sprite's own uv rect to find its place across the width, whatever the atlas.
        private static void SetUvRect(SpriteRenderer glass)
        {
            var uv = glass.sprite.uv;
            Vector2 min = uv[0], max = uv[0];
            foreach (var p in uv)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            var block = new MaterialPropertyBlock();
            glass.GetPropertyBlock(block);
            block.SetVector(UvRectId, new Vector4(min.x, min.y, 1f / (max.x - min.x), 1f / (max.y - min.y)));
            glass.SetPropertyBlock(block);
        }

        private void LateUpdate() => Reactions.Tick();

        // rise is at least TubeArt.Rise(capacity), more when the tube spans board rows.
        public void Setup(int index, int capacity, Vector2 localPosition, float rise, BallAnimationConfig config)
        {
            _config = config;
            Index = index;
            Capacity = capacity;
            Rise = rise;
            _inMotion = false;
            _receiving = false;
            _completed = false;
            Reactions.Clear();
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one;
            _balls.Clear();
            _lifted.Clear();

            FitArt(rise);
            var size = Size;
            _collider.size = size;
            _collider.offset = new Vector2(0f, size.y * 0.5f);
            ShowCap(false, false);
            UpdateSorting();
        }

        // Glass sprites draw sliced and stretch by rise with the bottom fixed; the end piece stays, the cap rides up.
        private void FitArt(float rise)
        {
            foreach (var (part, size, position) in _body)
            {
                // Simple mode turns a sliced size into scale; restore the size first or a pooled tube stays tall.
                if (Mathf.Approximately(rise, 0f))
                {
                    part.size = size;
                    part.drawMode = SpriteDrawMode.Simple;
                }
                else
                {
                    part.drawMode = SpriteDrawMode.Sliced;
                    part.size = new Vector2(size.x, size.y + rise);
                }
                part.transform.localScale = Vector3.one;
                part.transform.localPosition = position + Vector3.up * (rise * 0.5f);
            }
            _emptyCap.transform.localPosition = _emptyCapPosition + Vector3.up * rise;
            _rimFront.transform.localPosition = _emptyCap.transform.localPosition;
        }

        // Raised so lifted and flying balls are never drawn behind a neighbouring tube.
        public void SetInMotion(bool inMotion, bool receiving = false)
        {
            _inMotion = inMotion;
            _receiving = inMotion && receiving;
            UpdateSorting();
        }

        public Vector3 SlotWorldPosition(int slot) => transform.TransformPoint(TubeArt.SlotLocalPosition(slot));

        public int IndexOf(BallItem ball) => _balls.IndexOf(ball);

        public void Push(BallItem ball, bool snap)
        {
            var slot = _balls.Count;
            _balls.Add(ball);
            ball.SetStackIndex(slot);
            Enter(ball);
            if (snap) ball.transform.localPosition = TubeArt.SlotLocalPosition(slot);
        }

        // Reparents a ball back into the resting layer after a flight.
        public void Adopt(BallItem ball)
        {
            if (ball.transform.parent != _content) Enter(ball);
        }

        // Keeps local scale: the layer may be mid-squash, and keeping world size would bake the inverse squash in.
        private void Enter(BallItem ball)
        {
            var shape = ball.transform.localScale;
            ball.transform.SetParent(_content, true);
            ball.transform.localScale = shape;
        }

        public BallItem Pop()
        {
            var top = _balls[_balls.Count - 1];
            _balls.RemoveAt(_balls.Count - 1);
            _lifted.Remove(top);
            // A ball leaving mid-finish (Undo) gets its glow and shape back at once.
            top.StopDrain();
            return top;
        }

        // celebrate plays the charge, hop and cap pop; otherwise the cap is set at once (undo, level start).
        public void SetCompleted(bool completed, bool celebrate)
        {
            StopMoment();
            _completed = completed;
            ShowCap(completed, celebrate);
            if (!completed) Reactions.Clear();
            else if (celebrate)
            {
                Reactions.Complete(_config);
                PlayMoment();
            }
        }

        // Orbs exhale bottom first toward the finish; their glow dims and returns with the ball wave after the slam.
        private void PlayMoment()
        {
            var c = _config;
            var f = c.Finish;
            var slam = c.CompleteChargeDuration + c.CapPopDuration + c.CapSlamDuration;
            for (var i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];
                var startAt = f.DrainStart(i, _balls.Count);
                var hopAt = slam + c.BallWaveDelay + c.BallWaveStagger * i;
                ball.Exhale(startAt, f.ExhaleTime(_balls.Count), f.ExhaleRecover, f.ExhaleShrink, f.ExhaleStretch, f.ExhaleDrift);
                ball.DrainGlow(startAt + f.WispTime * f.SoulPeel, f.DrainTime, f.DrainShare, hopAt, f.GlowReturnTime);
                PrimeTween.Tween.Delay(_momentClock, hopAt, _ =>
                {
                    ball.Hop(c.BallWaveHop, c.BallWaveDuration, 0f);
                    ball.Nudge(Mathf.Min(1.5f, c.BallWaveLand), c.BallWaveDuration, c);
                });
            }
        }

        private void StopMoment()
        {
            foreach (var ball in _balls) ball.StopDrain();
            PrimeTween.Tween.StopAll(_momentClock);
        }

        // Lifts top first, each the same short distance; liftDelays gets each ball's start delay for lift sounds.
        public void Select(int count, BallAnimationConfig config, List<float> liftDelays = null)
        {
            Deselect(config);
            count = Mathf.Min(count, _balls.Count);
            if (count <= 0) return;

            var top = _balls.Count - 1;
            var rise = TubeArt.SlotHeight * config.LiftHeight;
            // Fixed lift: the top ball always goes to the same spot at the mouth, from any depth.
            if (config.LiftToMouth) rise += TubeArt.SlotLocalPosition(Capacity - 1).y - TubeArt.SlotLocalPosition(top).y;
            var overshoot = Vector3.up * (TubeArt.SlotHeight * config.LiftOvershoot);
            var duration = config.LiftDuration(rise + overshoot.y);
            // The wave ends with the top ball; the stagger is compressed so the last ball still gets MinWaveShare.
            var lifting = 0;
            while (lifting < count && !_balls[top - lifting].IsHidden) lifting++;
            var lastDelay = config.WaveDelay(lifting - 1);
            var compress = lastDelay > 0f ? Mathf.Min(1f, duration * (1f - MinWaveShare) / lastDelay) : 1f;
            var spacing = TubeArt.LiftedSpacing(config.GlowScale);
            for (var i = 0; i < lifting; i++)
            {
                var slot = top - i;
                var ball = _balls[slot];
                var delay = config.WaveDelay(i) * compress;
                liftDelays?.Add(delay);
                _lifted.Add(ball);
                ball.SetHighlight(true, config);
                ball.StopMotion();
                var lifted = TubeArt.SlotLocalPosition(top) + Vector3.up * (rise - spacing * i);
                ball.MoveLocal(lifted, duration - delay, config.LiftCurve, config,
                    BallItem.Arrival.Hover, delay, anticipate: true, mouthY: MouthLocalY,
                    overshoot: overshoot, settle: config.LiftSettleDuration);
            }
            // Balls left in the tube feel the tap.
            for (var slot = 0; slot <= top - _lifted.Count; slot++) _balls[slot].Bob(config.TapBob, config.TapBobDuration);
            if (_lifted.Count > 0) Reactions.Select(config);
            UpdateSorting();
        }

        // Bottom ball lands first. onContact(index, count) runs as each one lands, in landing order.
        public void Deselect(BallAnimationConfig config, System.Action<int, int> onContact = null) =>
            LowerLifted(config, config.LowerDuration, config.LowerCurve, onContact, config.ReturnContactStagger);

        // Silent return of balls left lifted after a partial transfer.
        public void ReturnLifted(BallAnimationConfig config) =>
            LowerLifted(config, config.ReturnDuration, config.ReturnCurve);

        private void LowerLifted(BallAnimationConfig config, System.Func<float, float> durationFor, AnimationCurve curve,
            System.Action<int, int> onContact = null, float stagger = 0f)
        {
            var back = 0f;
            var count = _lifted.Count;
            // _lifted is top ball first; the bottom ball lands first.
            for (var k = 0; k < count; k++)
            {
                var ball = _lifted[count - 1 - k];
                ball.StopMotion();
                ball.SetHighlight(false, config);
                var home = TubeArt.SlotLocalPosition(_balls.IndexOf(ball));
                var duration = durationFor(Mathf.Abs(ball.transform.localPosition.y - home.y));
                var delay = k * stagger;
                back = Mathf.Max(back, delay + duration);
                var index = k;
                ball.MoveLocal(home, duration, curve, config, BallItem.Arrival.Land, delay, mouthY: MouthLocalY,
                    onArrive: onContact == null ? null : () => onContact(index, count));
            }
            if (_lifted.Count > 0) Reactions.Catch(config, back);
            _lifted.Clear();
            UpdateSorting();
        }

        private void UpdateSorting() =>
            _sortingGroup.sortingOrder = _receiving ? ReceivingGroupOrder : _inMotion || _lifted.Count > 0 ? RaisedGroupOrder : 0;

        // After the charge the empty cap fades as the completed cap pops up, slams onto the mouth and settles.
        private void ShowCap(bool show, bool celebrate)
        {
            var cap = _completedCap.transform;
            var wasShown = _completedCap.enabled;
            StopCapTweens();
            var capY = TubeArt.CapLocalY + Rise;
            cap.localPosition = new Vector3(0f, capY, 0f);
            cap.localScale = Vector3.one;
            _completedCap.color = _gem.color = _emptyCap.color = _rimFront.color = Color.white;
            _completedCap.enabled = _gem.enabled = show;
            _emptyCap.enabled = _rimFront.enabled = !show || celebrate;
            _glowFlash.enabled = false;
            SetTint(show ? Color.white : _config.GlassRestTint);
            if (!show && celebrate && wasShown) Uncap(capY);
            if (!show || !celebrate) return;

            var c = _config;
            SetTint(c.GlassRestTint);
            SetTint(Color.white, c.GlassCompleteBrighten);

            var delay = c.CompleteChargeDuration;
            var landed = delay + c.CapPopDuration + c.CapSlamDuration;
            PrimeTween.Tween.Alpha(_emptyCap, 1f, 0f, c.CapFadeIn, PrimeTween.Ease.InQuad, startDelay: delay)
                .OnComplete(_emptyCap, empty => empty.enabled = false);
            PrimeTween.Tween.Alpha(_rimFront, 1f, 0f, c.CapFadeIn, PrimeTween.Ease.InQuad, startDelay: delay)
                .OnComplete(_rimFront, rim => rim.enabled = false);

            _completedCap.color = _gem.color = new Color(1f, 1f, 1f, 0f);
            cap.localPosition = new Vector3(0f, capY + c.CapPopStart, 0f);
            cap.localScale = Scale(c.CapPopScale);
            PrimeTween.Tween.Alpha(_completedCap, 0f, 1f, c.CapFadeIn, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(_gem, 0f, 1f, c.CapFadeIn, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.LocalPositionY(cap, capY + c.CapPopStart, capY + c.CapPopPeak, c.CapPopDuration,
                PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.LocalPositionY(cap, capY + c.CapPopPeak, capY, c.CapSlamDuration,
                PrimeTween.Ease.InQuad, startDelay: delay + c.CapPopDuration);
            // Tall through the flight, squash on the slam, then settle.
            PrimeTween.Tween.Scale(cap, Scale(c.CapPopScale), Scale(c.CapStretch), c.CapStretchIn,
                PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Scale(cap, Scale(c.CapStretch), Scale(c.CapSquash), c.CapSquashIn,
                PrimeTween.Ease.OutQuad, startDelay: landed);
            PrimeTween.Tween.Scale(cap, Scale(c.CapSquash), Vector3.one, c.CapSettle,
                PrimeTween.Ease.InOutSine, startDelay: landed + c.CapSquashIn);

            Flash(_glowFlash, c.CompleteGlowColor, c, landed);
        }

        // Undo of a completing move: the cap lifts off and fades as the open rim and the resting glass come back.
        private void Uncap(float capY)
        {
            var c = _config;
            var time = Mathf.Max(c.Boosters.UncapTime, 0.0001f);
            var cap = _completedCap.transform;
            _completedCap.enabled = _gem.enabled = true;
            PrimeTween.Tween.LocalPositionY(cap, capY, capY + c.Boosters.UncapLift, time, PrimeTween.Ease.OutQuad);
            PrimeTween.Tween.Scale(cap, Vector3.one, Scale(c.CapPopScale), time, PrimeTween.Ease.InQuad);
            PrimeTween.Tween.Alpha(_completedCap, 1f, 0f, time, PrimeTween.Ease.InQuad)
                .OnComplete(_completedCap, done => done.enabled = false);
            PrimeTween.Tween.Alpha(_gem, 1f, 0f, time, PrimeTween.Ease.InQuad)
                .OnComplete(_gem, done => done.enabled = false);
            PrimeTween.Tween.Alpha(_emptyCap, 0f, 1f, time, PrimeTween.Ease.OutQuad);
            PrimeTween.Tween.Alpha(_rimFront, 0f, 1f, time, PrimeTween.Ease.OutQuad);
            SetTint(Color.white);
            SetTint(c.GlassRestTint, time);
        }

        private static Vector3 Scale(Vector2 xy) => new(xy.x, xy.y, 1f);

        // Level won: the gem punches up and settles while the cap's glow flashes again.
        public void PulseCap(float delay, float scale, float time)
        {
            if (!_completed) return;
            var gem = _gem.transform;
            PrimeTween.Tween.StopAll(gem);
            gem.localScale = Vector3.one;
            PrimeTween.Tween.Scale(gem, Vector3.one, Vector3.one * scale, Mathf.Max(time * 0.35f, 0.0001f),
                    PrimeTween.Ease.OutQuad, startDelay: delay)
                .Chain(PrimeTween.Tween.Scale(gem, Vector3.one * scale, Vector3.one, Mathf.Max(time * 0.65f, 0.0001f),
                    PrimeTween.Ease.OutBack));
            Flash(_glowFlash, _config.CompleteGlowColor, _config, delay);
        }

        private static void Flash(SpriteRenderer glow, Color color, BallAnimationConfig config, float delay)
        {
            glow.enabled = true;
            glow.color = new Color(color.r, color.g, color.b, 0f);
            // Plain tweens on the renderer, not a sequence, so StopCapTweens catches them.
            PrimeTween.Tween.Alpha(glow, 0f, color.a, config.CompleteGlowIn, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(glow, color.a, 0f, config.CompleteGlowOut, PrimeTween.Ease.InOutSine,
                startDelay: delay + config.CompleteGlowIn + config.CompleteGlowHold);
        }

        // Sole writer of the front glass tint, set at once or eased from its current value.
        public void SetTint(Color tint, float duration = 0f)
        {
            PrimeTween.Tween.StopAll(_front);
            PrimeTween.Tween.StopAll(_frontBehind);
            var front = Glass(tint, TubeArt.GlassOverBalls);
            var behind = Glass(tint, 1f - TubeArt.GlassOverBalls);
            if (duration <= 0f)
            {
                _front.color = front;
                _frontBehind.color = behind;
                return;
            }
            if (_front.color == front) return;
            PrimeTween.Tween.Color(_front, front, duration, PrimeTween.Ease.OutQuad);
            PrimeTween.Tween.Color(_frontBehind, behind, duration, PrimeTween.Ease.OutQuad);
        }

        // Back to its own tint: white once completed, the rest tint otherwise.
        public void ResetTint(float duration) => SetTint(_completed ? Color.white : _config.GlassRestTint, duration);

        private static Color Glass(Color tint, float share) => new(tint.r, tint.g, tint.b, share);

        private void StopCapTweens()
        {
            PrimeTween.Tween.StopAll(_completedCap.transform);
            PrimeTween.Tween.StopAll(_completedCap);
            PrimeTween.Tween.StopAll(_gem);
            PrimeTween.Tween.StopAll(_gem.transform);
            _gem.transform.localScale = Vector3.one;
            PrimeTween.Tween.StopAll(_emptyCap);
            PrimeTween.Tween.StopAll(_rimFront);
            PrimeTween.Tween.StopAll(_glowFlash);
            PrimeTween.Tween.StopAll(_front);
            PrimeTween.Tween.StopAll(_frontBehind);
        }

        public void OnPointerClick(PointerEventData eventData) =>
            EventBus<BottleTapped>.Raise(new BottleTapped(this));

        public override void OnDespawn()
        {
            StopMoment();
            Reactions.Clear();
            PrimeTween.Tween.StopAll(transform);
            StopCapTweens();
            _completedCap.enabled = _gem.enabled = _glowFlash.enabled = false;
            _emptyCap.color = _rimFront.color = Color.white;
            _emptyCap.enabled = _rimFront.enabled = true;
            _glow.enabled = true;
            _completedCap.transform.localScale = Vector3.one;
            _inMotion = false;
            _receiving = false;
            _balls.Clear();
            _lifted.Clear();
            UpdateSorting();
        }
    }
}
