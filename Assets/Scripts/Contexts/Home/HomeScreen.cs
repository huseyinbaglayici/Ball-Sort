using Project.Contexts.Application;
using Project.Contexts.Main;
using Project.Core.Audio;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Services;
using Project.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Home
{
    /// <summary>Home screen: level and coins, Play, and the three persisted settings toggles.</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _view;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private Button _playButton;
        [SerializeField] private Toggle _musicToggle;
        [SerializeField] private Toggle _sfxToggle;
        [SerializeField] private Toggle _hapticsToggle;

        private ProgressService _progress;
        private LocalizationService _localization;

        private void Start()
        {
            _progress = Services.Get<ProgressService>();
            _localization = Services.Get<LocalizationService>();
            var settings = Services.Get<SettingsService>();
            var audio = Services.Get<AudioService>();
            var haptics = Services.Get<HapticService>();

            _musicToggle.SetIsOnWithoutNotify(!settings.Model.MusicMuted);
            _sfxToggle.SetIsOnWithoutNotify(!settings.Model.SfxMuted);
            _hapticsToggle.SetIsOnWithoutNotify(settings.Model.HapticsEnabled);

            _musicToggle.onValueChanged.AddListener(on =>
            {
                settings.Model.MusicMuted = !on;
                audio.SetMusicMuted(!on);
                settings.Save();
            });
            _sfxToggle.onValueChanged.AddListener(on =>
            {
                settings.Model.SfxMuted = !on;
                audio.SetSfxMuted(!on);
                settings.Save();
            });
            _hapticsToggle.onValueChanged.AddListener(on =>
            {
                settings.Model.HapticsEnabled = on;
                haptics.Enabled = on;
                settings.Save();
                if (on) haptics.Play(HapticStrength.Medium);
            });
            _playButton.onClick.AddListener(() => _ = Services.Get<MainFlow>().GoToGameAsync());

            Refresh();
        }

        /// <summary>Shows or hides the home view; it shows the current level and coins when it opens.</summary>
        public void SetOpen(bool open)
        {
            _view.SetActive(open);
            if (open) Refresh();
        }

        private void OnEnable()
        {
            EventBus<CoinsChanged>.Subscribe(OnCoinsChanged);
            EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
        }

        private void OnDisable()
        {
            EventBus<CoinsChanged>.Unsubscribe(OnCoinsChanged);
            EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);
        }

        private void OnCoinsChanged(CoinsChanged _) => Refresh();
        private void OnLanguageChanged(LanguageChanged _) => Refresh();

        private void Refresh()
        {
            if (_progress == null) return;
            _levelText.text = _localization.Format("home.level", _progress.Level);
            _coinsText.text = _progress.Coins.ToString();
        }
    }
}
