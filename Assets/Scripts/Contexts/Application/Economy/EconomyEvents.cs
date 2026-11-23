using Project.Core.Events;

namespace Project.Contexts.Application.Economy
{
    public readonly struct CoinsChanged : IEvent
    {
        public readonly int Coins;
        public readonly int Delta;

        public CoinsChanged(int coins, int delta)
        {
            Coins = coins;
            Delta = delta;
        }
    }

    public readonly struct BoosterStockChanged : IEvent
    {
        public readonly BoosterType Type;
        public readonly int Stock;

        public BoosterStockChanged(BoosterType type, int stock)
        {
            Type = type;
            Stock = stock;
        }
    }

    public enum LivesChangeReason
    {
        Other,
        Spent,
        Refilled,
        // Handed over at once (rewarded ad).
        Granted,
        UnlimitedStarted,
        UnlimitedEnded
    }

    public readonly struct LivesChanged : IEvent
    {
        public readonly int Count;
        public readonly bool Unlimited;
        public readonly LivesChangeReason Reason;

        public LivesChanged(int count, bool unlimited, LivesChangeReason reason = LivesChangeReason.Other)
        {
            Count = count;
            Unlimited = unlimited;
            Reason = reason;
        }
    }

    // A campaign try ended without a win (loss, restart or quit).
    public readonly struct LevelFailed : IEvent { }

    // Raised after its reward is saved.
    public readonly struct LevelCompleted : IEvent
    {
        public readonly LevelResult Result;
        public LevelCompleted(LevelResult result) => Result = result;
    }

    // Raised after a purchase is delivered and saved.
    public readonly struct StorePurchased : IEvent
    {
        public readonly StoreProduct Product;
        public StorePurchased(StoreProduct product) => Product = product;
    }
}
