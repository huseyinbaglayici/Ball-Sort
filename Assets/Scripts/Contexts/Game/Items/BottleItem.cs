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
        private const float CapDrop = 0.35f;
        // The last ball in the selection wave still gets at least this share of the lift time.
        private const float MinWaveShare = 0.55f;

        [SerializeField] private SpriteRenderer _back;
        [SerializeField] private SpriteRenderer _front;
        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private SpriteRenderer _mouth;
        [SerializeField] private SpriteRenderer _rimFront;
        [SerializeField] private SpriteRenderer _stopper;
        [SerializeField] private SpriteRenderer _gem;
        // Additive completion glows, tinted through their color.
        [SerializeField] private SpriteRenderer _outlineGlow;
        [SerializeField] private SpriteRenderer _capGlow;
        [SerializeField] private BoxCollider2D _collider;
        [SerializeField] private SortingGroup _sortingGroup;

        private readonly List<BallItem> _balls = new();
        private readonly List<BallItem> _lifted = new();
        private bool _inMotion;
        private bool _receiving;
        private Transform _content;
        // Body sprites with their art size and position, for stretching to other capacities.
        private readonly List<(SpriteRenderer Renderer, Vector2 Size, Vector3 Position)> _body = new();
        private Vector3 _mouthPosition;
        private Vector3 _rimFrontPosition;

        public int Index { get; private set; }
        public int Capacity { get; private set; }
        public IReadOnlyList<BallItem> Balls => _balls;

        // Extra height over the TubeArt.Slots art; set by the board.
        public float Rise { get; private set; }

        public float MouthLocalY => TubeArt.MouthLocalY + Rise;

        public Vector2 Size => new(TubeArt.Size.x, MouthLocalY);
        public int BallCount => _balls.Count;

        public int LiftedCount => _lifted.Count;

        public TubeReactions Reactions { get; private set; }

        private void Awake()
        {
            _content = new GameObject("Balls").transform;
            _content.SetParent(transform, false);
            Reactions = new TubeReactions(_back.transform.parent, _content);

            foreach (Transform child in _back.transform.parent)
                if (child.TryGetComponent<SpriteRenderer>(out var part) && part != _mouth && part != _rimFront && part != _stopper && part.sprite != null)
                    _body.Add((part, part.sprite.bounds.size, child.localPosition));
            _mouthPosition = _mouth.transform.localPosition;
            _rimFrontPosition = _rimFront.transform.localPosition;
        }

        private void LateUpdate() => Reactions.Tick();

        // rise is at least TubeArt.Rise(capacity), more when the tube spans board rows.
        public void Setup(int index, int capacity, Vector2 localPosition, float rise)
        {
            Index = index;
            Capacity = capacity;
            Rise = rise;
            _inMotion = false;
            _receiving = false;
            Reactions.Clear();
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one;
            _balls.Clear();
            _lifted.Clear();

            FitArt(rise);
            var size = Size;
            _collider.size = size;
            _collider.offset = new Vector2(0f, size.y * 0.5f);
            ShowCap(false, null);
            UpdateSorting();
        }

        // Body sprites draw sliced and stretch by rise with the bottom fixed; sprites without a border stretch whole.
        private void FitArt(float rise)
        {
            foreach (var (part, size, position) in _body)
            {
                part.drawMode = Mathf.Approximately(rise, 0f) ? SpriteDrawMode.Simple : SpriteDrawMode.Sliced;
                if (part.drawMode == SpriteDrawMode.Sliced) part.size = new Vector2(size.x, size.y + rise);
                part.transform.localPosition = position + Vector3.up * (rise * 0.5f);
            }
            _mouth.transform.localPosition = _mouthPosition + Vector3.up * rise;
            _rimFront.transform.localPosition = _rimFrontPosition + Vector3.up * rise;
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
            return top;
        }

        // The cap waits for the last ball's impact to settle. Undo removes it at once.
        public void SetCompleted(bool completed, BallAnimationConfig config)
        {
            ShowCap(completed, config, completed ? config.CompleteDelay : 0f);
            if (completed) Reactions.Complete(config);
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

        // Cap and gem drop in as the open mouth fades out; hiding restores the open mouth at once.
        private void ShowCap(bool show, BallAnimationConfig config, float delay = 0f)
        {
            var cap = _stopper.transform;
            StopCapTweens();
            var capY = TubeArt.CapLocalY + Rise;
            cap.localPosition = new Vector3(0f, capY, 0f);
            _stopper.color = _gem.color = _mouth.color = _rimFront.color = Color.white;
            _stopper.enabled = _gem.enabled = show;
            _mouth.enabled = _rimFront.enabled = true;
            _outlineGlow.enabled = _capGlow.enabled = false;
            if (!show) return;

            var fade = config.CapDuration * 0.5f;
            cap.localPosition = new Vector3(0f, capY + CapDrop, 0f);
            _stopper.color = _gem.color = new Color(1f, 1f, 1f, 0f);
            PrimeTween.Tween.LocalPositionY(cap, capY + CapDrop, capY, config.CapDuration,
                PrimeTween.Ease.OutBack, startDelay: delay);
            PrimeTween.Tween.Alpha(_stopper, 0f, 1f, fade, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(_gem, 0f, 1f, fade, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(_mouth, 1f, 0f, fade, PrimeTween.Ease.InQuad, startDelay: delay + fade);
            PrimeTween.Tween.Alpha(_rimFront, 1f, 0f, fade, PrimeTween.Ease.InQuad, startDelay: delay + fade);

            var landed = delay + config.CapDuration * 0.5f;
            Flash(_outlineGlow, config.CompleteGlowColor, 0f, config, landed);
            Flash(_capGlow, config.CapGlowColor, config.CapGlowRestAlpha, config, landed);
        }

        private static void Flash(SpriteRenderer glow, Color color, float rest, BallAnimationConfig config, float delay)
        {
            glow.enabled = true;
            glow.color = new Color(color.r, color.g, color.b, 0f);
            // Plain tweens on the renderer, not a sequence, so StopCapTweens catches them.
            PrimeTween.Tween.Alpha(glow, 0f, color.a, config.CompleteGlowIn, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(glow, color.a, rest, config.CompleteGlowOut, PrimeTween.Ease.InOutSine,
                startDelay: delay + config.CompleteGlowIn + config.CompleteGlowHold);
        }

        private void StopCapTweens()
        {
            PrimeTween.Tween.StopAll(_stopper.transform);
            PrimeTween.Tween.StopAll(_stopper);
            PrimeTween.Tween.StopAll(_gem);
            PrimeTween.Tween.StopAll(_mouth);
            PrimeTween.Tween.StopAll(_rimFront);
            PrimeTween.Tween.StopAll(_outlineGlow);
            PrimeTween.Tween.StopAll(_capGlow);
        }

        public void OnPointerClick(PointerEventData eventData) =>
            EventBus<BottleTapped>.Raise(new BottleTapped(this));

        public override void OnDespawn()
        {
            Reactions.Clear();
            PrimeTween.Tween.StopAll(transform);
            StopCapTweens();
            _stopper.enabled = _gem.enabled = _outlineGlow.enabled = _capGlow.enabled = false;
            _mouth.color = _rimFront.color = Color.white;
            _mouth.enabled = _rimFront.enabled = true;
            _inMotion = false;
            _receiving = false;
            _balls.Clear();
            _lifted.Clear();
            UpdateSorting();
        }
    }
}
