namespace Project.Contexts.Application.Economy
{
    public enum StoreSection
    {
        Offers,
        Coins
    }

    // A store item: its test price label and what one purchase grants.
    public readonly struct StoreProduct
    {
        public readonly string Id;
        public readonly string TitleKey;
        public readonly string Price;
        public readonly int Coins;
        public readonly int Undo;
        public readonly int FreeMove;
        public readonly int ExtraTube;
        public readonly bool NoAds;

        public StoreProduct(string id, string titleKey, string price, int coins = 0, int undo = 0, int freeMove = 0,
            int extraTube = 0, bool noAds = false)
        {
            Id = id;
            TitleKey = titleKey;
            Price = price;
            Coins = coins;
            Undo = undo;
            FreeMove = freeMove;
            ExtraTube = extraTube;
            NoAds = noAds;
        }

        public int Boosters(BoosterType type) => type switch
        {
            BoosterType.Undo => Undo,
            BoosterType.ExtraTube => ExtraTube,
            _ => FreeMove
        };
    }

    // Store table with US test prices; live prices would come from the store.
    public static class StoreCatalog
    {
        // Hours the beginner offer stays after it is first shown.
        public const int BeginnerHours = 48;

        public static readonly StoreProduct Beginner =
            new("beginner", "store.beginner", "$1.99", coins: 600, undo: 1, freeMove: 1, extraTube: 1);

        public static readonly StoreProduct[] Bundles =
        {
            new("bundle_small", "store.bundle_small", "$2.99", coins: 900, undo: 1, freeMove: 1),
            new("bundle_medium", "store.bundle_medium", "$5.99", coins: 2000, undo: 2, freeMove: 2, extraTube: 1),
            new("bundle_large", "store.bundle_large", "$9.99", coins: 4000, undo: 3, freeMove: 3, extraTube: 2)
        };

        public static readonly StoreProduct[] CoinPacks =
        {
            new("coins_1", "store.coins", "$0.99", coins: 450),
            new("coins_2", "store.coins", "$2.99", coins: 1450),
            new("coins_3", "store.coins", "$5.99", coins: 3100),
            new("coins_4", "store.coins", "$9.99", coins: 5500)
        };

        public static readonly StoreProduct NoAds = new("no_ads", "store.no_ads", "$4.99", noAds: true);
    }
}
