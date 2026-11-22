using System;
using PrimeTween;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Core.Audio;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.LiveOps
{
    // Seven day cells: claimed ones ticked, today's glowing; Claim pays today's gold.
    public sealed class DailyRewardPopup : Popup
    {
        [Serializable]
        public struct DayCell
        {
            public RectTransform Root;
            public TMP_Text Day;
            public TMP_Text Amount;
            public Image Icon;
            public GameObject Today;
            public GameObject Tick;
        }

        [SerializeField] private DayCell[] _days = Array.Empty<DayCell>();
        [SerializeField] private Button _claim;
        [SerializeField] private LocalizedText _claimLabel;
        [SerializeField] private Button _close;
        [SerializeField] private RectTransform _coinTarget;
        [SerializeField] private Sprite _coin;
        [SerializeField] private float _closeDelay = 1.1f;

        private DailyRewardService _daily;
        private LocalizationService _localization;
        private Tween _pulse;
        private Tween _autoClose;

        protected override void Awake()
        {
            base.Awake();
            _daily = Services.Get<DailyRewardService>();
            _localization = Services.Get<LocalizationService>();
            _claim.onClick.AddListener(Claim);
            _close.onClick.AddListener(Close);
        }

        protected override void OnShow(object args) => Refresh();

        protected override void OnHidden()
        {
            _pulse.Stop();
            // A delayed close left over from a claim must not close the popup when it is opened again.
            _autoClose.Stop();
        }

        private void Refresh()
        {
            var today = _daily.CurrentIndex;
            var claimable = _daily.CanClaim;
            for (var i = 0; i < _days.Length; i++)
            {
                var cell = _days[i];
                var claimed = _daily.IsClaimed(i);
                cell.Day.text = _localization.Format("daily.day", i + 1);
                cell.Amount.text = DailyRewardService.Rewards[i].ToString();
                cell.Tick.SetActive(claimed);
                cell.Today.SetActive(i == today && claimable);
                cell.Icon.color = claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            }

            _claim.interactable = claimable;
            _claimLabel.Key = claimable ? "daily.claim" : "daily.come_back";
            _pulse.Stop();
            if (!claimable || today >= _days.Length) return;
            var glow = _days[today].Today.transform;
            glow.localScale = Vector3.one;
            _pulse = Tween.Scale(glow, 1.08f, 0.6f, Ease.InOutSine, -1, CycleMode.Yoyo, useUnscaledTime: true);
        }

        private void Claim()
        {
            var index = _daily.CurrentIndex;
            if (_daily.Claim() <= 0) return;
            Services.Get<AudioManager>().PlayOneShotSound("coin_double");
            CoinBurst.Play(_days[index].Icon.rectTransform, _coinTarget, _coin);
            Refresh();
            _autoClose = Tween.Delay(this, _closeDelay, popup => popup.Close(), useUnscaledTime: true);
        }
    }
}
