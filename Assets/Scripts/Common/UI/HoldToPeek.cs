using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // While held, fades a popup's panel and dim away to show what lies under it; the dim keeps blocking input.
    public sealed class HoldToPeek : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float FadeDuration = 0.15f;

        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private Image _dim;

        private float _dimAlpha = -1f;
        private bool _held;

        public void OnPointerDown(PointerEventData eventData) => SetHeld(true);
        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);
        public void OnPointerExit(PointerEventData eventData) => SetHeld(false);

        private void OnDisable()
        {
            _held = false;
            PrimeTween.Tween.StopAll(_panel);
            PrimeTween.Tween.StopAll(_dim);
            if (_panel != null) _panel.alpha = 1f;
            if (_dim != null && _dimAlpha >= 0f) _dim.color = WithAlpha(_dim.color, _dimAlpha);
        }

        private void SetHeld(bool held)
        {
            if (held == _held) return;
            _held = held;
            if (_dimAlpha < 0f) _dimAlpha = _dim.color.a;
            PrimeTween.Tween.StopAll(_panel);
            PrimeTween.Tween.StopAll(_dim);
            PrimeTween.Tween.Alpha(_panel, held ? 0f : 1f, FadeDuration, Ease.OutQuad, useUnscaledTime: true);
            PrimeTween.Tween.Alpha(_dim, held ? 0f : _dimAlpha, FadeDuration, Ease.OutQuad, useUnscaledTime: true);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
