using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Main
{
    // Home-to-level reveal drawn over everything: a dark cover with a feathered round hole on the orb in the mascot's hand.
    // Opening grows the hole out of the orb over the built level; closing shrinks it back. One clock drives it.
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class LevelOpenView : MonoBehaviour
    {
        [SerializeField] private LevelOpenSettings _settings;
        [SerializeField] private Shader _shader;

        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimInnerId = Shader.PropertyToID("_RimInner");
        private static readonly int RimOuterId = Shader.PropertyToID("_RimOuter");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int RimAlphaId = Shader.PropertyToID("_RimAlpha");
        private static readonly int DimId = Shader.PropertyToID("_Dim");

        private RectTransform _rect;
        private CanvasGroup _group;
        private Image _image;
        private Material _material;
        private Canvas _canvas;
        private RectTransform _orb;
        private Action<float> _push;
        private Vector2 _centre;
        private float _orbRadius;
        private float _coverRadius;
        private bool _switched;
        private Action _onCovered;

        public bool IsRunning { get; private set; }

        // The view starts inactive, so it sets itself up on first use rather than in Awake.
        private void Init()
        {
            if (_rect != null) return;
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _image = GetComponent<Image>();
            _canvas = GetComponentInParent<Canvas>(true).rootCanvas;
            _material = new Material(_shader);
            _image.material = _material;
        }

        // Darkens around the orb, runs onCovered (the level is built unseen), then reveals it out of the orb.
        // push gets the Home content's scale as the view moves into the orb.
        public Awaitable OpenAsync(RectTransform orb, Action<float> push, Action onCovered)
        {
            Init();
            _orb = orb;
            _push = push;
            return RunAsync(_settings.OpenTotal, EvaluateOpen, onCovered);
        }

        // Shrinks the level into the orb's spot, runs onCovered, then clears off Home as it pulls back out.
        public Awaitable CloseAsync(RectTransform orb, Action<float> push, Action onCovered)
        {
            Init();
            _orb = orb;
            _push = push;
            return RunAsync(_settings.CloseTotal, EvaluateClose, onCovered);
        }

        private async Awaitable RunAsync(float total, Action<float> evaluate, Action onCovered)
        {
            IsRunning = true;
            _switched = false;
            _onCovered = onCovered;
            gameObject.SetActive(true);
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            try
            {
                Locate();
                evaluate(0f);
                await Tween.Custom(this, 0f, total, total, (view, t) => evaluate(t), Ease.Linear, useUnscaledTime: true);
            }
            finally
            {
                // A switch that threw must still leave the screen uncovered and the flow usable.
                Switch();
                Tween.StopAll(this);
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                gameObject.SetActive(false);
                IsRunning = false;
            }
        }

        private void Switch()
        {
            if (_switched) return;
            _switched = true;
            var action = _onCovered;
            _onCovered = null;
            action?.Invoke();
        }

        private void EvaluateOpen(float t)
        {
            var s = _settings;
            var darken = Easing.Evaluate(Mathf.Clamp01(t / s.DarkenTime), Ease.OutQuad);
            var grow = Easing.Evaluate(Mathf.Clamp01((t - s.DarkenTime) / s.GrowTime), Ease.InOutSine);
            // Home moves into the orb over the whole open; the inside is dimmest as the hole starts to grow.
            _push?.Invoke(1f + s.PushScale * Easing.Evaluate(Mathf.Clamp01(t / s.OpenTotal), Ease.InOutSine));
            if (t >= s.DarkenTime) Switch();
            Apply(Mathf.LerpUnclamped(_orbRadius, _coverRadius, grow), darken, s.Dim * darken * (1f - grow));
        }

        private void EvaluateClose(float t)
        {
            var s = _settings;
            var shrink = Easing.Evaluate(Mathf.Clamp01(t / s.ShrinkTime), Ease.InOutSine);
            var clear = Easing.Evaluate(Mathf.Clamp01((t - s.ShrinkTime) / s.ClearTime), Ease.OutQuad);
            if (t >= s.ShrinkTime) Switch();
            // Home shows pushed in and pulls back out as the dark clears.
            if (_switched) _push?.Invoke(1f + s.PushScale * (1f - clear));
            Apply(Mathf.LerpUnclamped(_coverRadius, _orbRadius, shrink), 1f - clear, s.Dim * shrink);
        }

        private void Apply(float radius, float alpha, float dim)
        {
            var s = _settings;
            var feather = Mathf.Max(radius * s.FeatherShare, s.FeatherMin);
            _material.SetFloat(RadiusId, radius + feather);
            _material.SetFloat(FeatherId, feather);
            _material.SetColor(ColorId, s.Dark);
            _material.SetColor(RimInnerId, s.RimInner);
            _material.SetColor(RimOuterId, s.RimOuter);
            _material.SetFloat(RimWidthId, s.RimWidth);
            _material.SetFloat(RimAlphaId, s.RimAlpha);
            _material.SetFloat(DimId, dim);
            _image.color = new Color(1f, 1f, 1f, alpha);
        }

        // The orb's centre and radius in this view's space (origin bottom-left, as the shader reads it), and the radius that covers the screen.
        private void Locate()
        {
            var corners = new Vector3[4];
            _orb.GetWorldCorners(corners);
            var orbCanvas = _orb.GetComponentInParent<Canvas>(true).rootCanvas;
            var orbCamera = orbCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : orbCanvas.worldCamera;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var min = ToLocal(corners[0]);
            var max = ToLocal(corners[2]);
            var size = _rect.rect.size;
            _centre = (min + max) * 0.5f + size * 0.5f;
            _orbRadius = Mathf.Max(max.x - min.x, 2f) * 0.5f;
            _coverRadius = 0f;
            for (var x = 0; x <= 1; x++)
                for (var y = 0; y <= 1; y++)
                    _coverRadius = Mathf.Max(_coverRadius, (new Vector2(size.x * x, size.y * y) - _centre).magnitude);
            _material.SetVector(CenterId, _centre);
            _material.SetVector(SizeId, size);

            Vector2 ToLocal(Vector3 world)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(orbCamera, world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, camera, out var local);
                return local;
            }
        }
    }
}
