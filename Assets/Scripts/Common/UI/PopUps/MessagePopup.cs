using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Common.UI.PopUps
{
    public readonly struct MessageArgs
    {
        public readonly string TitleKey;
        public readonly string Body;
        public readonly string ButtonKey;
        // Runs after the popup starts closing.
        public readonly Action Confirm;
        // The X closes without running the action; off for notices that must lead somewhere.
        public readonly bool Closable;
        // Difficulty tab above the frame; empty hides it.
        public readonly string Header;
        public readonly int HeaderMarks;
        // Broken heart: the action costs a life.
        public readonly bool Heart;

        public MessageArgs(string titleKey, string body, string buttonKey, Action confirm = null, bool closable = false,
            string header = null, int headerMarks = 0, bool heart = false)
        {
            TitleKey = titleKey;
            Body = body;
            ButtonKey = buttonKey;
            Confirm = confirm;
            Closable = closable;
            Header = header;
            HeaderMarks = headerMarks;
            Heart = heart;
        }
    }

    public sealed class MessagePopup : Popup
    {
        [SerializeField] private LocalizedText _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button _button;
        [SerializeField] private LocalizedText _buttonLabel;
        [SerializeField] private Button _close;
        [SerializeField] private DifficultyTag _header;
        [SerializeField] private GameObject _heart;
        // Title ribbon: the info art, or the loss art when a life is at stake.
        [SerializeField] private Image _ribbon;
        [SerializeField] private Sprite _infoRibbon;
        [SerializeField] private Sprite _lossRibbon;

        private MessageArgs _args;

        protected override void Awake()
        {
            base.Awake();
            _button.onClick.AddListener(() =>
            {
                Close();
                _args.Confirm?.Invoke();
            });
            if (_close != null) _close.onClick.AddListener(Close);
        }

        protected override void OnShow(object args)
        {
            _args = args is MessageArgs m ? m : default;
            _title.Key = _args.TitleKey;
            _body.text = _args.Body ?? string.Empty;
            _buttonLabel.Key = string.IsNullOrEmpty(_args.ButtonKey) ? "common.continue" : _args.ButtonKey;
            if (_close != null) _close.gameObject.SetActive(_args.Closable);
            if (_header != null) _header.Set(_args.Header, _args.HeaderMarks);
            if (_heart != null) _heart.SetActive(_args.Heart);
            // Under the heart, or centered in the well without it.
            _body.alignment = _args.Heart ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
            if (_ribbon != null) _ribbon.sprite = _args.Heart ? _lossRibbon : _infoRibbon;
        }
    }
}
