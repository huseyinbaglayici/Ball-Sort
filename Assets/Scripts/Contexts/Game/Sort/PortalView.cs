using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// One warp portal over a tube, with its own life: it opens like an iris (a flat slit that widens first and then
    /// opens up, with a small overshoot), stays alive while it is open (a slow breath), and closes by swelling a little and
    /// collapsing into its center, the void swallowing itself. Its two layers (the ring art, seen from above) draw over the
    /// balls. The view is gone once it has closed or been destroyed.
    /// The ring is the art from ArtSource/Hole/make_hole.py: a thick ring 1 unit wide seen from above like the tubes,
    /// pivoted on the center of its face, flattened to <see cref="RingFlatten"/> of its width, with a side wall
    /// hanging under its near arc.
    /// A portal a ball goes into faces down instead (<see cref="FacesDown"/>): the art is turned upside down, so the ring
    /// is seen from below, its opening toward the tube under it. It draws under the ball, so the ball rises over its
    /// near rim and the lit inner wall at the bottom of the opening and is split where the black void begins: it goes
    /// into the black, never behind the ring.
    /// </summary>
    public sealed class PortalView
    {
        // Share of the opening before the slit starts to open up, so the width leads the height.
        private const float HeightLag = 0.25f;
        // The slit is never flatter than this share of the full height once it has appeared.
        private const float SlitHeight = 0.08f;
        // How much the open portal breathes, as a share of its size, and one breath in seconds.
        private const float Breath = 0.02f;
        private const float BreathPeriod = 0.9f;
        // The close first swells to this size, over this share of the close, then collapses.
        private const float CloseSwell = 1.06f;
        private const float SwellShare = 0.3f;
        // Share of the opening over which the portal fades in.
        private const float FadeInShare = 0.35f;
        // The collapse shuts the height over this share of itself, and fades out over the last FadeOutShare.
        private const float HeightCollapseShare = 0.7f;
        private const float FadeOutShare = 0.3f;

        // The ring art is flattened to this share of its width (make_hole.py FLATTEN), and so is the ellipse where its
        // plane cuts a ball.
        private const float RingFlatten = 0.45f;
        // The opening's width as a share of the ring's (make_hole.py OPENING), and how far the lit inner wall reaches
        // into it, as a share of its half height (make_hole.py DEPTH): the black void is the opening shifted that far.
        private const float Opening = 0.78f;
        private const float TunnelDepth = 0.75f;

        private readonly GameObject _root;
        private readonly Transform _parent;
        private readonly Vector3 _center;
        private readonly SpriteRenderer _back;
        private readonly SpriteRenderer _front;
        private readonly BallAnimationConfig _config;
        private readonly float _width;
        private readonly bool _facesDown;
        private PrimeTween.Tween _stage;
        private PrimeTween.Tween _alive;
        private Vector2 _shape;
        private float _alpha;

        public PortalView(Transform parent, Vector3 localCenter, BallAnimationConfig config, int order, bool facesDown)
        {
            _config = config;
            _facesDown = facesDown;
            _width = Width(config);
            _parent = parent;
            _center = localCenter;
            _root = new GameObject("WarpPortal");
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = localCenter;
            _back = Layer(config.PortalBack, order);
            _front = Layer(config.PortalFront, order + 1);
            Apply(Vector2.zero, 0f);
        }

        public bool IsGone => _root == null;

        /// <summary>The portal's size now as a share of its open size (width, height): 0 before it opens and once gone.</summary>
        public Vector2 Shape => IsGone ? Vector2.zero : _shape;

        /// <summary>Width of an open portal ring.</summary>
        public static float Width(BallAnimationConfig config) => TubeArt.BallDiameter * config.PortalWidth;

        /// <summary>
        /// How far an open ring's lowest point is under its center. Facing up, the side wall hangs under the face's near
        /// arc, so the lowest point is the bottom of that wall; facing down, it is the face's own lower arc.
        /// </summary>
        public static float Drop(BallAnimationConfig config, bool facesDown) =>
            Width(config) * (RingFlatten * config.PortalRim + (facesDown ? 0f : config.PortalWall));

        /// <summary>
        /// How far under an open ring's center a ball is split (see <see cref="Edge"/>). Facing up, at the ring's lowest
        /// edge, so the ball slips behind the ring; facing down, where the black void begins at the bottom of the
        /// opening, so the ball goes into the black right there.
        /// </summary>
        public static float Cut(BallAnimationConfig config, bool facesDown) => facesDown
            ? Width(config) * RingFlatten * config.PortalRim * Opening * (1f - TunnelDepth)
            : Drop(config, false);

        /// <summary>
        /// The ellipse whose lower arc splits the ball, in world space, at its current <see cref="Shape"/>, as (center y,
        /// center x, half width, half height): facing up, the bottom of the side wall; facing down, the black void inside
        /// the opening. The arc's middle is <see cref="Cut"/> under the ring's center once it is fully open. The mask only
        /// acts within that width, so a ring still opening hides nothing beside it: right for the ball going in, which is
        /// really there.
        /// </summary>
        public Vector4 Edge => EdgeAt(Shape);

        /// <summary>
        /// <see cref="Edge"/> as it is once the ring is fully open, whatever its shape now. The part of a ball coming out
        /// is cut on it, so nothing of it shows above the cut line, not even beside a ring still opening or closing.
        /// </summary>
        public Vector4 OpenEdge => EdgeAt(Vector2.one);

        private Vector4 EdgeAt(Vector2 shape)
        {
            var halfWidth = _width * _config.PortalRim * (_facesDown ? Opening : 1f);
            // Facing down the void sits up the opening, past the lit inner wall; facing up the edge hangs a wall lower.
            var drop = _facesDown
                ? -halfWidth * RingFlatten * TunnelDepth * shape.y
                : _width * _config.PortalWall * shape.y;
            var center = _parent.TransformPoint(new Vector3(_center.x, _center.y - drop, _center.z));
            var scale = _parent.lossyScale;
            return new Vector4(center.y, center.x, halfWidth * shape.x * scale.x, halfWidth * RingFlatten * shape.y * scale.y);
        }

        /// <summary>Opens after <paramref name="delay"/> seconds: the iris, then the breath while it stays open.</summary>
        public void Open(float delay = 0f)
        {
            if (IsGone) return;
            Stop();
            _stage = PrimeTween.Tween.Custom(_root,
                    new PrimeTween.TweenSettings<float>(0f, 1f,
                        new PrimeTween.TweenSettings(Mathf.Max(_config.PortalOpen, 0.0001f), PrimeTween.Ease.Linear, startDelay: delay)),
                    (_, t) =>
                    {
                        var width = OutBack(t);
                        var height = OutBack((t - HeightLag) / (1f - HeightLag));
                        if (t > 0f) height = Mathf.Max(height, SlitHeight * Mathf.Clamp01(t / HeightLag));
                        Apply(new Vector2(width, height), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FadeInShare)));
                    })
                .OnComplete(this, view => view.Breathe());
        }

        /// <summary>
        /// Closes after <paramref name="delay"/> seconds, from whatever shape it has then: it swells a little and
        /// collapses into its center, fading at the very end, and is gone.
        /// </summary>
        public void Close(float delay = 0f)
        {
            if (IsGone) return;
            if (delay > 0f)
            {
                // Keep the opening and the breath running until the close starts.
                PrimeTween.Tween.Delay(this, delay, view => view.Close());
                return;
            }
            Stop();
            var from = _shape;
            var fromAlpha = _alpha;
            _stage = PrimeTween.Tween.Custom(_root,
                    new PrimeTween.TweenSettings<float>(0f, 1f,
                        new PrimeTween.TweenSettings(Mathf.Max(_config.PortalClose, 0.0001f), PrimeTween.Ease.Linear)),
                    (_, t) =>
                    {
                        Vector2 shape;
                        if (t < SwellShare)
                        {
                            var s = Mathf.Sin(Mathf.PI * 0.5f * t / SwellShare);
                            shape = Vector2.Lerp(from, from * CloseSwell, s);
                        }
                        else
                        {
                            // Collapsing: the height goes first (the iris shuts to a slit), the width follows.
                            var c = (t - SwellShare) / (1f - SwellShare);
                            var width = 1f - c * c;
                            var shut = Mathf.Clamp01(c / HeightCollapseShare);
                            var height = 1f - shut * shut;
                            shape = new Vector2(from.x * CloseSwell * width, from.y * CloseSwell * height);
                        }
                        var fade = Mathf.Clamp01((t - (1f - FadeOutShare)) / FadeOutShare);
                        Apply(shape, fromAlpha * (1f - Mathf.SmoothStep(0f, 1f, fade)));
                    })
                .OnComplete(this, view => view.Destroy());
        }

        /// <summary>Removes the portal at once.</summary>
        public void Destroy()
        {
            if (IsGone) return;
            Stop();
            PrimeTween.Tween.StopAll(this);
            Object.Destroy(_root);
        }

        private void Breathe()
        {
            if (IsGone) return;
            var shape = _shape;
            _alive = PrimeTween.Tween.Custom(_root.transform,
                new PrimeTween.TweenSettings<float>(0f, 1f,
                    new PrimeTween.TweenSettings(BreathPeriod, PrimeTween.Ease.Linear, cycles: -1)),
                (_, k) => Apply(shape * (1f + Breath * Mathf.Sin(Mathf.PI * 2f * k)), _alpha));
        }

        private void Stop()
        {
            if (_stage.isAlive) _stage.Stop();
            if (_alive.isAlive) _alive.Stop();
        }

        private void Apply(Vector2 shape, float alpha)
        {
            _shape = shape;
            _alpha = alpha;
            // Facing down, the art is turned upside down: the ring seen from below.
            _root.transform.localScale = new Vector3(_width * shape.x, _width * shape.y * (_facesDown ? -1f : 1f), 1f);
            var c = _config.PortalColor;
            _back.color = _front.color = new Color(c.r, c.g, c.b, c.a * alpha);
        }

        private SpriteRenderer Layer(Sprite sprite, int order)
        {
            var go = new GameObject(sprite != null ? sprite.name : "WarpPortalLayer");
            go.transform.SetParent(_root.transform, false);
            var layer = go.AddComponent<SpriteRenderer>();
            layer.sprite = sprite;
            layer.sortingOrder = order;
            return layer;
        }

        private static float OutBack(float t) => PrimeTween.Easing.Evaluate(Mathf.Clamp01(t), PrimeTween.Ease.OutBack);
    }
}
