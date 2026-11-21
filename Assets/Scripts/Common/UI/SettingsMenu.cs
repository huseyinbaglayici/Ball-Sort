using System;
using PrimeTween;
using Project.Core.Haptics;
using Project.Core.Services;
using Project.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // Settings button with a column of round buttons over a dark tint; a tap on the tint or the button closes it.
    public sealed class SettingsMenu : MonoBehaviour
    {
        private const float ItemDuration = 0.25f;
        private const float ItemStagger = 0.06f;
        private const float SpinDuration = 0.6f;
        private const float DimDuration = 0.6f;
        [SerializeField] private float _dimAlpha = 0.588f;
        // Where the buttons slide in from, relative to their rest position.
        [SerializeField] private Vector2 _hiddenOffset = new(371f, 0f);

        [SerializeField] private Button _gear;
        [SerializeField] private Button _dim;
        [SerializeField] private Button _sound;
        [SerializeField] private Button _music;
        [SerializeField] private Button _haptics;
        [SerializeField] private Button _exit;
        // Off marks drawn over each switch's icon.
        [SerializeField] private GameObject _soundOff;
        [SerializeField] private GameObject _musicOff;
        [SerializeField] private GameObject _hapticsOff;

        private SettingsService _settings;
        private HapticModule _hapticModule;
        private RectTransform[] _items;
        private Vector2[] _rest;
        private bool _open;

        // Raised after the menu has closed.
        public event Action ExitPressed;

        private void Awake()
        {
            _settings = Services.Get<SettingsService>();
            _hapticModule = Services.Get<HapticModule>();

            var buttons = _exit != null ? new[] { _sound, _music, _haptics, _exit } : new[] { _sound, _music, _haptics };
            _items = new RectTransform[buttons.Length];
            _rest = new Vector2[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
            {
                _items[i] = (RectTransform)buttons[i].transform;
                _rest[i] = _items[i].anchoredPosition;
            }

            _gear.onClick.AddListener(() => SetOpen(!_open));
            _dim.onClick.AddListener(() => SetOpen(false));
            _sound.onClick.AddListener(() =>
            {
                _settings.SetSoundMuted(!_settings.SoundMuted);
                Render();
            });
            _music.onClick.AddListener(() =>
            {
                _settings.SetMusicMuted(!_settings.MusicMuted);
                Render();
            });
            _haptics.onClick.AddListener(() =>
            {
                _settings.SetHapticsEnabled(!_settings.HapticsEnabled);
                Render();
                if (_settings.HapticsEnabled) _hapticModule.Haptic(HapticModule.HapticType.Medium);
            });
            if (_exit != null)
                _exit.onClick.AddListener(() =>
                {
                    SetOpen(false);
                    ExitPressed?.Invoke();
                });
        }

        private void OnEnable() => Snap(false);

        private void OnDisable() => Snap(false);

        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            Render();

            PrimeTween.Tween.StopAll(_gear.transform);
            PrimeTween.Tween.LocalEulerAngles(_gear.transform, Vector3.zero, new Vector3(0f, 0f, open ? -360f : 360f), SpinDuration,
                Ease.OutCubic, useUnscaledTime: true);

            var dimGraphic = _dim.targetGraphic;
            PrimeTween.Tween.StopAll(dimGraphic);
            _dim.gameObject.SetActive(true);
            PrimeTween.Tween.Alpha(dimGraphic, open ? _dimAlpha : 0f, open ? DimDuration : ItemDuration, Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(this, menu =>
                {
                    if (!menu._open) menu._dim.gameObject.SetActive(false);
                });

            for (var i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                PrimeTween.Tween.StopAll(item);
                var delay = (open ? i : _items.Length - 1 - i) * ItemStagger;
                var hidden = _rest[i] + _hiddenOffset;
                if (open)
                {
                    item.gameObject.SetActive(true);
                    item.anchoredPosition = hidden;
                    PrimeTween.Tween.UIAnchoredPosition(item, hidden, _rest[i], ItemDuration, Ease.OutBack, startDelay: delay,
                        useUnscaledTime: true);
                }
                else
                {
                    PrimeTween.Tween.UIAnchoredPosition(item, hidden, ItemDuration, Ease.InQuad, startDelay: delay,
                            useUnscaledTime: true)
                        .OnComplete(item, rect => rect.gameObject.SetActive(false));
                }
            }
        }

        private void Snap(bool open)
        {
            if (_items == null) return;
            _open = open;
            PrimeTween.Tween.StopAll(_gear.transform);
            _gear.transform.localRotation = Quaternion.identity;
            PrimeTween.Tween.StopAll(_dim.targetGraphic);
            var color = _dim.targetGraphic.color;
            color.a = open ? _dimAlpha : 0f;
            _dim.targetGraphic.color = color;
            _dim.gameObject.SetActive(open);
            for (var i = 0; i < _items.Length; i++)
            {
                PrimeTween.Tween.StopAll(_items[i]);
                _items[i].anchoredPosition = _rest[i];
                _items[i].gameObject.SetActive(open);
            }
            Render();
        }

        private void Render()
        {
            if (_settings == null) return;
            Mark(_soundOff, !_settings.SoundMuted);
            Mark(_musicOff, !_settings.MusicMuted);
            Mark(_hapticsOff, _settings.HapticsEnabled);
        }

        private static void Mark(GameObject offMark, bool on)
        {
            if (offMark != null) offMark.SetActive(!on);
        }
    }
}
