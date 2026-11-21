using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Main;
using Project.Core.Events;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Home
{
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _view;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private DifficultyTag _difficulty;
        [SerializeField] private TMP_Text _metaText;
        [SerializeField] private JourneyRoad _road;
        // The road-reward chest on the road.
        [SerializeField] private Button _roadButton;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _starterPackButton;
        [SerializeField] private Button _challengeButton;
        [SerializeField] private Button _rewardedLifeButton;
        [SerializeField] private Button _settingsButton;
        // Tabs for screens not in the game yet.
        [SerializeField] private Button[] _comingSoon = System.Array.Empty<Button>();
        [SerializeField] private SettingsPopup _settingsPopup;
        [SerializeField] private MessagePopup _messagePopup;

        private ProgressService _progress;
        private LivesService _lives;
        private MonetizationService _ads;
        private LocalizationService _localization;
        private PopupService _popups;

        private void Start()
        {
            _progress = Services.Get<ProgressService>();
            _lives = Services.Get<LivesService>();
            _ads = Services.Get<MonetizationService>();
            _localization = Services.Get<LocalizationService>();
            _popups = Services.Get<PopupService>();

            _playButton.onClick.AddListener(Play);
            if (_roadButton != null) _roadButton.onClick.AddListener(ShowRoadReward);
            if (_starterPackButton != null)
                _starterPackButton.onClick.AddListener(() => ShowMessage("offer.starter_title", "offer.preview_body"));
            if (_challengeButton != null)
                _challengeButton.onClick.AddListener(() => ShowMessage("challenge.title", "challenge.soon_body"));
            if (_rewardedLifeButton != null)
                _rewardedLifeButton.onClick.AddListener(() => _ads.ShowRewardedLife(_ => Refresh()));
            if (_settingsButton != null) _settingsButton.onClick.AddListener(ShowSettings);
            foreach (var tab in _comingSoon)
                if (tab != null) tab.onClick.AddListener(() => ShowMessage("nav.soon_title", "nav.soon_body"));

            Refresh();
        }

        public void SetOpen(bool open)
        {
            _view.SetActive(open);
            if (open) Refresh();
        }

        private void OnEnable()
        {
            EventBus<LivesChanged>.Subscribe(OnLivesChanged);
            EventBus<LevelCompleted>.Subscribe(OnLevelCompleted);
            EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
        }

        private void OnDisable()
        {
            EventBus<LivesChanged>.Unsubscribe(OnLivesChanged);
            EventBus<LevelCompleted>.Unsubscribe(OnLevelCompleted);
            EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);
        }

        private void OnLivesChanged(LivesChanged _) => Refresh();
        private void OnLevelCompleted(LevelCompleted _) => Refresh();
        private void OnLanguageChanged(LanguageChanged _) => Refresh();

        // With no life left Play explains when the next one comes.
        private void Play()
        {
            if (_progress.IsCampaignFinished) return;
            if (!_lives.CanPlay)
            {
                _popups.Show(_messagePopup, new MessageArgs("lives.out_title",
                    _localization.Format("lives.out_body", EconomyText.Timer(_lives.NextLifeIn)), "common.ok",
                    closable: true, heart: true));
                return;
            }
            _ = Services.Get<MainFlow>().GoToGameAsync();
        }

        private void ShowSettings() => _popups.Show(_settingsPopup, new SettingsArgs(null, null, false));

        private void ShowMessage(string titleKey, string bodyKey) =>
            _popups.Show(_messagePopup, new MessageArgs(titleKey, _localization.Get(bodyKey), "common.ok", closable: true));

        private void Refresh()
        {
            if (_progress == null) return;
            var finished = _progress.IsCampaignFinished;
            var level = _progress.Level;
            _levelText.text = finished ? _localization.Get("meta.final_title") : _localization.Format("home.level", level);
            _playButton.interactable = !finished;
            if (_difficulty != null)
            {
                var difficulty = finished ? LevelDifficulty.Normal : CampaignRules.DifficultyOf(level);
                _difficulty.Set(EconomyText.Difficulty(difficulty, _localization), EconomyText.DifficultyMarks(difficulty));
            }
            if (_road != null) _road.Show(Mathf.Min(level, CampaignRules.MaxLevel), _localization);

            if (_metaText != null)
                _metaText.text = finished
                    ? _localization.Get("meta.final_title")
                    : _localization.Format("home.period", _progress.MetaPeriod);
            // Tab-bar slots stay in place; locked ones just don't respond.
            if (_starterPackButton != null) _starterPackButton.interactable = _progress.IsUnlocked(Feature.StarterPack);
            if (_challengeButton != null) _challengeButton.interactable = _progress.IsUnlocked(Feature.Challenge);
            if (_rewardedLifeButton != null)
                _rewardedLifeButton.gameObject.SetActive(!finished && _ads.CanOfferRewardedLife);
        }

        private void ShowRoadReward()
        {
            var next = CampaignRules.NextRoadLevel(_progress.Level);
            if (next == 0) return;
            var body = _localization.Format("road.body", next, EconomyText.Road(CampaignRules.RoadRewardAt(next), _localization));
            _popups.Show(_messagePopup, new MessageArgs("road.title", body, "common.ok", closable: true));
        }
    }
}
