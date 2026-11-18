using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using Project.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// A tube on the board: its art, its tap area and the stack of ball units shown inside it.
    /// It only holds views. The puzzle rules and the real contents live in SortBoard.
    /// The root has its own SortingGroup, and a Container child holds the Back sprite and the open Mouth, the contents
    /// in between, the additive Front glass, the Outline and the RimFront (the front half of the mouth) on top. A Stopper
    /// child with its Gem is the cap that replaces the open mouth when the tube is completed. The root sits at the
    /// bottom of the art (see <see cref="TubeArt"/>).
    /// Taps arrive through the camera's Physics2DRaycaster, so a Collider2D is required.
    /// The stack order always matches SortBoard right away; tweens only move the balls toward their slots.
    /// The resting balls live in a Balls layer that follows the tube's <see cref="Reactions"/>; a ball in flight hangs
    /// from the root, so the tubes it travels between never pull it around.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SortingGroup))]
    public sealed class BottleItem : Item, IPointerClickHandler
    {
        // A selected tube or one with balls flying in or out draws above the others.
        private const int RaisedGroupOrder = 10;
        // A tube receiving balls draws above the tube they come from, so a flying ball never passes behind its origin.
        private const int ReceivingGroupOrder = 11;
        // The completed cap drops onto the mouth from this far above, in board units.
        private const float CapDrop = 0.35f;
        // In the selection wave the last ball still lifts over at least this share of the lift time.
        private const float MinWaveShare = 0.55f;

        [SerializeField] private SpriteRenderer _back;
        [SerializeField] private SpriteRenderer _front;
        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private SpriteRenderer _mouth;
        [SerializeField] private SpriteRenderer _rimFront;
        [SerializeField] private SpriteRenderer _stopper;
        [SerializeField] private SpriteRenderer _gem;
        [SerializeField] private BoxCollider2D _collider;
        [SerializeField] private SortingGroup _sortingGroup;

        private readonly List<BallItem> _balls = new();
        private readonly List<BallItem> _lifted = new();
        private bool _inMotion;
        private bool _receiving;
        private Transform _content;

        public int Index { get; private set; }
        public int Capacity { get; private set; }
        public IReadOnlyList<BallItem> Balls => _balls;
        public int BallCount => _balls.Count;

        /// <summary>The tube's squash and shake reactions; they move the art and the resting balls together.</summary>
        public TubeReactions Reactions { get; private set; }

        private void Awake()
        {
            _content = new GameObject("Balls").transform;
            _content.SetParent(transform, false);
            Reactions = new TubeReactions(_back.transform.parent, _content);
        }

        private void LateUpdate() => Reactions.Tick();

        public void Setup(int index, int capacity, Vector2 localPosition)
        {
            if (capacity != TubeArt.Slots)
                Debug.LogWarning($"[BottleItem] The tube art holds {TubeArt.Slots} balls; this level uses capacity {capacity}.");

            Index = index;
            Capacity = capacity;
            _inMotion = false;
            _receiving = false;
            Reactions.Clear();
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one;
            _balls.Clear();
            _lifted.Clear();

            var size = TubeArt.Size;
            _collider.size = size;
            _collider.offset = new Vector2(0f, size.y * 0.5f);
            ShowCap(false, null);
            UpdateSorting();
        }

        /// <summary>
        /// Keeps the tube above the others while it is selected or has balls flying in or out, so lifted and flying
        /// balls are never drawn behind a neighbouring tube.
        /// </summary>
        public void SetInMotion(bool inMotion, bool receiving = false)
        {
            _inMotion = inMotion;
            _receiving = inMotion && receiving;
            UpdateSorting();
        }

        public Vector3 SlotWorldPosition(int slot) => transform.TransformPoint(TubeArt.SlotLocalPosition(slot));

        public int IndexOf(BallItem ball) => _balls.IndexOf(ball);

        /// <summary>Adds the unit on top of the stack. With snap it is placed in its slot at once.</summary>
        public void Push(BallItem ball, bool snap)
        {
            var slot = _balls.Count;
            _balls.Add(ball);
            ball.transform.SetParent(_content, !snap);
            if (snap) ball.transform.localPosition = TubeArt.SlotLocalPosition(slot);
        }

        /// <summary>Takes a ball of this tube back into the resting layer, where it is, after a flight.</summary>
        public void Adopt(BallItem ball)
        {
            if (ball.transform.parent != _content) ball.transform.SetParent(_content, true);
        }

        public BallItem Pop()
        {
            var top = _balls[_balls.Count - 1];
            _balls.RemoveAt(_balls.Count - 1);
            _lifted.Remove(top);
            return top;
        }

        /// <summary>
        /// A completed bottle is locked by the rules; this only shows it. Once the last ball's impact has settled the cap
        /// drops on the mouth and the tube gives a small victory hop. Undo takes the cap away at once.
        /// </summary>
        public void SetCompleted(bool completed, BallAnimationConfig config)
        {
            ShowCap(completed, config, completed ? config.CompleteDelay : 0f);
            if (completed) Reactions.Complete(config);
        }

        /// <summary>
        /// Highlights the top <paramref name="count"/> balls all at once, so the whole group reads as selected on the
        /// first frame, then lifts them one after another, top ball first. Every ball rises by the same short distance
        /// from its slot, wherever the stack ends, so a selection always reads the same; the long way up to the portal
        /// belongs to the move. With <see cref="BallAnimationConfig.LiftToMouth"/> (experimental) the top ball instead
        /// always goes to the same spot at the mouth, from any depth. The group keeps its stack spacing. Each ball starts
        /// from where it is now.
        /// </summary>
        public void Select(int count, BallAnimationConfig config)
        {
            Deselect(config);
            count = Mathf.Min(count, _balls.Count);
            if (count <= 0) return;

            var top = _balls.Count - 1;
            // Every lifted ball moves the same way, so they stay in line; near the mouth the ball squeezes as it passes.
            var rise = TubeArt.SlotHeight * config.LiftHeight;
            // Experimental: the top ball goes to the mouth spot (where a full tube's top ball lifts to), from any depth.
            if (config.LiftToMouth) rise += TubeArt.SlotLocalPosition(TubeArt.Slots - 1).y - TubeArt.SlotLocalPosition(top).y;
            var overshoot = Vector3.up * (TubeArt.SlotHeight * config.LiftOvershoot);
            var duration = config.LiftDuration(rise + overshoot.y);
            // The whole wave ends when the top ball does: a later ball starts later and lifts faster. The stagger is
            // compressed so the last ball still gets at least MinWaveShare of the lift time.
            var lifting = 0;
            while (lifting < count && !_balls[top - lifting].IsHidden) lifting++;
            var lastDelay = config.WaveDelay(lifting - 1);
            var compress = lastDelay > 0f ? Mathf.Min(1f, duration * (1f - MinWaveShare) / lastDelay) : 1f;
            for (var i = 0; i < lifting; i++)
            {
                var slot = top - i;
                var ball = _balls[slot];
                var delay = config.WaveDelay(i) * compress;
                _lifted.Add(ball);
                ball.SetHighlight(true, config);
                ball.StopMotion();
                ball.MoveLocal(TubeArt.SlotLocalPosition(slot) + Vector3.up * rise, duration - delay, config.LiftCurve, config,
                    BallItem.Arrival.Hover, delay, anticipate: true, mouthY: TubeArt.MouthLocalY,
                    overshoot: overshoot, settle: config.LiftSettleDuration);
            }
            // The balls that stay in the tube feel the tap: one tiny up-and-down.
            for (var slot = 0; slot <= top - _lifted.Count; slot++) _balls[slot].Bob(config.TapBob, config.TapBobDuration);
            if (_lifted.Count > 0) Reactions.Select(config);
            UpdateSorting();
        }

        /// <summary>Lowers every lifted ball back to its slot with the deselect timing.</summary>
        public void Deselect(BallAnimationConfig config) =>
            LowerLifted(config, config.LowerDuration, config.LowerCurve);

        /// <summary>Sends the balls left lifted after a partial transfer back to their slots.</summary>
        public void ReturnLifted(BallAnimationConfig config) =>
            LowerLifted(config, config.ReturnDuration, config.ReturnCurve);

        private void LowerLifted(BallAnimationConfig config, System.Func<float, float> durationFor, AnimationCurve curve)
        {
            var back = 0f;
            foreach (var ball in _lifted)
            {
                ball.StopMotion();
                ball.SetHighlight(false, config);
                var home = TubeArt.SlotLocalPosition(_balls.IndexOf(ball));
                var duration = durationFor(Mathf.Abs(ball.transform.localPosition.y - home.y));
                back = Mathf.Max(back, duration);
                ball.MoveLocal(home, duration, curve, config, BallItem.Arrival.Land, mouthY: TubeArt.MouthLocalY);
            }
            if (_lifted.Count > 0) Reactions.Catch(config, back);
            _lifted.Clear();
            UpdateSorting();
        }

        private void UpdateSorting() =>
            _sortingGroup.sortingOrder = _receiving ? ReceivingGroupOrder : _inMotion || _lifted.Count > 0 ? RaisedGroupOrder : 0;

        /// <summary>
        /// The completed cap and its gem (the gem is a child of the cap and shares its canvas): after
        /// <paramref name="delay"/> they drop onto the mouth, fading in on the way, and settle with a small overshoot,
        /// while the open mouth fades out under them. Hiding restores the open mouth at once.
        /// </summary>
        private void ShowCap(bool show, BallAnimationConfig config, float delay = 0f)
        {
            var cap = _stopper.transform;
            StopCapTweens();
            cap.localPosition = new Vector3(0f, TubeArt.CapLocalY, 0f);
            _stopper.color = _gem.color = _mouth.color = _rimFront.color = Color.white;
            _stopper.enabled = _gem.enabled = show;
            _mouth.enabled = _rimFront.enabled = true;
            if (!show) return;

            var fade = config.CapDuration * 0.5f;
            cap.localPosition = new Vector3(0f, TubeArt.CapLocalY + CapDrop, 0f);
            _stopper.color = _gem.color = new Color(1f, 1f, 1f, 0f);
            PrimeTween.Tween.LocalPositionY(cap, TubeArt.CapLocalY + CapDrop, TubeArt.CapLocalY, config.CapDuration,
                PrimeTween.Ease.OutBack, startDelay: delay);
            PrimeTween.Tween.Alpha(_stopper, 0f, 1f, fade, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(_gem, 0f, 1f, fade, PrimeTween.Ease.OutQuad, startDelay: delay);
            PrimeTween.Tween.Alpha(_mouth, 1f, 0f, fade, PrimeTween.Ease.InQuad, startDelay: delay + fade);
            PrimeTween.Tween.Alpha(_rimFront, 1f, 0f, fade, PrimeTween.Ease.InQuad, startDelay: delay + fade);
        }

        private void StopCapTweens()
        {
            PrimeTween.Tween.StopAll(_stopper.transform);
            PrimeTween.Tween.StopAll(_stopper);
            PrimeTween.Tween.StopAll(_gem);
            PrimeTween.Tween.StopAll(_mouth);
            PrimeTween.Tween.StopAll(_rimFront);
        }

        public void OnPointerClick(PointerEventData eventData) =>
            EventBus<BottleTapped>.Raise(new BottleTapped(this));

        public override void OnDespawn()
        {
            Reactions.Clear();
            PrimeTween.Tween.StopAll(transform);
            StopCapTweens();
            _stopper.enabled = _gem.enabled = false;
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
