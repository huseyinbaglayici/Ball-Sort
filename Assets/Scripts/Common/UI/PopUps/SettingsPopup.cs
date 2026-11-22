using System;
using System.Collections;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Services;
using Project.Core.Settings;
using TMPro;
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

    // Switches saved at once, the main-menu rows (language, help, feedback) and the version footer.
    public sealed class SettingsPopup : Popup
    {
        private const string AppName = "Orbit Sort";
        // Mail apps take the app out of focus; staying in focus this long means none opened.
        private const float MailCheckSeconds = 1.5f;
        private const float ToastSeconds = 1.4f;

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

        [Header("Pages")]
        [SerializeField] private LocalizedText _title;
        [SerializeField] private GameObject _mainPage;
        [SerializeField] private GameObject _languagePage;
        [SerializeField] private GameObject _helpPage;
        [SerializeField] private Button[] _backButtons = Array.Empty<Button>();

        [Header("Rows")]
        // The well behind the rows; its height follows the rows shown.
        [SerializeField] private RectTransform _rowPanel;
        [SerializeField] private float _rowStep = 160f;
        [SerializeField] private float _rowPadding = 40f;
        [SerializeField] private Button _languageRow;
        [SerializeField] private TMP_Text _languageLabel;
        [SerializeField] private Button _helpRow;
        [SerializeField] private Button _feedbackRow;
        [SerializeField] private Button _copyRow;
        [SerializeField] private TMP_Text _copyLabel;
        [SerializeField] private RectTransform _languageList;
        [SerializeField] private Button _languageTemplate;

        [Header("Footer")]
        [SerializeField] private Button _footer;
        [SerializeField] private TMP_Text _footerLabel;
        [SerializeField] private Button _privacy;
        [SerializeField] private Button _terms;
        [SerializeField] private CanvasGroup _toast;
        [SerializeField] private TMP_Text _toastLabel;
        [SerializeField] private AppLinks _links;
        [SerializeField] private BuildInfo _buildInfo;

        private readonly List<Button> _languageButtons = new();
        private SettingsService _settings;
        private LocalizationService _localization;
        private HapticModule _hapticModule;
        private SettingsArgs _args;
        private SettingsRows _rows;
        private float _frameHeight;
        private bool _offerCopy;
        private bool _leftApp;

        protected override void Awake()
        {
            base.Awake();
            if (_frame != null) _frameHeight = _frame.sizeDelta.y;
            _settings = Services.Get<SettingsService>();
            _localization = Services.Get<LocalizationService>();
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

            foreach (var back in _backButtons) back.onClick.AddListener(() => ShowPage(_mainPage));
            if (_languageRow != null) _languageRow.onClick.AddListener(() => ShowPage(_languagePage));
            if (_helpRow != null) _helpRow.onClick.AddListener(() => ShowPage(_helpPage));
            if (_feedbackRow != null) _feedbackRow.onClick.AddListener(SendFeedback);
            if (_copyRow != null) _copyRow.onClick.AddListener(() => Copy(_links.SupportEmail));
            if (_footer != null) _footer.onClick.AddListener(() => Copy(FooterText));
            if (_privacy != null) _privacy.onClick.AddListener(() => UnityEngine.Application.OpenURL(_links.PrivacyUrl));
            if (_terms != null) _terms.onClick.AddListener(() => UnityEngine.Application.OpenURL(_links.TermsUrl));
            BuildLanguageList();
        }

        private void OnEnable() => EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
        private void OnDisable() => EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _leftApp = true;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _leftApp = true;
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
            if (_frame != null)
            {
                var extra = (_args.Restart != null ? 1 : 0) + (_args.Home != null ? 1 : 0);
                _frame.sizeDelta = new Vector2(_frame.sizeDelta.x, _frameHeight + _buttonStep * extra);
            }

            _rows = SettingsRows.For(_localization.Languages.Count, _links);
            _offerCopy = false;
            if (_toast != null) _toast.alpha = 0f;
            if (_footerLabel != null) _footerLabel.text = FooterText;
            ShowPage(_mainPage);
        }

        protected override void OnHidden() => StopAllCoroutines();

        private string FooterText => _buildInfo != null ? $"{AppName} · {_buildInfo.Label}" : AppName;

        // One page at a time; the title names the page.
        private void ShowPage(GameObject page)
        {
            if (_mainPage != null) _mainPage.SetActive(page == _mainPage);
            if (_languagePage != null) _languagePage.SetActive(page == _languagePage);
            if (_helpPage != null) _helpPage.SetActive(page == _helpPage);
            if (_title != null)
                _title.Key = page == _helpPage ? "help.title" : page == _languagePage ? "settings.language" : "settings.title";
            // The footer and its links belong to the main page.
            var main = page == _mainPage;
            if (_footer != null) _footer.gameObject.SetActive(main);
            if (_privacy != null) _privacy.gameObject.SetActive(main && _rows.Privacy);
            if (_terms != null) _terms.gameObject.SetActive(main && _rows.Terms);
            if (main) LayoutRows();
            if (page == _languagePage) RefreshLanguageList();
        }

        // Active rows stack from the panel top; the panel fits them.
        private void LayoutRows()
        {
            if (_rowPanel == null) return;
            if (_languageRow != null) _languageRow.gameObject.SetActive(_rows.Language);
            if (_feedbackRow != null) _feedbackRow.gameObject.SetActive(_rows.Feedback);
            if (_copyRow != null) _copyRow.gameObject.SetActive(_rows.Feedback && _offerCopy);
            RefreshLabels();

            var y = -_rowPadding - _rowStep * 0.5f;
            foreach (var row in new[] { _languageRow, _helpRow, _feedbackRow, _copyRow })
            {
                if (row == null || !row.gameObject.activeSelf) continue;
                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                y -= _rowStep;
            }
            _rowPanel.sizeDelta = new Vector2(_rowPanel.sizeDelta.x, _rowPadding * 2f + _rowStep * _rows.Count(_offerCopy));
        }

        private void RefreshLabels()
        {
            if (_languageLabel != null)
                _languageLabel.text = _localization.Format("settings.language_row", _localization.NativeName(_localization.LanguageCode));
            if (_copyLabel != null) _copyLabel.text = _localization.Format("settings.copy_address", _links != null ? _links.SupportEmail : "");
        }

        private void OnLanguageChanged(LanguageChanged _)
        {
            RefreshLabels();
            RefreshLanguageList();
        }

        private void BuildLanguageList()
        {
            if (_languageList == null || _languageTemplate == null) return;
            _languageTemplate.gameObject.SetActive(false);
            foreach (var code in _localization.Languages)
            {
                var button = Instantiate(_languageTemplate, _languageList);
                button.name = "Language_" + code;
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>(true).text = _localization.NativeName(code);
                var pick = code;
                button.onClick.AddListener(() =>
                {
                    _settings.SetLanguage(pick);
                    ShowPage(_mainPage);
                });
                _languageButtons.Add(button);
            }
        }

        // The current language shows its check mark.
        private void RefreshLanguageList()
        {
            for (var i = 0; i < _languageButtons.Count; i++)
            {
                var check = _languageButtons[i].transform.Find("Check");
                if (check != null) check.gameObject.SetActive(_localization.Languages[i] == _localization.LanguageCode);
            }
        }

        // A draft with version and device filled in; the player sends it from the mail app.
        private void SendFeedback()
        {
            var body = "\n\n---\n" + FooterText + "\n" + SystemInfo.deviceModel + " · " + SystemInfo.operatingSystem;
            _leftApp = false;
            UnityEngine.Application.OpenURL(SettingsRows.FeedbackMail(_links.SupportEmail,
                _localization.Format("settings.feedback_subject", AppName), body));
            StopAllCoroutines();
            StartCoroutine(OfferCopyIfMailStayedShut());
        }

        private IEnumerator OfferCopyIfMailStayedShut()
        {
            yield return new WaitForSecondsRealtime(MailCheckSeconds);
            if (_leftApp || _offerCopy) yield break;
            _offerCopy = true;
            if (_mainPage == null || _mainPage.activeSelf) LayoutRows();
        }

        private void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUIUtility.systemCopyBuffer = text;
            if (_toast == null) return;
            if (_toastLabel != null) _toastLabel.text = _localization.Get("settings.copied");
            PrimeTween.Tween.StopAll(_toast);
            _toast.alpha = 1f;
            PrimeTween.Tween.Alpha(_toast, 0f, 0.3f, PrimeTween.Ease.InQuad, startDelay: ToastSeconds, useUnscaledTime: true);
        }

        private void Run(Action action)
        {
            Close();
            action?.Invoke();
        }
    }
}
