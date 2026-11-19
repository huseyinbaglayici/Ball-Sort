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
        // The last one is the big main button, the others smaller ones under it, in order.
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

    // A fail shows the coins a win would pay; buttons are copies of a main and a smaller template.
    public sealed class ResultPopup : Popup
    {
        private const float OptionSpacing = 120f;

        [SerializeField] private RectTransform _frame;
        [SerializeField] private DifficultyTag _header;
        [SerializeField] private TMP_Text _levelTitle;
        [SerializeField] private LocalizedText _status;
        [SerializeField] private GameObject _statusHeart;
        [SerializeField] private GameObject _rewards;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private TMP_Text _details;
        [SerializeField] private Button _double;
        [SerializeField] private Button _close;
        [SerializeField] private Button _primary;
        [SerializeField] private Button _optionTemplate;

        private readonly List<Button> _options = new();
        private ResultArgs _result;
        private int _shownCoins;
        private float _frameHeight;

        protected override void Awake()
        {
            base.Awake();
            _frameHeight = _frame.sizeDelta.y;
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
            var difficulty = CampaignRules.DifficultyOf(_result.Level);

            _header.Set(EconomyText.Difficulty(difficulty, loc), EconomyText.DifficultyMarks(difficulty));
            _levelTitle.text = loc.Format("home.level", _result.Level);
            _status.Key = _result.Won ? "result.complete" : "result.failed";
            _statusHeart.SetActive(!_result.Won);
            _close.gameObject.SetActive(_result.Close != null);
            if (_double != null) _double.gameObject.SetActive(_result.Double != null);

            // A win shows what it paid; a fail shows what a win of this level would pay.
            _shownCoins = _result.Won ? _result.Result.TotalCoins : CampaignRules.FirstWinCoins(_result.Level);
            _rewards.SetActive(_shownCoins > 0);
            _reward.text = _shownCoins.ToString();
            _details.text = _result.Won ? WinDetails(_result.Result, loc) : _result.Note ?? string.Empty;
            BuildOptions(_result.Options);
        }

        private static string WinDetails(LevelResult level, LocalizationService loc)
        {
            if (!level.FirstWin) return string.Empty;
            var road = EconomyText.Road(level.Road, loc);
            return loc.Format("result.base", level.BaseCoins) + (road.Length > 0 ? "\n" + loc.Format("result.road", road) : string.Empty);
        }

        private void BuildOptions(IReadOnlyList<ResultOption> options)
        {
            var count = options?.Count ?? 0;
            _primary.gameObject.SetActive(count > 0);
            if (count > 0) Bind(_primary, options[count - 1]);

            var secondary = Mathf.Max(0, count - 1);
            while (_options.Count < secondary)
            {
                var copy = Instantiate(_optionTemplate, _optionTemplate.transform.parent, false);
                copy.name = $"Option{_options.Count}";
                _options.Add(copy);
            }

            // The frame grows downwards by one row per smaller button; its top stays where it is.
            _frame.sizeDelta = new Vector2(_frame.sizeDelta.x, _frameHeight + OptionSpacing * secondary);
            var start = ((RectTransform)_optionTemplate.transform).anchoredPosition;
            for (var i = 0; i < _options.Count; i++)
            {
                var button = _options[i];
                var visible = i < secondary;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                ((RectTransform)button.transform).anchoredPosition = start + new Vector2(0, -OptionSpacing * i);
                Bind(button, options[i]);
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
                _reward.text = _shownCoins.ToString();
            });
        }
    }
}
