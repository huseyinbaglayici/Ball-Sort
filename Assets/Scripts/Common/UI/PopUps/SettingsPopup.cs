using System;
using Project.Core.Haptics;
using Project.Core.Services;
using Project.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Common.UI.PopUps
{
    public readonly struct SettingsArgs
    {
        // Null hides the button.
        public readonly Action Restart;
        public readonly Action Home;
        public readonly bool RestartCostsLife;

        public SettingsArgs(Action restart, Action home, bool restartCostsLife)
        {
            Restart = restart;
            Home = home;
            RestartCostsLife = restartCostsLife;
        }
    }

    // Sound, music and vibration switches saved at once; optional restart, Home and Continue buttons.
    public sealed class SettingsPopup : Popup
    {
        [SerializeField] private ToggleSwitch _sound;
        [SerializeField] private ToggleSwitch _music;
        [SerializeField] private ToggleSwitch _haptics;
        [SerializeField] private Button _restart;
        [SerializeField] private GameObject _restartCost;
        [SerializeField] private Button _home;
        [SerializeField] private Button _continue;
        [SerializeField] private Button _close;
        // The frame grows by one step per extra button below the switches.
        [SerializeField] private RectTransform _frame;
        [SerializeField] private float _buttonStep = 170f;

        private SettingsService _settings;
        private HapticModule _hapticModule;
        private SettingsArgs _args;
        private float _frameHeight;

        protected override void Awake()
        {
            base.Awake();
            if (_frame != null) _frameHeight = _frame.sizeDelta.y;
            _settings = Services.Get<SettingsService>();
            _hapticModule = Services.Get<HapticModule>();

            _sound.Changed += on => _settings.SetSoundMuted(!on);
            _music.Changed += on => _settings.SetMusicMuted(!on);
            _haptics.Changed += on =>
            {
                _settings.SetHapticsEnabled(on);
                if (on) _hapticModule.Haptic(HapticModule.HapticType.Medium);
            };
            _close.onClick.AddListener(Close);
            if (_continue != null) _continue.onClick.AddListener(Close);
            if (_restart != null) _restart.onClick.AddListener(() => Run(_args.Restart));
            if (_home != null) _home.onClick.AddListener(() => Run(_args.Home));
        }

        protected override void OnShow(object args)
        {
            _args = args is SettingsArgs s ? s : default;
            _sound.Show(!_settings.SoundMuted);
            _music.Show(!_settings.MusicMuted);
            _haptics.Show(_settings.HapticsEnabled);
            if (_restart != null) _restart.gameObject.SetActive(_args.Restart != null);
            if (_restartCost != null) _restartCost.SetActive(_args.RestartCostsLife);
            if (_home != null) _home.gameObject.SetActive(_args.Home != null);
            if (_frame == null) return;
            var extra = (_args.Restart != null ? 1 : 0) + (_args.Home != null ? 1 : 0);
            _frame.sizeDelta = new Vector2(_frame.sizeDelta.x, _frameHeight + _buttonStep * extra);
        }

        private void Run(Action action)
        {
            Close();
            action?.Invoke();
        }
    }
}
