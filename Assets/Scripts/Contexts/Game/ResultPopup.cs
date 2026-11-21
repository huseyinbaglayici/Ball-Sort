using System;
using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Game
{
    // Pressing an enabled option closes the popup, then runs it.
    public readonly struct ResultOption
    {
        public readonly string Label;
        public readonly Action Run;
        public readonly bool Enabled;

        public ResultOption(string label, Action run, bool enabled = true)
        {
            Label = label;
            Run = run;
            Enabled = enabled;
        }
    }

    public readonly struct ResultArgs
    {
        public readonly bool Won;
        public readonly int Level;
        public readonly LevelResult Result;
        // The first one is the big main button, the others smaller ones under it, in order.
        public readonly IReadOnlyList<ResultOption> Options;
        // Wins only; reports whether the coins were doubled. Null hides it.
        public readonly Action<Action<bool>> Double;
        // Line under the status after a loss (why, lives, why an offer is missing).
        public readonly string Note;
        // The popup closes first. Null hides the X.
        public readonly Action Close;

        private ResultArgs(bool won, int level, LevelResult result, IReadOnlyList<ResultOption> options,
            Action<Action<bool>> @double, string note, Action close)
        {
            Won = won;
            Level = level;
            Result = result;
            Options = options;
            Double = @double;
            Note = note;
            Close = close;
        }

        public static ResultArgs Win(LevelResult result, IReadOnlyList<ResultOption> options, Action<Action<bool>> @double) =>
            new(true, result.Level, result, options, @double, null, null);

        public static ResultArgs Loss(int level, IReadOnlyList<ResultOption> options, string note, Action close) =>
            new(false, level, default, options, null, note, close);
    }

    // Ribbon, a picture in the well, the coins a win paid, then a main button and smaller ones under it.
    public sealed class ResultPopup : Popup
    {
        private const float OptionSpacing = 240f;

        [SerializeField] private RectTransform _frame;
        [SerializeField] private Image _ribbon;
        [SerializeField] private Sprite _winRibbon;
        [SerializeField] private Sprite _lossRibbon;
        [SerializeField] private LocalizedText _title;
        [SerializeField] private GameObject _winPicture;
        [SerializeField] private GameObject _lossPicture;
        [SerializeField] private GameObject _rewards;
        [SerializeField] private TMP_Text _reward;
        // Sack for a plain win's coins, chest for hard wins and road gold.
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private Sprite _coinSack;
        [SerializeField] private Sprite _coinChest;
        [SerializeField] private TMP_Text _details;
        [SerializeField] private Button _double;
        [SerializeField] private Button _close;
        [SerializeField] private Button _primary;
        [SerializeField] private Button _optionTemplate;
        // Frame height and main button y (from the frame top) for a win and for a fail.
        [SerializeField] private Vector2 _winLayout = new(1331f, -1061f);
        [SerializeField] private Vector2 _lossLayout = new(1331f, -1061f);

        private readonly List<Button> _options = new();
        private ResultArgs _result;
        private int _shownCoins;
        private float _frameHeight;
        private Vector2 _frameRest;

        protected override void Awake()
        {
            base.Awake();
            _frameRest = _frame.anchoredPosition;
            _optionTemplate.gameObject.SetActive(false);
            if (_double != null) _double.onClick.AddListener(OnDouble);
            _close.onClick.AddListener(() =>
            {
                Close();
                _result.Close?.Invoke();
            });
        }

        protected override void OnShow(object args)
        {
            _result = args is ResultArgs r ? r : default;
            var loc = Services.Get<LocalizationService>();
            var won = _result.Won;
            var layout = won ? _winLayout : _lossLayout;
            _frameHeight = layout.x;
            ((RectTransform)_primary.transform).anchoredPosition = new Vector2(0f, layout.y);

            _ribbon.sprite = won ? _winRibbon : _lossRibbon;
            _title.Key = won ? "result.win" : "result.failed";
            _winPicture.SetActive(won);
            _lossPicture.SetActive(!won);
            _close.gameObject.SetActive(_result.Close != null);
            if (_double != null)
            {
                _double.gameObject.SetActive(_result.Double != null);
                ((RectTransform)_double.transform).anchoredPosition = new Vector2(0f, layout.y - OptionSpacing);
            }

            _shownCoins = won ? _result.Result.TotalCoins : 0;
            _rewards.SetActive(_shownCoins > 0);
            _reward.text = $"+{_shownCoins}";
            if (_rewardIcon != null) _rewardIcon.sprite = IsBigReward(_result.Result) ? _coinChest : _coinSack;
            _details.text = won ? WinDetails(_result.Result, loc) : _result.Note ?? string.Empty;
            BuildOptions(_result.Options);
        }

        private static bool IsBigReward(in LevelResult level) =>
            level.Difficulty != LevelDifficulty.Normal || level.Road.Coins > 0;

        private static string WinDetails(LevelResult level, LocalizationService loc)
        {
            if (!level.FirstWin) return string.Empty;
            var road = EconomyText.Road(level.Road, loc);
            return road.Length > 0 ? loc.Format("result.road", road) : string.Empty;
        }

        // The first option is the main button; the rest stack below it and the frame grows to fit.
        private void BuildOptions(IReadOnlyList<ResultOption> options)
        {
            var count = options?.Count ?? 0;
            _primary.gameObject.SetActive(count > 0);
            if (count > 0) Bind(_primary, options[0]);

            var secondary = Mathf.Max(0, count - 1);
            while (_options.Count < secondary)
            {
                var copy = Instantiate(_optionTemplate, _optionTemplate.transform.parent, false);
                copy.name = $"Option{_options.Count}";
                _options.Add(copy);
            }

            var extra = secondary + (_double != null && _double.gameObject.activeSelf ? 1 : 0);
            _frame.sizeDelta = new Vector2(_frame.sizeDelta.x, _frameHeight + OptionSpacing * extra);
            // Grow both ways so a tall frame stays centred on short screens.
            _frame.anchoredPosition = _frameRest + new Vector2(0f, OptionSpacing * extra * 0.5f);
            var start = ((RectTransform)_primary.transform).anchoredPosition - new Vector2(0f, OptionSpacing);
            for (var i = 0; i < _options.Count; i++)
            {
                var button = _options[i];
                var visible = i < secondary;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                ((RectTransform)button.transform).anchoredPosition = start + new Vector2(0, -OptionSpacing * i);
                Bind(button, options[i + 1]);
            }
        }

        private void Bind(Button button, ResultOption option)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = option.Label;
            button.interactable = option.Enabled;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Close();
                option.Run?.Invoke();
            });
        }

        private void OnDouble()
        {
            _double.interactable = false;
            _result.Double?.Invoke(doubled =>
            {
                if (this == null) return;
                _double.gameObject.SetActive(false);
                _double.interactable = true;
                if (!doubled) return;
                _shownCoins += _result.Result.BaseCoins;
                _reward.text = $"+{_shownCoins}";
            });
        }
    }
}
