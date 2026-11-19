using System;
using Project.Core.Events;

namespace Project.Contexts.Application.Economy
{
    // Never negative; every change is saved and raises CoinsChanged.
    public sealed class WalletService
    {
        private readonly PlayerData _data;

        public WalletService(PlayerData data) => _data = data;

        public int Coins => _data.Model.Coins;

        public bool CanAfford(int amount) => amount >= 0 && Coins >= amount;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Change(amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || !CanAfford(amount)) return false;
            if (amount > 0) Change(-amount);
            return true;
        }

        private void Change(int delta)
        {
            _data.Model.Coins = Math.Max(0, _data.Model.Coins + delta);
            _data.Save();
            EventBus<CoinsChanged>.Raise(new CoinsChanged(_data.Model.Coins, delta));
        }
    }
}
