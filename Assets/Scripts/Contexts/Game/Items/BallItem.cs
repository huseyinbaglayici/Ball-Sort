using Project.Common.Tween;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Serves Ball and QuestionMark prefabs; root moves and the landing settle tween apart so each can stop alone.
    public sealed class BallItem : Item
    {
        private const int FlyingOrderBoost = 20;
        // Frames longer than 30 fps are treated as hitches by the motion smear.
        private const float SmearFrameLimit = 1f / 30f;
        // A smeared ball never gets thinner than this share of its width.
        private const float MinSmearWidth = 0.75f;
        // In ball diameters from the mouth line.
        private const float NeckReach = 0.6f;
        private const float RevealPunch = 0.2f;
        // Timed to the reveal sound: shell breaks at +531 ms, color lights up at about +700 ms.
        private const float RevealBreak = 0.531f;
        private const float RevealLight = 0.7f;
        private const float RevealShakeDegrees = 10f;
        private PrimeTween.Sequence _reveal;

        private static readonly AnimationCurve SnapHitCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private static readonly AnimationCurve SnapReleaseCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        // Scaled by squash and smear; at scale 1 the sphere is TubeArt.BallDiameter wide.
        [SerializeField] private Transform _visual;
        // All orb layers, bottom to top.
        [SerializeField] private SpriteRenderer[] _layers;
        [SerializeField] private SpriteRenderer _outerGlow;
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _coreGlow;
        [SerializeField] private SpriteRenderer _innerGlow;
        [SerializeField] private GameObject _questionMark;
        [SerializeField] private OrbSkin _hiddenOrb;

        private int[] _baseOrders;
        private Renderer _questionMarkRenderer;
        private int _questionMarkOrder;
        // Stack place sorting offset, so a ball draws whole over the one under it.
        private int _stackOffset;
        private bool _flying;
        private PrimeTween.Sequence _settle;
        // Kept so a new squish can start from the current shape.
        private Vector2 _squash = Vector2.one;
        private BallHighlight _highlight;
        private BallPortalMask _portal;

        public enum Arrival
        {
            None,
            Land,
            // Selected ball floats up and down until moved.
            Hover,
        }

        // The real color, also while hidden.
        public int ColorIndex { get; private set; }

        public bool IsHidden { get; private set; }

        private void Awake()
        {
            _baseOrders = new int[_layers.Length];
            int bottom = int.MaxValue, top = int.MinValue;
            for (var i = 0; i < _layers.Length; i++)
            {
                _baseOrders[i] = _layers[i].sortingOrder;
                bottom = Mathf.Min(bottom, _baseOrders[i]);
                top = Mathf.Max(top, _baseOrders[i]);
            }
            if (_questionMark != null)
            {
                _questionMarkRenderer = _questionMark.GetComponent<Renderer>();
                _questionMarkOrder = _questionMarkRenderer.sortingOrder;
            }
            _highlight = new BallHighlight(_visual, _layers[0].sortingLayerID, bottom, top);
            _portal = new BallPortalMask(transform, _visual, _layers);
        }

        public void Setup(int colorIndex, OrbSkin orb, Color color, bool hidden, BallAnimationConfig config)
        {
            ColorIndex = colorIndex;
            IsHidden = hidden && _questionMark != null;
            SetOrb(IsHidden ? _hiddenOrb : orb);
            _outerGlow.color = new Color(1f, 1f, 1f, config.OrbHaloAlpha);
            _highlight.SetTint(IsHidden ? Color.white : color);
            if (_questionMark != null) _questionMark.SetActive(IsHidden);
            transform.localScale = Vector3.one;
            ResetVisuals();
        }

        // Used by Undo. Only question mark balls can hide.
        public void Hide()
        {
            if (IsHidden || _questionMark == null) return;
            IsHidden = true;
            StopReveal();
            SetOrb(_hiddenOrb);
            _highlight.SetTint(Color.white);
            _questionMark.SetActive(true);
        }

        public void Reveal(OrbSkin orb, Color color)
        {
            if (!IsHidden) return;
            IsHidden = false;
            StopReveal();
            PrimeTween.Tween.ShakeLocalRotation(transform, new Vector3(0f, 0f, RevealShakeDegrees), RevealBreak);
            _reveal = PrimeTween.Sequence.Create()
                .ChainDelay(RevealBreak)
                .ChainCallback(this, ball =>
                {
                    ball.SetOrb(orb);
                    if (ball._questionMark != null) ball._questionMark.SetActive(false);
                    TweenPresets.Punch(ball.transform, RevealPunch);
                })
                .ChainDelay(RevealLight - RevealBreak)
                .ChainCallback(this, ball => ball._highlight.SetTint(color));
        }

        // Drops any pending reveal step without finishing it.
        private void StopReveal()
        {
            if (_reveal.isAlive) _reveal.Stop();
            transform.localRotation = Quaternion.identity;
        }

        public void SetHighlight(bool on, BallAnimationConfig config) => _highlight.Set(on, config);

        // The highlight keeps fading.
        public void StopMotion()
        {
            PrimeTween.Tween.StopAll(transform);
            StopSettle();
            transform.localScale = Vector3.one;
        }

        public void SetFlying(bool flying)
        {
            _flying = flying;
            ApplySorting();
        }

        // 0 = bottom; each place raises the body by BallSortingSpan so touching balls never interleave layers.
        public void SetStackIndex(int index)
        {
            _stackOffset = index * TubeArt.BallSortingSpan;
            ApplySorting();
        }

        private void ApplySorting()
        {
            var boost = _flying ? FlyingOrderBoost : 0;
            for (var i = 0; i < _layers.Length; i++)
                _layers[i].sortingOrder = _baseOrders[i] + boost + (_layers[i] == _outerGlow ? 0 : _stackOffset);
            if (_questionMarkRenderer != null) _questionMarkRenderer.sortingOrder = _questionMarkOrder + boost + _stackOffset;
            _highlight.SetOrder(boost, _stackOffset);
        }

        // Highlight is hidden while split; the "?" shows only while the center is still below the source edge.
        public void SetPortalSplit(PortalView source, Vector3 exit, Vector3 exitShape, PortalView target,
            Transform exitParent)
        {
            if (!_portal.IsSplit) _highlight.Hide();
            _portal.Split(source, exit, exitShape, target, exitParent);
            var sourceEdge = source.Edge;
            if (_questionMark != null) _questionMark.SetActive(IsHidden && _visual.position.y < sourceEdge.x - sourceEdge.w);
        }

        // Ball is fully out of the target ring but keeps the exit glow until it falls away.
        public void SetPortalTrail(Vector4 targetEdge)
        {
            _portal.Trail(targetEdge);
            if (_questionMark != null) _questionMark.SetActive(IsHidden);
        }

        public void ClearPortalCut()
        {
            if (!_portal.IsActive) return;
            _portal.Clear();
            if (_questionMark != null) _questionMark.SetActive(IsHidden);
        }

        // Stretches the ball back along this frame's travel (area kept) so a fast flight reads as one shape.
        public void SetMotionSmear(Vector2 step, BallAnimationConfig config)
        {
            // A hitch frame would flash the ball wide, so cap the step at a 30 fps frame's motion.
            var hitch = Time.deltaTime > SmearFrameLimit ? SmearFrameLimit / Time.deltaTime : 1f;
            step *= hitch;
            var distance = step.magnitude;
            if (distance < 1e-5f)
            {
                ApplySquash(Vector2.one);
                return;
            }
            // Front edge stays put; the tail reaches back to last frame's position.
            var diameter = TubeArt.BallDiameter;
            var length = Mathf.Min(1f + config.SmearAmount * distance / diameter, config.MaxSmear);
            var width = Mathf.Max(1f - config.SmearThin * (length - 1f), MinSmearWidth);
            var direction = step / distance;
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            _visual.localScale = new Vector3(length, width, 1f);
            _visual.localPosition = -(Vector3)direction * ((length - 1f) * diameter * 0.5f);
        }

        // Runs on the root, so any new move stops it.
        public void Bob(float height, float duration)
        {
            if (height <= 0f || duration <= 0f || PrimeTween.Tween.GetTweensCount(transform) > 0) return;
            var home = transform.localPosition;
            PrimeTween.Tween.Custom(transform, 0f, 1f, duration,
                    (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * 2f * k) * (1f - k)))
                .OnComplete(transform, t => t.localPosition = home);
        }

        // Hard stop in the slot: a linear hit into SnapSquash, then an eased release with no overshoot.
        public void Snap(BallAnimationConfig config)
        {
            if (_settle.isAlive) _settle.Stop();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, config.SnapSquash, config.SnapHit, SnapHitCurve, 0f))
                .Chain(Squash(config.SnapSquash, Vector2.one, config.SnapRecover, SnapReleaseCurve, 0f));
        }

        // Scaled-down tube squash (strength 1 = full); starts from the current squash so overlapping waves continue.
        public void Nudge(float strength, float delay, BallAnimationConfig config)
        {
            if (strength <= 0.001f) return;
            if (_settle.isAlive) _settle.Stop();
            var squash = Vector2.one + (config.TubeSquash - Vector2.one) * strength;
            var stretch = Vector2.one + (config.TubeStretch - Vector2.one) * strength;
            stretch.y = Mathf.Min(stretch.y, TubeArt.MaxStackStretch);
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, squash, config.TubeSquashDuration, config.SquashCurve, delay))
                .Chain(Squash(squash, stretch, config.TubeStretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(stretch, Vector2.one, config.TubeRecoverDuration, config.RecoverCurve, 0f));
        }

        public void Settle(BallAnimationConfig config)
        {
            StopSettle();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(Vector2.one, config.SquashScale, config.SquashDuration, config.SquashCurve, config.SettleDelay))
                .Chain(Squash(config.SquashScale, config.StretchScale, config.StretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(config.StretchScale, Vector2.one, config.RecoverDuration, config.RecoverCurve, 0f));
        }

        // anticipate comes out of duration; mouthY is the mouth for the neck squeeze; overshoot settles over settle.
        public void MoveLocal(Vector3 position, float duration, AnimationCurve curve, BallAnimationConfig config,
            Arrival arrival, float delay = 0f, bool anticipate = false, float mouthY = float.NaN,
            Vector3 overshoot = default, float settle = 0f, System.Action onArrive = null)
        {
            if ((transform.localPosition - position).sqrMagnitude < 1e-6f)
            {
                transform.localPosition = position;
                onArrive?.Invoke();
                return;
            }

            if (anticipate && config.AnticipationDuration > 0f)
            {
                var press = Mathf.Min(config.AnticipationDuration, duration * 0.5f);
                StopSettle();
                _settle = PrimeTween.Sequence.Create()
                    .Chain(Squash(Vector2.one, config.AnticipationSquash, press, config.SquashCurve, delay));
                delay += press;
                duration -= press;
            }

            var from = transform.localPosition;
            var previous = from;
            var peak = position + overshoot;
            settle = overshoot == Vector3.zero ? 0f : settle;
            var total = duration + settle;
            // Apply the neck squeeze on the first frame so the ball never flashes round.
            if (!float.IsNaN(mouthY)) transform.localScale = NeckScale(from.y, mouthY, config);
            PrimeTween.Tween.Custom(transform,
                    new PrimeTween.TweenSettings<float>(0f, 1f, new PrimeTween.TweenSettings(total, PrimeTween.Ease.Linear, startDelay: delay)),
                    (t, k) =>
                    {
                        var time = k * total;
                        Vector3 point;
                        var squish = Vector3.one;
                        if (time < duration || settle <= 0f)
                            point = Vector3.LerpUnclamped(from, peak, curve.Evaluate(Mathf.Clamp01(time / Mathf.Max(duration, 0.0001f))));
                        else
                        {
                            // Squash peaks halfway through the settle.
                            var s = (time - duration) / settle;
                            point = Vector3.Lerp(peak, position, Mathf.SmoothStep(0f, 1f, s));
                            var squash = Vector2.LerpUnclamped(Vector2.one, config.LiftSettleSquash, Mathf.Sin(Mathf.PI * s));
                            squish = new Vector3(squash.x, squash.y, 1f);
                        }
                        t.localPosition = point;
                        var neck = float.IsNaN(mouthY) ? Vector3.one : NeckScale(point.y, mouthY, config);
                        t.localScale = Vector3.Scale(neck, squish);
                        SetMotionSmear(point - previous, config);
                        previous = point;
                    })
                .OnComplete(this, ball =>
                {
                    // A ball that stops in the mouth (short lift) stays squeezed.
                    ball.transform.localScale = float.IsNaN(mouthY) ? Vector3.one : NeckScale(position.y, mouthY, config);
                    ball.Arrive(arrival, config);
                    // Landing sound and haptic fire on this frame.
                    onArrive?.Invoke();
                });
        }

        // Tightest with the center on the mouth line, round again a little over a radius away.
        public static Vector3 NeckScale(float y, float mouthY, BallAnimationConfig config)
        {
            var tight = Mathf.Clamp01(1f - Mathf.Abs(y - mouthY) / (TubeArt.BallDiameter * NeckReach));
            // Never taller than the stack gap allows, so lifted neighbours don't overlap.
            var height = Mathf.Min(config.NeckSqueeze.y, TubeArt.MaxStackStretch);
            return new Vector3(Mathf.Lerp(1f, config.NeckSqueeze.x, tight), Mathf.Lerp(1f, height, tight), 1f);
        }

        private void Arrive(Arrival arrival, BallAnimationConfig config)
        {
            switch (arrival)
            {
                case Arrival.Land:
                    Settle(config);
                    break;
                case Arrival.Hover:
                    ApplySquash(Vector2.one);
                    Hover(config);
                    break;
                default:
                    ApplySquash(Vector2.one);
                    break;
            }
        }

        // Endless sine starting at the rest point so it never jumps. Runs on the root, so StopMotion ends it.
        private void Hover(BallAnimationConfig config)
        {
            if (config.HoverHeight <= 0f || config.HoverPeriod <= 0f) return;
            var home = transform.localPosition;
            var height = config.HoverHeight;
            PrimeTween.Tween.Custom(transform,
                new PrimeTween.TweenSettings<float>(0f, 1f,
                    new PrimeTween.TweenSettings(config.HoverPeriod, PrimeTween.Ease.Linear, cycles: -1)),
                (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * 2f * k)));
        }

        public override void OnDespawn()
        {
            PrimeTween.Tween.StopAll(transform);
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            StopReveal();
            StopSettle();
            _highlight.Hide();
            _stackOffset = 0;
            SetFlying(false);
            ClearPortalCut();
        }

        private void StopSettle()
        {
            if (_settle.isAlive) _settle.Stop();
            ApplySquash(Vector2.one);
        }

        private PrimeTween.Tween Squash(Vector2 from, Vector2 to, float duration, AnimationCurve curve, float delay) =>
            PrimeTween.Tween.Custom(this,
                new PrimeTween.TweenSettings<float>(0f, 1f, new PrimeTween.TweenSettings(duration, curve, startDelay: delay)),
                (ball, t) => ball.ApplySquash(Vector2.LerpUnclamped(from, to, t)));

        private void ApplySquash(Vector2 squash)
        {
            _squash = squash;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = new Vector3(squash.x, squash.y, 1f);
            // Keep the bottom still.
            _visual.localPosition = new Vector3(0f, (squash.y - 1f) * TubeArt.BallDiameter * 0.5f, 0f);
        }

        private void SetOrb(OrbSkin orb)
        {
            _outerGlow.sprite = orb.OuterGlow;
            _core.sprite = orb.Core;
            _coreGlow.sprite = orb.CoreGlow;
            _innerGlow.sprite = orb.InnerGlow;
        }
    }
}
