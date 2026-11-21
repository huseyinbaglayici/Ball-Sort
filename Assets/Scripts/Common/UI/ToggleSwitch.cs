using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // On/off switch: the track swaps art and the knob slides across in 0.15 s.
    [RequireComponent(typeof(Button))]
    public sealed class ToggleSwitch : MonoBehaviour
    {
        private const float SlideDuration = 0.15f;

        [SerializeField] private Image _track;
        [SerializeField] private Sprite _trackOn;
        [SerializeField] private Sprite _trackOff;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private float _knobTravel = 72f;
        // Drawn over the row's icon while off.
        [SerializeField] private GameObject _offMark;

        private float _offX;
        private bool _ready;

        public bool IsOn { get; private set; }
        public event Action<bool> Changed;

        private void Awake()
        {
            Init();
            GetComponent<Button>().onClick.AddListener(() =>
            {
                Show(!IsOn, true);
                Changed?.Invoke(IsOn);
            });
        }

        private void Init()
        {
            if (_ready) return;
            _ready = true;
            _offX = _knob.anchoredPosition.x;
        }

        public void Show(bool on, bool animate = false)
        {
            Init();
            IsOn = on;
            _track.sprite = on ? _trackOn : _trackOff;
            if (_offMark != null) _offMark.SetActive(!on);
            var target = new Vector2(_offX + (on ? _knobTravel : 0f), _knob.anchoredPosition.y);
            PrimeTween.Tween.StopAll(_knob);
            if (animate) PrimeTween.Tween.UIAnchoredPosition(_knob, target, SlideDuration, Ease.OutQuad, useUnscaledTime: true);
            else _knob.anchoredPosition = target;
        }
    }
}
