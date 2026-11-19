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
        [SerializeField] private TMP_Text _rewardTag;
        [SerializeField] private TMP_Text _metaText;
        [SerializeField] private Button _roadButton;
        [SerializeField] private TMP_Text _roadText;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _starterPackButton;
        [SerializeField] private Button _challengeButton;
        [SerializeField] private Button _rewardedLifeButton;
        [SerializeField] private Button[] _comingSoon = System.Array.Empty<Button>();
        [SerializeField] private MessagePopup _messagePopup;

        private ProgressService _progress;
        private LivesService _lives;
        private MonetizationService _ads;
        private LocalizationService _localization;

        private void Start()
        {
            _progress = Services.Get<ProgressService>();
            _lives = Services.Get<LivesService>();
            _ads = Services.Get<MonetizationService>();
            _localization = Services.Get<LocalizationService>();

            _playButton.onClick.AddListener(() =>
            {
                if (CanPlay) _ = Services.Get<MainFlow>().GoToGameAsync();
            });
            if (_roadButton != null) _roadButton.onClick.AddListener(ShowRoadReward);
            if (_starterPackButton != null)
                _starterPackButton.onClick.AddListener(() => ShowMessage("offer.starter_title", "offer.preview_body"));
            if (_challengeButton != null)
                _challengeButton.onClick.AddListener(() => ShowMessage("challenge.title", "challenge.soon_body"));
            if (_rewardedLifeButton != null)
                _rewardedLifeButton.onClick.AddListener(() => _ads.ShowRewardedLife(_ => Refresh()));
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

        private bool CanPlay => !_progress.IsCampaignFinished && _lives.CanPlay;

        private void ShowMessage(string titleKey, string bodyKey) =>
            Services.Get<PopupService>().Show(_messagePopup,
                new MessageArgs(titleKey, _localization.Get(bodyKey), "common.ok", closable: true));

        private void Refresh()
        {
            if (_progress == null) return;
            var finished = _progress.IsCampaignFinished;
            var level = _progress.Level;
            var difficulty = finished ? LevelDifficulty.Normal : CampaignRules.DifficultyOf(level);
            _levelText.text = finished ? _localization.Get("meta.final_title") : _localization.Format("home.level", level);
            if (_difficulty != null)
                _difficulty.Set(EconomyText.Difficulty(difficulty, _localization), EconomyText.DifficultyMarks(difficulty));
            if (_rewardTag != null)
            {
                _rewardTag.transform.parent.gameObject.SetActive(!finished);
                _rewardTag.text = $"+{CampaignRules.FirstWinCoins(level)}";
            }
            _playButton.interactable = CanPlay;

            if (_metaText != null)
                _metaText.text = finished
                    ? _localization.Get("meta.final_body")
                    : _localization.Format("meta.period", _progress.MetaPeriod,
                        CampaignRules.LevelsPerMetaPeriod * _progress.MetaPeriod);
            var hasRoad = !finished && CampaignRules.NextRoadLevel(level) > 0;
            if (_roadButton != null) _roadButton.gameObject.SetActive(hasRoad);
            if (_roadText != null && hasRoad) _roadText.text = RoadProgress(level);
            if (_starterPackButton != null)
                _starterPackButton.gameObject.SetActive(_progress.IsUnlocked(Feature.StarterPack));
            if (_challengeButton != null)
                _challengeButton.gameObject.SetActive(_progress.IsUnlocked(Feature.Challenge));
            if (_rewardedLifeButton != null)
                _rewardedLifeButton.gameObject.SetActive(!finished && _ads.CanOfferRewardedLife);
        }

        // Levels won since the previous road reward, out of the levels between it and the next.
        private static string RoadProgress(int level)
        {
            var previous = CampaignRules.PreviousRoadLevel(level);
            return $"{level - 1 - previous}/{CampaignRules.NextRoadLevel(level) - previous}";
        }

        private void ShowRoadReward()
        {
            var next = CampaignRules.NextRoadLevel(_progress.Level);
            if (next == 0) return;
            var body = _localization.Format("road.body", next, EconomyText.Road(CampaignRules.RoadRewardAt(next), _localization));
            Services.Get<PopupService>().Show(_messagePopup, new MessageArgs("road.title", body, "common.ok", closable: true));
        }
    }
}
