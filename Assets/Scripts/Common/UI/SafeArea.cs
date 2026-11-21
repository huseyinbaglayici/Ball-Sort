using UnityEngine;

namespace Project.Common.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        // Smallest top and bottom margins as screen-height fractions, for screens without a notch or home bar.
        [SerializeField] private float _minTop;
        [SerializeField] private float _minBottom;

        private RectTransform _rect;
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            var area = Screen.safeArea;
            _applied = area;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var min = area.position;
            var max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            min.y = Mathf.Max(min.y, _minBottom);
            max.y = Mathf.Min(max.y, 1f - _minTop);
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
