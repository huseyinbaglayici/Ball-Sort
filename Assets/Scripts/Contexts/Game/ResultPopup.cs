using System;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Game
{
    public readonly struct ResultArgs
    {
        public readonly bool Won;
        public readonly int Reward;
        /// <summary>Runs when the button is pressed, after the popup starts closing.</summary>
        public readonly Action Continue;

        public ResultArgs(bool won, int reward, Action onContinue)
        {
            Won = won;
            Reward = reward;
            Continue = onContinue;
        }
    }

    /// <summary>
    /// Win/lose popup: a title, the reward after a win, and one button ("Continue" after a win, "Try again" after a
    /// loss) that closes it and runs the caller's action.
    /// </summary>
    public sealed class ResultPopup : Popup
    {
        [SerializeField] private LocalizedText _title;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private Button _continue;
        [SerializeField] private LocalizedText _continueLabel;

        private ResultArgs _result;

        protected override void Awake()
        {
            base.Awake();
            _continue.onClick.AddListener(() =>
            {
                Close();
                _result.Continue?.Invoke();
            });
        }

        protected override void OnShow(object args)
        {
            var result = args is ResultArgs r ? r : default;
            _result = result;
            _title.Key = result.Won ? "result.win" : "result.lose";
            _reward.text = result.Won ? $"+{result.Reward}" : string.Empty;
            _continueLabel.Key = result.Won ? "common.continue" : "common.retry";
        }
    }
}
