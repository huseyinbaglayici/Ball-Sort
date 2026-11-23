using System;
using System.Collections.Generic;
using Project.Core.Events;
using UnityEngine;

namespace Project.Contexts.Application.Economy
{
    // Demo store with no payment SDK: a purchase is approved at once and each transaction delivers once.
    public sealed class StoreService
    {
        private readonly PlayerData _data;
        private readonly WalletService _wallet;
        private readonly BoosterInventory _boosters;
        private readonly Func<bool> _beginnerUnlocked;
        private readonly Func<DateTime> _utcNow;
        private readonly HashSet<string> _delivered = new();

        public StoreService(PlayerData data, WalletService wallet, BoosterInventory boosters, Func<bool> beginnerUnlocked,
            Func<DateTime> utcNow = null)
        {
            _data = data;
            _wallet = wallet;
            _boosters = boosters;
            _beginnerUnlocked = beginnerUnlocked;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        // No live store in this build; screens say so.
        public bool IsDemo => true;

        public bool NoAds => _data.Model.NoAds;

        // Opens after the first interstitial actually shown; gone once owned.
        public bool NoAdsOffered => !NoAds && _data.Model.FirstInterstitialSeen;

        // After its unlock win, for a player who never bought, until 48 hours past its first showing.
        public bool BeginnerAvailable
        {
            get
            {
                if (_data.Model.HasPurchased || !_beginnerUnlocked()) return false;
                var shown = _data.Model.BeginnerShownTicks;
                return shown == 0 || _utcNow().Ticks - shown < TimeSpan.FromHours(StoreCatalog.BeginnerHours).Ticks;
            }
        }

        public TimeSpan BeginnerLeft
        {
            get
            {
                var shown = _data.Model.BeginnerShownTicks;
                if (shown == 0) return TimeSpan.FromHours(StoreCatalog.BeginnerHours);
                var end = shown + TimeSpan.FromHours(StoreCatalog.BeginnerHours).Ticks;
                return TimeSpan.FromTicks(Math.Max(0, end - _utcNow().Ticks));
            }
        }

        // Starts the offer's clock the first time a player sees it.
        public void MarkBeginnerShown()
        {
            if (_data.Model.BeginnerShownTicks != 0 || !BeginnerAvailable) return;
            _data.Model.BeginnerShownTicks = _utcNow().Ticks;
            _data.Save();
        }

        public void Buy(StoreProduct product, Action<bool> done)
        {
            if (product.NoAds && NoAds || product.Id == StoreCatalog.Beginner.Id && !BeginnerAvailable)
            {
                done?.Invoke(false);
                return;
            }
            var transaction = Guid.NewGuid().ToString("N");
            Debug.Log($"[Store] Purchase '{product.Id}' {product.Price} approved (demo) tx={transaction}.");
            done?.Invoke(Deliver(transaction, product));
        }

        // Demo restore: an owned No Ads stays owned.
        public void Restore(Action<bool> done)
        {
            Debug.Log($"[Store] Restore (demo): no_ads={NoAds}.");
            done?.Invoke(NoAds);
        }

        // A repeated store callback for the same transaction grants nothing.
        public bool Deliver(string transaction, StoreProduct product)
        {
            if (string.IsNullOrEmpty(transaction) || !_delivered.Add(transaction)) return false;
            using (_data.Batch())
            {
                _data.Model.HasPurchased = true;
                if (product.NoAds) _data.Model.NoAds = true;
                _wallet.Add(product.Coins);
                foreach (var type in CampaignRules.AllBoosters) _boosters.Add(type, product.Boosters(type));
                _data.Save();
            }
            EventBus<StorePurchased>.Raise(new StorePurchased(product));
            return true;
        }
    }
}
