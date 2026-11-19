using Project.Core.Events;

namespace Project.Contexts.Application.Economy
{
    public readonly struct BoosterCharge
    {
        public readonly BoosterType Type;
        public readonly bool FromStock;
        public readonly int Coins;

        public BoosterCharge(BoosterType type, bool fromStock, int coins)
        {
            Type = type;
            FromStock = fromStock;
            Coins = coins;
        }
    }

    // Takes stock first, then coins; Undo and restarts never refund a use.
    public sealed class BoosterInventory
    {
        private readonly PlayerData _data;
        private readonly WalletService _wallet;

        public BoosterInventory(PlayerData data, WalletService wallet)
        {
            _data = data;
            _wallet = wallet;
        }

        public int Stock(BoosterType type) => _data.Model.BoosterStock[(int)type];

        public static int Price(BoosterType type) => CampaignRules.Price(type);

        public static bool IsUnlocked(BoosterType type, int level) => level >= CampaignRules.UnlockLevel(type);

        public bool CanUse(BoosterType type, int level) =>
            IsUnlocked(type, level) && (Stock(type) > 0 || _wallet.CanAfford(Price(type)));

        // Call only after the board has accepted the booster's action.
        public bool TryUse(BoosterType type, int level, out BoosterCharge charge)
        {
            charge = default;
            if (!IsUnlocked(type, level)) return false;

            if (Stock(type) > 0)
            {
                SetStock(type, Stock(type) - 1);
                charge = new BoosterCharge(type, true, 0);
                return true;
            }

            var price = Price(type);
            if (!_wallet.TrySpend(price)) return false;
            charge = new BoosterCharge(type, false, price);
            return true;
        }

        public void Add(BoosterType type, int count)
        {
            if (count > 0) SetStock(type, Stock(type) + count);
        }

        // Opening stock of each booster unlocked at this level, granted once.
        public void GrantOpenings(int level)
        {
            using (_data.Batch())
            {
                foreach (var type in CampaignRules.AllBoosters)
                {
                    if (!IsUnlocked(type, level) || _data.Model.OpeningGranted[(int)type]) continue;
                    _data.Model.OpeningGranted[(int)type] = true;
                    Add(type, CampaignRules.OpeningBoosterGrant);
                }
            }
        }

        private void SetStock(BoosterType type, int stock)
        {
            _data.Model.BoosterStock[(int)type] = stock < 0 ? 0 : stock;
            _data.Save();
            EventBus<BoosterStockChanged>.Raise(new BoosterStockChanged(type, Stock(type)));
        }
    }
}
