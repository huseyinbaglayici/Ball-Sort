using System;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Store
{
    public readonly struct LivesArgs
    {
        public readonly int Level;
        // Shown instead of the offers once a life is there.
        public readonly Action Play;
        // Opens this panel again after the coin shop closes.
        public readonly Action Reopen;

        public LivesArgs(int level, Action play, Action reopen)
        {
            Level = level;
            Play = play;
            Reopen = reopen;
        }
    }

    // Out of lives on Home: the next free life's timer, a coin refill of every life and a rewarded +1.
    public sealed class LivesPopup : Popup
    {
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private Button _refill;
        [SerializeField] private LocalizedText _refillLabel;
        [SerializeField] private GameObject _refillPrice;
        [SerializeField] private Button _ad;
        [SerializeField] private ShopPopup _shopPopup;

        private LivesService _lives;
        private WalletService _wallet;
        private MonetizationService _ads;
        private LocalizationService _loc;
        private PopupService _popups;
        private LivesArgs _args;
        private bool _shownPlay;

        protected override void Awake()
        {
            base.Awake();
            _lives = Services.Get<LivesService>();
            _wallet = Services.Get<WalletService>();
            _ads = Services.Get<MonetizationService>();
            _loc = Services.Get<LocalizationService>();
            _popups = Services.Get<PopupService>();
            _close.onClick.AddListener(Close);
            _refill.onClick.AddListener(Refill);
            _ad.onClick.AddListener(WatchAd);
        }

        protected override void OnShow(object args)
        {
            _args = args is LivesArgs a ? a : default;
            _refillPrice.GetComponentInChildren<TMP_Text>(true).text = CampaignRules.LifeRefillPrice.ToString();
            _shownPlay = !_lives.CanPlay;
            Refresh();
        }

        private void Update()
        {
            if (IsOpen) Refresh();
        }

        // A free life that arrives while open turns the main button into Play.
        private void Refresh()
        {
            var canPlay = _lives.CanPlay;
            _timer.text = canPlay ? _loc.Get("lives.arrived") : _loc.Format("lives.out_body", EconomyText.Timer(_lives.NextLifeIn));
            if (canPlay == _shownPlay) return;
            _shownPlay = canPlay;
            _refillLabel.Key = canPlay ? "lives.play" : "lives.refill_all";
            _refillPrice.SetActive(!canPlay);
            _ad.gameObject.SetActive(!canPlay && _ads.CanOfferRewardedLife);
        }

        private void Refill()
        {
            switch (_lives.BuyRefill(_wallet))
            {
                case RefillResult.NoCoins:
                    // The coin shop, then back here with nothing spent.
                    Close();
                    _popups.Show(_shopPopup, new ShopArgs(StoreSection.Coins, _args.Reopen));
                    return;
                case RefillResult.Refilled:
                    Close();
                    return;
                default:
                    Close();
                    _args.Play?.Invoke();
                    return;
            }
        }

        private void WatchAd() => _ads.ShowRewardedLife(_args.Level, earned =>
        {
            if (earned) Close();
        });
    }
}
