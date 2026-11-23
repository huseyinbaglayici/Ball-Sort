using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Main;
using Project.Contexts.Store;
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
        // Play button label with the level number.
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private HomePoster _poster;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _shopButton;
        // Shown once the first interstitial has played, until No Ads is owned.
        [SerializeField] private Button _noAdsButton;
        [SerializeField] private Button _settingsButton;
        // Tabs for screens not in the game yet.
        [SerializeField] private Button[] _comingSoon = System.Array.Empty<Button>();
        [SerializeField] private SettingsPopup _settingsPopup;
        [SerializeField] private MessagePopup _messagePopup;
        [SerializeField] private ShopPopup _shopPopup;
        [SerializeField] private NoAdsPopup _noAdsPopup;
        [SerializeField] private LivesPopup _livesPopup;
        // The level orb the level-open zoom grows out of, and what fades away around it.
        [SerializeField] private RectTransform _orb;
        // Full-screen Home content; it pushes in about the orb while a level opens.
        [SerializeField] private RectTransform _content;
        [SerializeField] private CanvasGroup[] _chrome = System.Array.Empty<CanvasGroup>();
        [SerializeField] private float _leaveTime = 0.22f;

        private ProgressService _progress;
        private LivesService _lives;
        private LocalizationService _localization;
        private PopupService _popups;
        private StoreService _store;

        private void Start()
        {
            _progress = Services.Get<ProgressService>();
            _lives = Services.Get<LivesService>();
            _localization = Services.Get<LocalizationService>();
            _popups = Services.Get<PopupService>();
            _store = Services.Get<StoreService>();

            _playButton.onClick.AddListener(Play);
            if (_shopButton != null) _shopButton.onClick.AddListener(() => _popups.Show(_shopPopup, new ShopArgs(StoreSection.Offers)));
            if (_noAdsButton != null) _noAdsButton.onClick.AddListener(() => _popups.Show(_noAdsPopup));
            if (_settingsButton != null) _settingsButton.onClick.AddListener(ShowSettings);
            foreach (var tab in _comingSoon)
                if (tab != null) tab.onClick.AddListener(() => ShowMessage("nav.soon_title", "nav.soon_body"));

            Refresh();
        }

        public RectTransform Orb => _orb;

        public void SetOpen(bool open)
        {
            _view.SetActive(open);
            if (!open) return;
            foreach (var group in _chrome)
            {
                PrimeTween.Tween.StopAll(group);
                group.alpha = 1f;
            }
            Push(1f);
            Refresh();
        }

        // Scales the content about the orb; 1 puts it back.
        public void Push(float scale)
        {
            if (_content == null) return;
            if (_orb != null)
            {
                var corners = new Vector3[4];
                _orb.GetWorldCorners(corners);
                var local = _content.InverseTransformPoint((corners[0] + corners[2]) * 0.5f);
                var rect = _content.rect;
                _content.pivot = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x), Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            }
            _content.localScale = Vector3.one * scale;
        }

        // Bars and mascot fade out, leaving the orb alone for the zoom.
        public void Leave()
        {
            foreach (var group in _chrome)
                PrimeTween.Tween.Alpha(group, 0f, _leaveTime, PrimeTween.Ease.OutQuad, useUnscaledTime: true);
        }

        private void OnEnable()
        {
            EventBus<LivesChanged>.Subscribe(OnLivesChanged);
            EventBus<LevelCompleted>.Subscribe(OnLevelCompleted);
            EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
            EventBus<StorePurchased>.Subscribe(OnPurchased);
        }

        private void OnDisable()
        {
            EventBus<LivesChanged>.Unsubscribe(OnLivesChanged);
            EventBus<LevelCompleted>.Unsubscribe(OnLevelCompleted);
            EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);
            EventBus<StorePurchased>.Unsubscribe(OnPurchased);
        }

        private void OnLivesChanged(LivesChanged _) => Refresh();
        private void OnLevelCompleted(LevelCompleted _) => Refresh();
        private void OnLanguageChanged(LanguageChanged _) => Refresh();
        private void OnPurchased(StorePurchased _) => Refresh();

        // With no life left Play offers a refill, a rewarded life and the next free life's timer.
        private void Play()
        {
            if (_progress.IsCampaignFinished) return;
            if (!_lives.CanPlay)
            {
                ShowLives();
                return;
            }
            _ = Services.Get<MainFlow>().GoToGameAsync();
        }

        private void ShowLives() => _popups.Show(_livesPopup, new LivesArgs(_progress.Level, Play, ShowLives));

        private void ShowSettings() => _popups.Show(_settingsPopup, new SettingsArgs(null, null, false));

        private void ShowMessage(string titleKey, string bodyKey) =>
            _popups.Show(_messagePopup, new MessageArgs(titleKey, _localization.Get(bodyKey), "common.ok", closable: true));

        // Hidden Home waits for SetOpen, so a new chapter's poster fades in where the player can see it.
        private void Refresh()
        {
            if (_progress == null || !_view.activeSelf) return;
            var finished = _progress.IsCampaignFinished;
            var level = _progress.Level;
            _levelText.text = finished ? _localization.Get("meta.final_title") : _localization.Format("home.level", level);
            if (_poster != null) _poster.Show(level);
            _playButton.interactable = !finished;
            if (_noAdsButton != null) _noAdsButton.gameObject.SetActive(_store.NoAdsOffered);
        }
    }
}
