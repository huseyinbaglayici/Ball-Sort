using System;
using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
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
        // Coins it costs (shown with a coin on the button); 0 hides it.
        public readonly int Price;
        // Owned count in the corner badge; 0 hides it.
        public readonly int Badge;
        // Spends a life: a "-1 life" line under the button.
        public readonly bool LifeCost;

        public ResultOption(string label, Action run, bool enabled = true, int price = 0, int badge = 0, bool lifeCost = false)
        {
            Label = label;
            Run = run;
            Enabled = enabled;
            Price = price;
            Badge = badge;
            LifeCost = lifeCost;
        }
    }

    public readonly struct ResultArgs
    {
        // The first one is the big main button, the others smaller ones under it, in order.
        public readonly IReadOnlyList<ResultOption> Options;
        // Line under the status (why, lives, why an offer is missing).
        public readonly string Note;
        // The popup closes first. Null hides the X.
        public readonly Action Close;

        public ResultArgs(IReadOnlyList<ResultOption> options, string note, Action close)
        {
            Options = options;
            Note = note;
            Close = close;
        }
    }

    // The fail panel: ribbon, a picture in the well, the reason, then a main button and smaller ones under it.
    public sealed class ResultPopup : Popup
    {
        private const float OptionSpacing = 170f;
        private const float PriceShift = 80f;
        private static readonly Color DisabledText = new(0.16f, 0.12f, 0.38f);

        [SerializeField] private RectTransform _frame;
        [SerializeField] private TMP_Text _details;
        [SerializeField] private Button _close;
        // "Hold to see the board" caption on the fail panel's floor.
        [SerializeField] private GameObject _peek;
        [SerializeField] private Button _primary;
        [SerializeField] private Button _optionTemplate;
        // Frame height and main button y from the frame top; the "-1 life" line and peek caption fit inside.
        [SerializeField] private Vector2 _layout = new(1030f, -750f);

        private readonly List<Button> _options = new();
        private ResultArgs _result;
        private float _frameHeight;
        private Vector2 _frameRest;

        protected override void Awake()
        {
            base.Awake();
            _frameRest = _frame.anchoredPosition;
            _optionTemplate.gameObject.SetActive(false);
            _close.onClick.AddListener(() =>
            {
                Close();
                _result.Close?.Invoke();
            });
        }

        protected override void OnShow(object args)
        {
            _result = args is ResultArgs r ? r : default;
            // The fail panel settles in after a short beat.
            Entrance = PopupEntrance.Settle;
            EntranceDelay = 0.05f;
            _frameHeight = _layout.x;
            ((RectTransform)_primary.transform).anchoredPosition = new Vector2(0f, _layout.y);
            _close.gameObject.SetActive(_result.Close != null);
            if (_peek != null) _peek.SetActive(true);
            _details.text = _result.Note ?? string.Empty;
            BuildOptions(_result.Options);
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

            var extra = secondary;
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
            var label = (RectTransform)button.transform.Find("Label");
            var labelText = label.GetComponent<TMP_Text>();
            labelText.text = option.Label;
            // The disabled sprite is light: switch to dark text so it stays readable.
            var textColor = option.Enabled ? Color.white : DisabledText;
            labelText.color = textColor;
            // A price chip sits left of the label and pushes it right.
            var price = button.transform.Find("Price");
            if (price != null)
            {
                price.gameObject.SetActive(option.Price > 0);
                if (option.Price > 0)
                {
                    var amount = price.GetComponentInChildren<TMP_Text>(true);
                    amount.text = option.Price.ToString();
                    amount.color = textColor;
                }
            }
            label.anchoredPosition = new Vector2(option.Price > 0 ? PriceShift : 0f, label.anchoredPosition.y);
            var badge = button.transform.Find("Badge");
            if (badge != null)
            {
                badge.gameObject.SetActive(option.Badge > 0);
                if (option.Badge > 0) badge.GetComponentInChildren<TMP_Text>(true).text = option.Badge.ToString();
            }
            var cost = button.transform.Find("Cost");
            if (cost != null) cost.gameObject.SetActive(option.LifeCost);
            button.interactable = option.Enabled;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Close();
                option.Run?.Invoke();
            });
        }
    }
}
