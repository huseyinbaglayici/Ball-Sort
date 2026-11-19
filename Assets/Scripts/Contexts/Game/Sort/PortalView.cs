using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // A ball entering a face-down portal is cut where the void begins rather than passing behind the ring.
    public sealed class PortalView
    {
        // Width leads height while opening.
        private const float HeightLag = 0.25f;
        private const float SlitHeight = 0.08f;
        private const float Breath = 0.02f;
        private const float BreathPeriod = 0.9f;
        private const float CloseSwell = 1.06f;
        private const float SwellShare = 0.3f;
        private const float FadeInShare = 0.35f;
        private const float HeightCollapseShare = 0.7f;
        private const float FadeOutShare = 0.3f;

        // Must match the ring art; the black void is the opening shifted up by TunnelDepth of its half height.
        private const float RingFlatten = 0.45f;
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

        // Current size as a share of the open size; zero before opening and once gone.
        public Vector2 Shape => IsGone ? Vector2.zero : _shape;

        public static float Width(BallAnimationConfig config) => TubeArt.BallDiameter * config.PortalWidth;

        // Depth of the ring's lowest point under its center: side-wall bottom facing up, lower face arc facing down.
        public static float Drop(BallAnimationConfig config, bool facesDown) =>
            Width(config) * (RingFlatten * config.PortalRim + (facesDown ? 0f : config.PortalWall));

        // Split depth under the ring's center: lowest edge facing up, start of the void facing down.
        public static float Cut(BallAnimationConfig config, bool facesDown) => facesDown
            ? Width(config) * RingFlatten * config.PortalRim * Opening * (1f - TunnelDepth)
            : Drop(config, false);

        // World cut ellipse (cy, cx, hw, hh); masks only within its width, so an opening ring hides nothing beside it.
        public Vector4 Edge => EdgeAt(Shape);

        // Exiting balls are cut on the open edge so nothing shows above the line while the ring animates.
        public Vector4 OpenEdge => EdgeAt(Vector2.one);

        private Vector4 EdgeAt(Vector2 shape)
        {
            var halfWidth = _width * _config.PortalRim * (_facesDown ? Opening : 1f);
            var drop = _facesDown
                ? -halfWidth * RingFlatten * TunnelDepth * shape.y
                : _width * _config.PortalWall * shape.y;
            var center = _parent.TransformPoint(new Vector3(_center.x, _center.y - drop, _center.z));
            var scale = _parent.lossyScale;
            return new Vector4(center.y, center.x, halfWidth * shape.x * scale.x, halfWidth * RingFlatten * shape.y * scale.y);
        }

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

        public void Close(float delay = 0f)
        {
            if (IsGone) return;
            if (delay > 0f)
            {
                // Don't Stop() yet: the open and breath tweens keep running until the close starts.
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
                            // Height shuts first, to a slit, then the width follows.
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
