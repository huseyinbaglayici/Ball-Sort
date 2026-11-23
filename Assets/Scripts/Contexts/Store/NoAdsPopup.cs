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
    // The permanent No Ads offer: what it removes, its price, buy and restore.
    public sealed class NoAdsPopup : Popup
    {
        [SerializeField] private Button _close;
        [SerializeField] private Button _buy;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private Button _restore;
        [SerializeField] private TMP_Text _demo;
        [SerializeField] private TMP_Text _status;

        private StoreService _store;
        private LocalizationService _loc;
        private AudioManager _audio;

        protected override void Awake()
        {
            base.Awake();
            _store = Services.Get<StoreService>();
            _loc = Services.Get<LocalizationService>();
            _audio = Services.Get<AudioManager>();
            _close.onClick.AddListener(Close);
            _buy.onClick.AddListener(Buy);
            _restore.onClick.AddListener(Restore);
        }

        protected override void OnShow(object args)
        {
            _price.text = StoreCatalog.NoAds.Price;
            _demo.gameObject.SetActive(_store.IsDemo);
            _status.text = string.Empty;
            _buy.interactable = !_store.NoAds;
        }

        private void Buy() => _store.Buy(StoreCatalog.NoAds, bought =>
        {
            if (!bought) return;
            _audio.PlayOneShotSound("coin");
            Close();
        });

        private void Restore() => _store.Restore(restored =>
        {
            _status.text = _loc.Get(restored ? "store.restored" : "store.nothing_restored");
            if (restored) Close();
        });
    }
}
