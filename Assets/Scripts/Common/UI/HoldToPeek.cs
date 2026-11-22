using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // While pressed the panel and dim ease away to show the board (input stays blocked); release eases them back without snapping.
    public sealed class HoldToPeek : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float HideDuration = 0.22f;
        private const float ShowDuration = 0.2f;
        private const float HiddenScale = 0.98f;
        private const float PulsePeriod = 1.1f;
        private const float PulseLow = 0.65f;

        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private Image _dim;
        // The caption itself breathes gently so it reads as something to press.
        [SerializeField] private Graphic _pulse;

        private float _dimAlpha = -1f;
        private bool _held;

        public void OnPointerDown(PointerEventData eventData) => SetHeld(true);
        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);

        private void OnEnable()
        {
            if (_pulse == null) return;
            PrimeTween.Tween.StopAll(_pulse);
            _pulse.color = WithAlpha(_pulse.color, 1f);
            PrimeTween.Tween.Alpha(_pulse, 1f, PulseLow, PulsePeriod * 0.5f, Ease.InOutSine, cycles: -1,
                cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
        }

        private void OnDisable()
        {
            _held = false;
            // StopAll(null) would stop every tween in the game, so each target is checked first.
            if (_pulse != null) PrimeTween.Tween.StopAll(_pulse);
            if (_panel != null)
            {
                PrimeTween.Tween.StopAll(_panel);
                PrimeTween.Tween.StopAll(_panel.transform);
                _panel.alpha = 1f;
                _panel.interactable = true;
                _panel.transform.localScale = Vector3.one;
            }
            if (_dim != null)
            {
                PrimeTween.Tween.StopAll(_dim);
                if (_dimAlpha >= 0f) _dim.color = WithAlpha(_dim.color, _dimAlpha);
            }
        }

        private void SetHeld(bool held)
        {
            if (held == _held) return;
            _held = held;
            if (_dimAlpha < 0f) _dimAlpha = _dim.color.a;
            var panel = _panel.transform;
            // A second finger must not press the buttons while the panel is see-through.
            _panel.interactable = !held;
            PrimeTween.Tween.StopAll(_panel);
            PrimeTween.Tween.StopAll(panel);
            PrimeTween.Tween.StopAll(_dim);
            var duration = held ? HideDuration : ShowDuration;
            var ease = held ? Ease.OutCubic : Ease.InOutSine;
            // Each tween starts from the current value, so reversing mid-way is seamless.
            PrimeTween.Tween.Alpha(_panel, held ? 0f : 1f, duration, ease, useUnscaledTime: true);
            PrimeTween.Tween.Scale(panel, held ? HiddenScale : 1f, duration, ease, useUnscaledTime: true);
            PrimeTween.Tween.Alpha(_dim, held ? 0f : _dimAlpha, duration, ease, useUnscaledTime: true);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
