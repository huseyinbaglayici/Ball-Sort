using UnityEngine;

namespace Project.Core.CameraFit
{
    // Keeps the design width visible; tall phones get extra height, tablets keep the design height.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _designWidth = 10.8f;   // world units (1080 px at 100 PPU)
        [SerializeField, Min(0.1f)] private float _designHeight = 19.2f;  // world units (1920 px at 100 PPU)

        private Camera _camera;
        private int _lastWidth;
        private int _lastHeight;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight) Apply();
        }

        private void Apply()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_lastHeight == 0) return;

            var aspect = (float)_lastWidth / _lastHeight;
            var sizeForWidth = _designWidth / aspect * 0.5f;
            var sizeForHeight = _designHeight * 0.5f;
            _camera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);
        }
    }
}
