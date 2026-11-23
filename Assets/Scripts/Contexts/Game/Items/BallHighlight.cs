using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Thin additive rim plus a soft light behind the ball; parented to the visual so it squashes with it.
    public sealed class BallHighlight
    {
        private const int Pixels = 256;
        // Visible sphere relative to TubeArt.BallDiameter.
        private const float SphereScale = 1.01f;

        private static readonly Dictionary<float, Sprite> RimSprites = new();
        private static readonly Dictionary<float, Sprite> GlowSprites = new();
        private static Material _additive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            RimSprites.Clear();
            GlowSprites.Clear();
            _additive = null;
        }

        private readonly SpriteRenderer _rim;
        private readonly SpriteRenderer _glow;
        private readonly int _rimOrder;
        private readonly int _glowOrder;
        private BallAnimationConfig _config;
        private float _amount;
        private Color _tint = Color.white;
        private PrimeTween.Tween _fade;

        // Light under the lowest layer, rim over the top; the rim shares the "?" order as hidden balls never select.
        public BallHighlight(Transform parent, int sortingLayerId, int bottomOrder, int topOrder)
        {
            _rimOrder = topOrder + 1;
            _glowOrder = bottomOrder - 1;
            _rim = Create(parent, "Rim", _rimOrder, sortingLayerId);
            _glow = Create(parent, "Glow", _glowOrder, sortingLayerId);
        }

        // Fades from the current amount.
        public void Set(bool on, BallAnimationConfig config)
        {
            _config = config;
            // Sprites are 1 unit wide.
            _rim.sprite = RimSprite(config.ContourScale);
            _glow.sprite = GlowSprite(config.GlowScale);
            _rim.transform.localScale = Vector3.one * (TubeArt.BallDiameter * SphereScale * config.ContourScale);
            _glow.transform.localScale = Vector3.one * (TubeArt.BallDiameter * SphereScale * config.GlowScale);
            Fade(on ? 1f : 0f, on ? config.HighlightInDuration : config.HighlightOutDuration);
        }

        public void SetTint(Color color)
        {
            _tint = color;
            Apply(_amount);
        }

        public void Hide()
        {
            if (_fade.isAlive) _fade.Stop();
            Apply(0f);
        }

        // Only the rim takes the stack offset; the light stays under every body so it never covers a neighbour.
        public void SetOrder(int boost, int stackOffset)
        {
            _rim.sortingOrder = _rimOrder + boost + stackOffset;
            _glow.sortingOrder = _glowOrder + boost;
        }

        private void Fade(float target, float duration)
        {
            if (_fade.isAlive) _fade.Stop();
            if (duration <= 0f || Mathf.Abs(_amount - target) < 0.001f)
            {
                Apply(target);
                return;
            }
            _fade = PrimeTween.Tween.Custom(this, _amount, target, duration,
                (highlight, a) => highlight.Apply(a), PrimeTween.Ease.OutQuad);
        }

        private void Apply(float amount)
        {
            _amount = amount;
            var on = amount > 0.001f && _config != null;
            _rim.enabled = on;
            _glow.enabled = on;
            if (!on) return;
            _rim.color = Tinted(_config.ContourColor, _config.ContourWhiten, amount);
            _glow.color = Tinted(_config.GlowColor, _config.GlowWhiten, amount);
        }

        private Color Tinted(Color multiplier, float whiten, float amount)
        {
            Color.RGBToHSV(_tint, out var h, out var s, out _);
            var c = Color.Lerp(Color.HSVToRGB(h, s, 1f), Color.white, whiten) * multiplier;
            c.a = multiplier.a * amount;
            return c;
        }

        private static SpriteRenderer Create(Transform parent, string objectName, int order, int sortingLayerId)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = AdditiveMaterial();
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        private static Material AdditiveMaterial()
        {
            if (_additive == null) _additive = new Material(Shader.Find("Mobile/Particles/Additive")) { name = "BallGlowAdditive" };
            return _additive;
        }

        // scale: sprite diameter relative to the sphere.
        private static Sprite RimSprite(float scale)
        {
            if (RimSprites.TryGetValue(scale, out var sprite)) return sprite;
            var edge = 1f / scale;
            // In sphere radii the glass band spans 0.81 to 1; the rim peaks outside it and fades out by the core.
            const float peak = 0.95f;
            const float inner = 0.07f;
            sprite = BuildCircle("BallRim", d =>
            {
                var r = d / edge;
                if (r >= 1f / edge) return 0f;
                var x = r - peak;
                if (x < 0f) return Mathf.Exp(-(x * x) / (2f * inner * inner));
                var t = Mathf.Clamp01(x / (1f / edge - peak));
                return (1f - t) * (1f - t);
            });
            RimSprites[scale] = sprite;
            return sprite;
        }

        // Full inside the sphere too (hidden behind the ball) so no gap opens at the edge.
        private static Sprite GlowSprite(float scale)
        {
            if (GlowSprites.TryGetValue(scale, out var sprite)) return sprite;
            var edge = 1f / scale;
            sprite = BuildCircle("BallGlow", d =>
            {
                if (d <= edge) return 1f;
                var t = Mathf.Clamp01((d - edge) / (1f - edge));
                var f = 1f - t;
                return f * f;
            });
            GlowSprites[scale] = sprite;
            return sprite;
        }

        // White circle, 1 unit wide, alpha by normalized distance from center.
        private static Sprite BuildCircle(string name, System.Func<float, float> alphaAt)
        {
            const int size = Pixels;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name
            };
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var a = Mathf.Clamp01(alphaAt(Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
