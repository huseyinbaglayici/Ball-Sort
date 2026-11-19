using PrimeTween;
using UnityEngine;

namespace Project.Common.UI
{
    // Replays on every enable; unscaled time, like the other UI motion.
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiEntrance : MonoBehaviour
    {
        [SerializeField] private Vector2 _offset = new(0f, 400f);
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private float _delay;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private RectTransform _rect;
        private Vector2 _rest;
        private bool _hasRest;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            if (!_hasRest)
            {
                _rest = _rect.anchoredPosition;
                _hasRest = true;
            }
            PrimeTween.Tween.StopAll(_rect);
            _rect.anchoredPosition = _rest + _offset;
            PrimeTween.Tween.UIAnchoredPosition(_rect, _rest + _offset, _rest, _duration, _ease, startDelay: _delay,
                useUnscaledTime: true);
        }

        private void OnDisable()
        {
            if (_rect == null) return;
            PrimeTween.Tween.StopAll(_rect);
            _rect.anchoredPosition = _rest;
        }
    }
}
