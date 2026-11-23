using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // Pressing the dim or caption fades the panel away to show the board; releasing that finger brings it back.
    public sealed class HoldToPeek : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float HideDuration = 0.22f;
        private const float ShowDuration = 0.2f;
        private const float HiddenScale = 0.98f;
        private const float PulsePeriod = 1.1f;
        private const float PulseLow = 0.65f;

        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private Image _dim;
        // "Hold to see the board": a press area on the panel that breathes gently so it reads as something to press.
        [SerializeField] private Graphic _caption;

        private float _dimAlpha = -1f;
        private bool _held;
        private int _pointer;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Presses on the panel body bubble up here too; only the dim and the caption peek.
            var target = eventData.pointerPressRaycast.gameObject;
            if (_held || target == null) return;
            if (target != _dim.gameObject && (_caption == null || !target.transform.IsChildOf(_caption.transform))) return;
            _pointer = eventData.pointerId;
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_held && eventData.pointerId == _pointer) SetHeld(false);
        }

        // Back from the background the panel must be there.
        private void OnApplicationPause(bool paused)
        {
            if (paused && _held) Restore();
        }

        private void OnEnable()
        {
            if (_caption == null) return;
            PrimeTween.Tween.StopAll(_caption);
            _caption.color = WithAlpha(_caption.color, 1f);
            PrimeTween.Tween.Alpha(_caption, 1f, PulseLow, PulsePeriod * 0.5f, Ease.InOutSine, cycles: -1,
                cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
        }

        private void OnDisable()
        {
            // StopAll(null) would stop every tween in the game, so each target is checked first.
            if (_caption != null) PrimeTween.Tween.StopAll(_caption);
            Restore();
        }

        private void Restore()
        {
            _held = false;
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
