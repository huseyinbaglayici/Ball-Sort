using Project.Common.Tween;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// One unit of colored ball. The same component serves the Ball and QuestionMark prefabs:
    /// a QuestionMark unit hides its color behind a "?" until <see cref="Reveal"/> is called.
    /// The ball is the orb art (Art/Sprites/Orb), five layers under one Visual transform, which squash and smear scale.
    /// The Back and the Front (glass) are the same for every color; the other three take the ball's <see cref="OrbSkin"/>.
    /// Motion lives on separate tween targets so each can be stopped alone: the root transform (lift, flight, return)
    /// and this component (landing settle). The selection highlight is a <see cref="BallHighlight"/>, the split between
    /// two portals a <see cref="BallPortalMask"/>.
    /// </summary>
    public sealed class BallItem : Item
    {
        private const int FlyingOrderBoost = 20;
        // Longest frame the motion smear follows in full (30 fps); longer frames are hitches.
        private const float SmearFrameLimit = 1f / 30f;
        // A smeared ball never gets thinner than this share of its width.
        private const float MinSmearWidth = 0.75f;
        // How far from the mouth line, in ball diameters, a ball is squeezed by the narrow mouth.
        private const float NeckReach = 0.6f;
        private const float RevealPunch = 0.2f;

        private static readonly AnimationCurve SnapHitCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private static readonly AnimationCurve SnapReleaseCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        // Scaled by squash and smear. At scale 1 the orb's sphere is TubeArt.BallDiameter wide.
        [SerializeField] private Transform _visual;
        // Every orb layer, bottom to top, and the ones that take the ball's color.
        [SerializeField] private SpriteRenderer[] _layers;
        [SerializeField] private SpriteRenderer _outerGlow;
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _innerGlow;
        [SerializeField] private GameObject _questionMark;
        // What a hidden ball shows until it is revealed.
        [SerializeField] private OrbSkin _hiddenOrb;

        private int[] _baseOrders;
        private PrimeTween.Sequence _settle;
        // Current squash of the visual (1, 1 = round), so a new squish can start from it.
        private Vector2 _squash = Vector2.one;
        private BallHighlight _highlight;
        private BallPortalMask _portal;

        /// <summary>How a move of the ball's root ends.</summary>
        public enum Arrival
        {
            /// <summary>Just stop.</summary>
            None,
            /// <summary>The ball comes down into its slot: the landing squash and stretch.</summary>
            Land,
            /// <summary>A selected ball waits lifted: it keeps floating gently up and down until it is moved.</summary>
            Hover,
        }

        /// <summary>Palette index of the real color (also while it is hidden).</summary>
        public int ColorIndex { get; private set; }

        public bool IsHidden { get; private set; }

        private void Awake()
        {
            _baseOrders = new int[_layers.Length];
            for (var i = 0; i < _layers.Length; i++) _baseOrders[i] = _layers[i].sortingOrder;
            _highlight = new BallHighlight(transform, _layers[0].sortingLayerID);
            _portal = new BallPortalMask(transform, _visual, _layers);
        }

        public void Setup(int colorIndex, OrbSkin orb, bool hidden)
        {
            ColorIndex = colorIndex;
            IsHidden = hidden && _questionMark != null;
            SetOrb(IsHidden ? _hiddenOrb : orb);
            if (_questionMark != null) _questionMark.SetActive(IsHidden);
            transform.localScale = Vector3.one;
            ResetVisuals();
        }

        public void Reveal(OrbSkin orb)
        {
            if (!IsHidden) return;
            IsHidden = false;
            SetOrb(orb);
            if (_questionMark != null) _questionMark.SetActive(false);
            TweenPresets.Punch(transform, RevealPunch);
        }

        /// <inheritdoc cref="BallHighlight.Set"/>
        public void SetHighlight(bool on, BallAnimationConfig config) => _highlight.Set(on, config);

        /// <summary>Stops lift, flight, return and settle where they are. The highlight keeps fading.</summary>
        public void StopMotion()
        {
            PrimeTween.Tween.StopAll(transform);
            StopSettle();
            transform.localScale = Vector3.one;
        }

        /// <summary>Draws the ball above every bottle while it travels between them.</summary>
        public void SetFlying(bool flying)
        {
            var boost = flying ? FlyingOrderBoost : 0;
            for (var i = 0; i < _layers.Length; i++) _layers[i].sortingOrder = _baseOrders[i] + boost;
            _highlight.SetOrderBoost(boost);
        }

        /// <summary>
        /// Splits the ball between two portals while it passes through them (see <see cref="BallPortalMask.Split"/>).
        /// Placed by the caller so the part gone into one ring is the part out of the other. The selection highlight is
        /// hidden while split, and a "?" shows only while the ball's center is still below the source edge.
        /// </summary>
        public void SetPortalSplit(PortalView source, Vector3 exit, Vector3 exitShape, PortalView target,
            Transform exitParent)
        {
            if (!_portal.IsSplit) _highlight.Hide();
            _portal.Split(source, exit, exitShape, target, exitParent);
            var sourceEdge = source.Edge;
            if (_questionMark != null) _questionMark.SetActive(IsHidden && _visual.position.y < sourceEdge.x - sourceEdge.w);
        }

        /// <summary>The ball is all out of the target ring: it keeps the exit glow until it falls away (see
        /// <see cref="BallPortalMask.Trail"/>).</summary>
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

        /// <summary>
        /// Stretches the ball back along this frame's travel <paramref name="step"/> (area kept), so a fast flight reads
        /// as one moving shape instead of a jump. A zero step is a round ball.
        /// </summary>
        public void SetMotionSmear(Vector2 step, BallAnimationConfig config)
        {
            // A long frame (a hitch, e.g. right after a tap) would make the step huge and flash the ball wide for that
            // frame, so it never counts more than a 30 fps frame's worth of motion.
            var hitch = Time.deltaTime > SmearFrameLimit ? SmearFrameLimit / Time.deltaTime : 1f;
            step *= hitch;
            var distance = step.magnitude;
            if (distance < 1e-5f)
            {
                ApplySquash(Vector2.one);
                return;
            }
            // The front edge stays where the ball is; the tail reaches back to where it was last frame.
            var diameter = TubeArt.BallDiameter;
            var length = Mathf.Min(1f + config.SmearAmount * distance / diameter, config.MaxSmear);
            var width = Mathf.Max(1f - config.SmearThin * (length - 1f), MinSmearWidth);
            var direction = step / distance;
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            _visual.localScale = new Vector3(length, width, 1f);
            _visual.localPosition = -(Vector3)direction * ((length - 1f) * diameter * 0.5f);
        }

        /// <summary>
        /// One tiny up-and-down of a resting ball (up first, then down, dying out). Runs on the root, so any new move
        /// stops it.
        /// </summary>
        public void Bob(float height, float duration)
        {
            if (height <= 0f || duration <= 0f || PrimeTween.Tween.GetTweensCount(transform) > 0) return;
            var home = transform.localPosition;
            PrimeTween.Tween.Custom(transform, 0f, 1f, duration,
                    (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * 2f * k) * (1f - k)))
                .OnComplete(transform, t => t.localPosition = home);
        }

        /// <summary>
        /// The hard stop in the slot after the narrow mouth: one short linear hit into
        /// <see cref="BallAnimationConfig.SnapSquash"/> and an eased release with no overshoot, so the ball reads as
        /// locked in place. The bottom stays put.
        /// </summary>
        public void Snap(BallAnimationConfig config)
        {
            if (_settle.isAlive) _settle.Stop();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, config.SnapSquash, config.SnapHit, SnapHitCurve, 0f))
                .Chain(Squash(config.SnapSquash, Vector2.one, config.SnapRecover, SnapReleaseCurve, 0f));
        }

        /// <summary>
        /// Impact from a ball landing on top: a small copy of the tube squash (squash, stretch, back), scaled by
        /// <paramref name="strength"/> (1 = the full tube squash). It starts from the ball's current squash, so a second
        /// wave that arrives while one is playing continues it instead of snapping.
        /// </summary>
        public void Nudge(float strength, float delay, BallAnimationConfig config)
        {
            if (strength <= 0.001f) return;
            if (_settle.isAlive) _settle.Stop();
            var squash = Vector2.one + (config.TubeSquash - Vector2.one) * strength;
            var stretch = Vector2.one + (config.TubeStretch - Vector2.one) * strength;
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, squash, config.TubeSquashDuration, config.SquashCurve, delay))
                .Chain(Squash(squash, stretch, config.TubeStretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(stretch, Vector2.one, config.TubeRecoverDuration, config.RecoverCurve, 0f));
        }

        /// <summary>Short squash and stretch on contact. The bottom of the ball stays where it is.</summary>
        public void Settle(BallAnimationConfig config)
        {
            StopSettle();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(Vector2.one, config.SquashScale, config.SquashDuration, config.SquashCurve, config.SettleDelay))
                .Chain(Squash(config.SquashScale, config.StretchScale, config.StretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(config.StretchScale, Vector2.one, config.RecoverDuration, config.RecoverCurve, 0f));
        }

        /// <summary>
        /// Moves the ball's root to a local position from where it is now. While moving it stretches along its
        /// direction of travel like a flying ball, and it ends with the given arrival. Snaps when it is already there.
        /// </summary>
        /// <param name="anticipate">Press down first; the press is taken out of the move, so it still ends on time.</param>
        /// <param name="mouthY">The tube mouth in the same space; near it the ball is squeezed (see <see cref="NeckScale"/>).</param>
        /// <param name="overshoot">When set, the ball first goes this far past <paramref name="position"/> along the
        /// move, in <paramref name="duration"/>, then settles back onto it over <paramref name="settle"/> with a small
        /// squash (follow-through).</param>
        public void MoveLocal(Vector3 position, float duration, AnimationCurve curve, BallAnimationConfig config,
            Arrival arrival, float delay = 0f, bool anticipate = false, float mouthY = float.NaN,
            Vector3 overshoot = default, float settle = 0f)
        {
            if ((transform.localPosition - position).sqrMagnitude < 1e-6f)
            {
                transform.localPosition = position;
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
            // Near a mouth the ball keeps its squeezed shape from the first frame, so it never flashes round.
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
                            // Settling back into place: eased, with a small squash that peaks halfway.
                            var s = (time - duration) / settle;
                            point = Vector3.Lerp(peak, position, Mathf.SmoothStep(0f, 1f, s));
                            var squash = Vector2.LerpUnclamped(Vector2.one, config.LiftSettleSquash, Mathf.Sin(Mathf.PI * s));
                            squish = new Vector3(squash.x, squash.y, 1f);
                        }
                        t.localPosition = point;
                        // Passing the narrow mouth of its tube (lift, lowering, return): long and thin, as on the way in.
                        var neck = float.IsNaN(mouthY) ? Vector3.one : NeckScale(point.y, mouthY, config);
                        t.localScale = Vector3.Scale(neck, squish);
                        SetMotionSmear(point - previous, config);
                        previous = point;
                    })
                .OnComplete(this, ball =>
                {
                    // A ball that stops in the mouth (a short lift) stays squeezed there, as if passing the cap.
                    ball.transform.localScale = float.IsNaN(mouthY) ? Vector3.one : NeckScale(position.y, mouthY, config);
                    ball.Arrive(arrival, config);
                });
        }

        /// <summary>
        /// Ball scale at height <paramref name="y"/> near a tube mouth at <paramref name="mouthY"/> (same space):
        /// tightest when the center is on the mouth line, round again a little over a ball radius away from it.
        /// </summary>
        public static Vector3 NeckScale(float y, float mouthY, BallAnimationConfig config)
        {
            var tight = Mathf.Clamp01(1f - Mathf.Abs(y - mouthY) / (TubeArt.BallDiameter * NeckReach));
            return new Vector3(Mathf.Lerp(1f, config.NeckSqueeze.x, tight), Mathf.Lerp(1f, config.NeckSqueeze.y, tight), 1f);
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

        /// <summary>
        /// While selected the ball floats up and down around where it stopped, one smooth sine with no end, starting at
        /// its rest point so it never jumps. It runs on the root, so the move, a deselect or any new motion stops it
        /// (<see cref="StopMotion"/>) and continues from the floating pose.
        /// </summary>
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
            StopSettle();
            _highlight.Hide();
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
            // Keep the bottom of the ball still: move the center by half of the height change.
            _visual.localPosition = new Vector3(0f, (squash.y - 1f) * TubeArt.BallDiameter * 0.5f, 0f);
        }

        private void SetOrb(OrbSkin orb)
        {
            _outerGlow.sprite = orb.OuterGlow;
            _core.sprite = orb.Core;
            _innerGlow.sprite = orb.InnerGlow;
        }
    }
}
