using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// The selection highlight of one ball: a thin contour and a soft light around this ball only, drawn behind it, so
    /// selected neighbours stay separate balls. Both are procedural sprites until the contour art arrives.
    /// </summary>
    public sealed class BallHighlight
    {
        // The orb is at 1 inside the tube; the highlight sits behind it.
        private const int ContourOrder = -1;
        private const int GlowOrder = -2;
        private const int Pixels = 128;

        private static Sprite _ringSprite;
        private static Sprite _haloSprite;

        private readonly SpriteRenderer _contour;
        private readonly SpriteRenderer _glow;
        private BallAnimationConfig _config;
        private float _amount;
        private PrimeTween.Tween _fade;

        public BallHighlight(Transform parent, int sortingLayerId)
        {
            _contour = Create(parent, "Contour", RingSprite(), ContourOrder, sortingLayerId);
            _glow = Create(parent, "Glow", HaloSprite(), GlowOrder, sortingLayerId);
        }

        /// <summary>
        /// Fades the contour and light in or out from where they are now. Every ball of a selection calls it at the same
        /// moment, so the whole group reads as selected on the first frame.
        /// </summary>
        public void Set(bool on, BallAnimationConfig config)
        {
            _config = config;
            // The highlight sprites are 1 unit wide.
            _contour.transform.localScale = Vector3.one * (TubeArt.BallDiameter * config.ContourScale);
            _glow.transform.localScale = Vector3.one * (TubeArt.BallDiameter * config.GlowScale);
            Fade(on ? 1f : 0f, on ? config.HighlightInDuration : config.HighlightOutDuration);
        }

        /// <summary>Hides the highlight at once.</summary>
        public void Hide()
        {
            if (_fade.isAlive) _fade.Stop();
            Apply(0f);
        }

        /// <summary>Raises the highlight with its ball while the ball flies over the tubes.</summary>
        public void SetOrderBoost(int boost)
        {
            _contour.sortingOrder = ContourOrder + boost;
            _glow.sortingOrder = GlowOrder + boost;
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
            _contour.enabled = on;
            _glow.enabled = on;
            if (!on) return;
            var c = _config.ContourColor;
            var g = _config.GlowColor;
            _contour.color = new Color(c.r, c.g, c.b, c.a * amount);
            _glow.color = new Color(g.r, g.g, g.b, g.a * amount);
        }

        private static SpriteRenderer Create(Transform parent, string objectName, Sprite sprite, int order, int sortingLayerId)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>A thin ring.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const float thickness = 0.08f;
            _ringSprite = BuildCircle("BallContour", d =>
            {
                var edge = Mathf.Abs(d - (1f - thickness * 0.5f));
                return (thickness * 0.5f - edge) * Pixels * 0.5f + 0.5f;
            });
            return _ringSprite;
        }

        /// <summary>Soft light strongest at the ball edge and fading outward; clear inside, so it never tints the ball.</summary>
        private static Sprite HaloSprite()
        {
            if (_haloSprite != null) return _haloSprite;
            // The ball edge sits at about 0.79 of the halo radius when the halo is 1.26 balls wide.
            const float inner = 0.76f;
            const float innerFade = 0.08f;
            _haloSprite = BuildCircle("BallGlow", d =>
            {
                if (d >= 1f) return 0f;
                if (d < inner) return 1f - (inner - d) / innerFade;
                var t = 1f - (d - inner) / (1f - inner);
                return t * t;
            });
            return _haloSprite;
        }

        /// <summary>A white circle 1 unit wide whose alpha depends on the normalized distance from its center.</summary>
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
