using System;
using UnityEngine;

namespace Project.Common.Visual
{
    // Camera child: fills the view with the nebula (cover, centred) and lets the art's own stars breathe.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpaceBackground : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;

        [Serializable]
        private sealed class StarData
        {
            public float x;
            public float y;
            public int[] rect;
            public float[] pivot;
        }

        [Serializable]
        private sealed class StarLayout
        {
            public int width;
            public int height;
            public StarData[] stars;
        }

        [SerializeField] private Camera _camera;
        // Cut-out stars and where they sit on the nebula art.
        [SerializeField] private Texture2D _starSheet;
        [SerializeField] private TextAsset _starLayout;
        // Each star slowly swings between these on its own 2-8 s cycle and phase; mostly alpha, a hair of scale.
        [SerializeField] private Vector2 _alpha = new(0.7f, 1f);
        [SerializeField] private Vector2 _scale = new(0.97f, 1f);
        [SerializeField] private Vector2 _period = new(2f, 8f);

        private SpriteRenderer _background;
        private Transform[] _stars = Array.Empty<Transform>();
        private SpriteRenderer[] _starRenderers = Array.Empty<SpriteRenderer>();
        private float[] _phase;
        private float[] _speed;
        private float _fittedHeight;
        private float _fittedAspect;

        private void Awake()
        {
            _background = GetComponent<SpriteRenderer>();
            if (_camera == null) _camera = GetComponentInParent<Camera>();
            CreateStars();
        }

        private void LateUpdate()
        {
            var height = _camera.orthographicSize * 2f;
            if (!Mathf.Approximately(height, _fittedHeight) || !Mathf.Approximately(_camera.aspect, _fittedAspect)) Fit();
            Breathe(Time.unscaledTime);
        }

        // Cover: the art fills the view on both axes and stays centred; stars are children, so they follow.
        private void Fit()
        {
            _fittedHeight = _camera.orthographicSize * 2f;
            _fittedAspect = _camera.aspect;
            var width = _fittedHeight * _fittedAspect;
            var art = _background.sprite != null ? _background.sprite.bounds.size : Vector3.one;
            var scale = Mathf.Max(width / art.x, _fittedHeight / art.y);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Breathe(float time)
        {
            for (var i = 0; i < _stars.Length; i++)
            {
                var wave = 0.5f - 0.5f * Mathf.Cos((time * _speed[i] + _phase[i]) * Mathf.PI * 2f);
                var color = _starRenderers[i].color;
                color.a = Mathf.Lerp(_alpha.x, _alpha.y, wave);
                _starRenderers[i].color = color;
                var size = Mathf.Lerp(_scale.x, _scale.y, wave);
                _stars[i].localScale = new Vector3(size, size, 1f);
            }
        }

        private void CreateStars()
        {
            if (_starSheet == null || _starLayout == null) return;
            var layout = JsonUtility.FromJson<StarLayout>(_starLayout.text);
            if (layout?.stars == null) return;

            var count = layout.stars.Length;
            _stars = new Transform[count];
            _starRenderers = new SpriteRenderer[count];
            _phase = new float[count];
            _speed = new float[count];
            for (var i = 0; i < count; i++)
            {
                var data = layout.stars[i];
                var rect = new Rect(data.rect[0], data.rect[1], data.rect[2], data.rect[3]);
                var sprite = Sprite.Create(_starSheet, rect, new Vector2(data.pivot[0], data.pivot[1]), PixelsPerUnit);
                var star = new GameObject("Star", typeof(SpriteRenderer));
                star.transform.SetParent(transform, false);
                // Same pixels-per-unit as the nebula, centred on it: art pixels map one to one.
                star.transform.localPosition = new Vector3((data.x - layout.width * 0.5f) / PixelsPerUnit,
                    (layout.height * 0.5f - data.y) / PixelsPerUnit, -0.01f);
                var renderer = star.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerID = _background.sortingLayerID;
                renderer.sortingOrder = _background.sortingOrder + 1;
                _stars[i] = star.transform;
                _starRenderers[i] = renderer;
                _speed[i] = 1f / UnityEngine.Random.Range(_period.x, _period.y);
                _phase[i] = UnityEngine.Random.value;
            }
        }
    }
}
