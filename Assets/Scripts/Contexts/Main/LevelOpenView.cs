using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Main
{
    // Home-to-level reveal: a dark cover whose hole grows out of the mascot's orb; on close the level dives into a spot and Home opens out of it.
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class LevelOpenView : MonoBehaviour
    {
        [SerializeField] private LevelOpenSettings _settings;
        [SerializeField] private Shader _shader;
        [SerializeField] private Shader _closeShader;

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
        private static readonly int SnapshotId = Shader.PropertyToID("_Snapshot");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int AnchorId = Shader.PropertyToID("_Anchor");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int TintAmountId = Shader.PropertyToID("_TintAmount");

        private RectTransform _rect;
        private CanvasGroup _group;
        private Image _image;
        private Material _material;
        private Material _closeMaterial;
        private Texture2D _snapshot;
        private Canvas _canvas;
        private RectTransform _orb;
        private Action<float> _push;
        private Vector2 _centre;
        private float _orbRadius;
        private float _coverRadius;
        private float _zoomScale;
        private bool _revealing;
        private bool _switched;
        private Action _onCovered;

        public bool IsRunning { get; private set; }

        // Both masks are set up; without them the flow uses the plain loading overlay.
        public bool IsReady => _shader != null && _closeShader != null && _settings != null;

        // The view starts inactive, so it sets itself up on first use rather than in Awake.
        private void Init()
        {
            if (_rect != null) return;
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _image = GetComponent<Image>();
            _canvas = GetComponentInParent<Canvas>(true).rootCanvas;
            _material = new Material(_shader);
            _closeMaterial = new Material(_closeShader);
            _image.material = _material;
        }

        // Darkens around the orb, builds the level unseen in onCovered, then reveals it; push scales the Home content.
        public Awaitable OpenAsync(RectTransform orb, Action<float> push, Action onCovered)
        {
            Init();
            _orb = orb;
            _push = push;
            return RunAsync(_settings.OpenTotal, EvaluateOpen, onCovered);
        }

        // Freezes the level's last frame, runs onCovered so Home sets up under it, dives into the given screen spot until the tint
        // fills the screen, then opens Home out of the middle.
        public async Awaitable CloseAsync(Vector2 screenPoint, float screenRadius, Action<float> push, Action onCovered)
        {
            Init();
            _push = push;
            IsRunning = true;
            _revealing = false;
            try
            {
                await Awaitable.EndOfFrameAsync();
                _snapshot = ScreenCapture.CaptureScreenshotAsTexture();
                _closeMaterial.SetTexture(SnapshotId, _snapshot);
                _image.material = _closeMaterial;
                _image.color = Color.white;
                gameObject.SetActive(true);
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
                LocateScreen(screenPoint, screenRadius);
                EvaluateClose(0f);
                onCovered?.Invoke();
                var total = _settings.CloseTotal;
                await Tween.Custom(this, 0f, total, total, (view, t) => view.EvaluateClose(t), Ease.Linear, useUnscaledTime: true);
            }
            finally
            {
                Tween.StopAll(this);
                _push?.Invoke(1f);
                _closeMaterial.SetTexture(SnapshotId, null);
                if (_snapshot != null) Destroy(_snapshot);
                _snapshot = null;
                _image.material = _material;
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                gameObject.SetActive(false);
                IsRunning = false;
            }
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
            Apply(Mathf.LerpUnclamped(_orbRadius, _coverRadius, grow), darken, s.Dim * darken * (1f - grow), s.Dark);
        }

        // The dive: the spot drifts to the middle while the frame scales up about it and washes into the tint; then the
        // tint's hole grows from the middle like a vignette opening while Home settles from zoomed in.
        private void EvaluateClose(float t)
        {
            var s = _settings;
            var size = _rect.rect.size;
            var middle = size * 0.5f;
            if (t < s.ZoomTime)
            {
                var k = t / Mathf.Max(s.ZoomTime, 0.0001f);
                var dive = Easing.Evaluate(k, Ease.InCubic);
                var share = Mathf.Max(s.TintShare, 0.0001f);
                var tint = Easing.Evaluate(Mathf.Clamp01((k - (1f - share)) / share), Ease.InQuad);
                _push?.Invoke(1f + s.ClosePush);
                _closeMaterial.SetVector(SizeId, size);
                _closeMaterial.SetVector(AnchorId, _centre);
                _closeMaterial.SetVector(CenterId, Vector2.Lerp(_centre, middle, Easing.Evaluate(k, Ease.InOutSine)));
                _closeMaterial.SetFloat(ScaleId, Mathf.Pow(_zoomScale, dive));
                _closeMaterial.SetColor(TintId, s.ZoomTint);
                _closeMaterial.SetFloat(TintAmountId, tint);
                return;
            }

            if (!_revealing)
            {
                _revealing = true;
                _image.material = _material;
                _material.SetVector(CenterId, middle);
                _material.SetVector(SizeId, size);
                _coverRadius = middle.magnitude;
            }
            var reveal = Easing.Evaluate(Mathf.Clamp01((t - s.ZoomTime) / Mathf.Max(s.RevealTime, 0.0001f)), Ease.OutCubic);
            _push?.Invoke(1f + s.ClosePush * (1f - reveal));
            Apply(_coverRadius * reveal, 1f, 0f, s.ZoomTint);
        }

        private void Apply(float radius, float alpha, float dim, Color color)
        {
            var s = _settings;
            var feather = Mathf.Max(radius * s.FeatherShare, s.FeatherMin);
            _material.SetFloat(RadiusId, radius + feather);
            _material.SetFloat(FeatherId, feather);
            _material.SetColor(ColorId, color);
            _material.SetColor(RimInnerId, s.RimInner);
            _material.SetColor(RimOuterId, s.RimOuter);
            _material.SetFloat(RimWidthId, s.RimWidth);
            _material.SetFloat(RimAlphaId, s.RimAlpha);
            _material.SetFloat(DimId, dim);
            _image.color = new Color(1f, 1f, 1f, alpha);
        }

        // A screen spot and radius in this view's space, and how far the frame must scale for that radius to cover the screen.
        private void LocateScreen(Vector2 screenPoint, float screenRadius)
        {
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var size = _rect.rect.size;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, camera, out var at);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint + new Vector2(screenRadius, 0f), camera, out var edge);
            _centre = at + size * 0.5f;
            _orbRadius = Mathf.Max((edge - at).magnitude, 2f);
            _zoomScale = Mathf.Max(size.magnitude * 0.5f * _settings.ZoomFill / _orbRadius, 1f);
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
