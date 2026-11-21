using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Ring round a new tube's foot on touchdown: violet glow and a thin bright line, the far half behind the tube.
    public sealed class TouchRingView
    {
        // Must match the ring art: the line runs at this share of the sprite's half width.
        private const float LineRadius = 0.84f;
        private const float GlintFarAlpha = 0.45f;

        private readonly GameObject _root;
        private readonly SpriteRenderer[] _lines;
        private readonly SpriteRenderer[] _glows;
        private readonly SpriteRenderer _glint;
        private readonly BoosterFxSettings _s;
        private readonly Vector3 _size;
        private readonly float _native;
        private readonly int _nearOrder;
        private readonly int _farOrder;

        public TouchRingView(Transform parent, Vector3 localCenter, BoosterFxSettings settings, int nearOrder, int farOrder)
        {
            _s = settings;
            _nearOrder = nearOrder;
            _farOrder = farOrder;
            _root = new GameObject("TouchRing");
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = localCenter;
            _glows = new[] { Layer(settings.TouchGlowFar, farOrder - 1), Layer(settings.TouchGlowNear, nearOrder) };
            _lines = new[] { Layer(settings.TouchLineFar, farOrder), Layer(settings.TouchLineNear, nearOrder + 1) };
            _glint = Layer(settings.Glint, nearOrder + 2);
            _native = settings.TouchLineNear != null ? settings.TouchLineNear.bounds.size.x : 1f;
            _size = new Vector3(settings.TouchRingWidth / _native, settings.TouchRingWidth * settings.TouchRingAspect / _native, 1f);
            if (settings.Glint != null)
                _glint.transform.localScale = Vector3.one * (settings.GlintSize / settings.Glint.bounds.size.x);
            Apply(0f, 1f, 0f);
        }

        public bool IsGone => _root == null;

        // Opens quickly, tightens as follow-through while it fades; the glint runs once round from the near side.
        public void Play()
        {
            var total = Mathf.Max(_s.TouchRingTime, 0.0001f);
            var open = Mathf.Clamp01(_s.TouchRingOpen / total);
            PrimeTween.Tween.Custom(_root.transform, 0f, 1f, total, (_, t) =>
                {
                    var alpha = t < open ? Mathf.SmoothStep(0f, 1f, t / open) : 1f - Mathf.SmoothStep(0f, 1f, (t - open) / (1f - open));
                    var tighten = PrimeTween.Easing.Evaluate(t, PrimeTween.Ease.OutCubic);
                    Apply(alpha, Mathf.Lerp(1f, _s.TouchRingTighten, tighten), t);
                }, PrimeTween.Ease.Linear)
                .OnComplete(this, view => view.Destroy());
        }

        public void Destroy()
        {
            if (IsGone) return;
            PrimeTween.Tween.StopAll(_root.transform);
            Object.Destroy(_root);
        }

        private void Apply(float alpha, float scale, float travel)
        {
            var size = _size * scale;
            foreach (var line in _lines)
            {
                line.transform.localScale = size;
                line.color = Fade(_s.TouchLineColor, alpha);
            }
            foreach (var glow in _glows)
            {
                glow.transform.localScale = size;
                glow.color = Fade(_s.TouchGlowColor, alpha);
            }
            // Starts at the front and runs round once, eased so it slows as the ring fades.
            var angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * PrimeTween.Easing.Evaluate(travel, PrimeTween.Ease.OutSine);
            var near = Mathf.Sin(angle) < 0f;
            var rx = size.x * _native * 0.5f * LineRadius;
            var ry = size.y * _native * 0.5f * LineRadius;
            _glint.transform.localPosition = new Vector3(rx * Mathf.Cos(angle), ry * Mathf.Sin(angle), 0f);
            _glint.sortingOrder = near ? _nearOrder + 2 : _farOrder + 1;
            _glint.color = Fade(_s.GlintColor, alpha * (near ? 1f : GlintFarAlpha));
        }

        private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

        private SpriteRenderer Layer(Sprite sprite, int order)
        {
            var go = new GameObject(sprite != null ? sprite.name : "TouchRingLayer");
            go.transform.SetParent(_root.transform, false);
            var layer = go.AddComponent<SpriteRenderer>();
            layer.sprite = sprite;
            layer.sortingOrder = order;
            return layer;
        }
    }
}
