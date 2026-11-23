using System;
using System.Collections.Generic;
using System.Text;
using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Core.Audio;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Store
{
    public readonly struct ShopArgs
    {
        public readonly StoreSection Section;
        // Runs after the X, so the caller can bring back the panel it came from.
        public readonly Action Closed;

        public ShopArgs(StoreSection section, Action closed = null)
        {
            Section = section;
            Closed = closed;
        }
    }

    // The store page: beginner offer, bundles and No Ads, then coin packs; cards are laid out from the catalog.
    public sealed class ShopPopup : Popup
    {
        private const float StatusTime = 1.6f;

        [SerializeField] private Button _close;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _list;
        [SerializeField] private TMP_Text _headerTemplate;
        [SerializeField] private RectTransform _cardTemplate;
        [SerializeField] private TMP_Text _demo;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Sprite _beginnerIcon;
        [SerializeField] private Sprite _bundleIcon;
        [SerializeField] private Sprite _noAdsIcon;
        [SerializeField] private Sprite[] _coinIcons = Array.Empty<Sprite>();

        private readonly List<GameObject> _rows = new();
        private StoreService _store;
        private LocalizationService _loc;
        private AudioManager _audio;
        private ShopArgs _args;
        private TMP_Text _beginnerTimer;
        private bool _buying;

        protected override void Awake()
        {
            base.Awake();
            _store = Services.Get<StoreService>();
            _loc = Services.Get<LocalizationService>();
            _audio = Services.Get<AudioManager>();
            _headerTemplate.gameObject.SetActive(false);
            _cardTemplate.gameObject.SetActive(false);
            _close.onClick.AddListener(() =>
            {
                Close();
                _args.Closed?.Invoke();
            });
        }

        protected override void OnShow(object args)
        {
            _args = args is ShopArgs a ? a : default;
            Entrance = PopupEntrance.None;
            _buying = false;
            _status.alpha = 0f;
            _demo.gameObject.SetActive(_store.IsDemo);
            Rebuild();
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = _args.Section == StoreSection.Coins ? 0f : 1f;
        }

        protected override void OnHidden() => PrimeTween.Tween.StopAll(_status);

        private void Update()
        {
            if (_beginnerTimer != null)
                _beginnerTimer.text = _loc.Format("store.ends_in", EconomyText.Timer(_store.BeginnerLeft));
        }

        private void Rebuild()
        {
            // Hidden first: Destroy waits for the frame's end and the layout is measured now.
            foreach (var row in _rows)
            {
                row.SetActive(false);
                Destroy(row);
            }
            _rows.Clear();
            _beginnerTimer = null;

            Header("store.offers");
            if (_store.BeginnerAvailable)
            {
                _store.MarkBeginnerShown();
                _beginnerTimer = Card(StoreCatalog.Beginner, _beginnerIcon, true);
            }
            foreach (var bundle in StoreCatalog.Bundles) Card(bundle, _bundleIcon, false);
            if (_store.NoAdsOffered) Card(StoreCatalog.NoAds, _noAdsIcon, false);

            Header("store.coins");
            for (var i = 0; i < StoreCatalog.CoinPacks.Length; i++)
            {
                var icon = _coinIcons.Length == 0 ? null : _coinIcons[Mathf.Min(i, _coinIcons.Length - 1)];
                Card(StoreCatalog.CoinPacks[i], icon, false);
            }
        }

        private void Header(string key)
        {
            var header = Instantiate(_headerTemplate, _list, false);
            header.gameObject.SetActive(true);
            header.text = _loc.Get(key);
            _rows.Add(header.gameObject);
        }

        // Returns the card's timer line when it shows one.
        private TMP_Text Card(StoreProduct product, Sprite icon, bool timed)
        {
            var card = Instantiate(_cardTemplate, _list, false);
            card.gameObject.SetActive(true);
            _rows.Add(card.gameObject);
            var iconImage = card.Find("Icon").GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            card.Find("Title").GetComponent<TMP_Text>().text = product.Coins > 0 && product.TitleKey == "store.coins"
                ? _loc.Format("store.coin_amount", product.Coins)
                : _loc.Get(product.TitleKey);
            var contents = Contents(product);
            card.Find("Contents").GetComponent<TMP_Text>().text = contents;
            // A card with only a title centres it.
            if (contents.Length == 0 && !timed)
            {
                var title = (RectTransform)card.Find("Title");
                title.anchoredPosition = new Vector2(title.anchoredPosition.x, 0f);
            }
            var timer = card.Find("Timer").GetComponent<TMP_Text>();
            timer.gameObject.SetActive(timed);
            var buy = card.Find("Buy").GetComponent<Button>();
            buy.GetComponentInChildren<TMP_Text>(true).text = product.Price;
            buy.onClick.AddListener(() => Buy(product));
            return timed ? timer : null;
        }

        private string Contents(StoreProduct product)
        {
            if (product.NoAds) return _loc.Get("store.no_ads_short");
            // Coins on the first line, the boosters on the second.
            var text = new StringBuilder();
            if (product.Coins > 0 && product.TitleKey != "store.coins") text.Append(_loc.Format("store.coin_amount", product.Coins));
            var boosters = new StringBuilder();
            foreach (var type in CampaignRules.AllBoosters)
            {
                var count = product.Boosters(type);
                if (count <= 0) continue;
                if (boosters.Length > 0) boosters.Append(" · ");
                boosters.Append(count).Append(' ').Append(_loc.Get(BoosterKey(type)));
            }
            if (text.Length > 0 && boosters.Length > 0) text.Append('\n');
            return text.Append(boosters).ToString();
        }

        private static string BoosterKey(BoosterType type) => type switch
        {
            BoosterType.Undo => "booster.undo",
            BoosterType.ExtraTube => "booster.extra_tube",
            _ => "booster.free_move"
        };

        private void Buy(StoreProduct product)
        {
            if (_buying) return;
            _buying = true;
            _store.Buy(product, bought =>
            {
                _buying = false;
                if (!bought || !IsOpen) return;
                _audio.PlayOneShotSound("coin");
                Rebuild();
                _status.text = _loc.Get("store.bought");
                PrimeTween.Tween.StopAll(_status);
                _status.alpha = 1f;
                PrimeTween.Tween.Alpha(_status, 0f, 0.4f, startDelay: StatusTime, useUnscaledTime: true);
            });
        }
    }
}
