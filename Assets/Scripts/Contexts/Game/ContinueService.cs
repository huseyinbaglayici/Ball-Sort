using System;
using System.Text;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Sort;
using Project.Core.Localization;

namespace Project.Contexts.Game
{
    public enum ContinueResult
    {
        Applied,
        // The board changed since the offer (or there is no offer); nothing was charged.
        Stale,
        // This try already used its continue; nothing was charged.
        AlreadyUsed,
        NotEnoughCoins
    }

    // Rechecked right before paying, and coins, right and receipt go in one save, so nothing can pay twice.
    public sealed class ContinueService
    {
        private readonly PlayerData _data;
        private readonly WalletService _wallet;
        private readonly Func<DateTime> _utcNow;

        public ContinueService(PlayerData data, WalletService wallet, Func<DateTime> utcNow = null)
        {
            _data = data;
            _wallet = wallet;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public static int Price => CampaignRules.ContinuePrice;

        public bool CanAfford => _wallet.CanAfford(Price);

        // Survives an app restart.
        public bool UsedInTry(int tryId) => _data.Model.LastContinue.TryId == tryId && _data.Model.LastContinue.Coins > 0;

        // The exact result shown before paying, e.g. "900 gold · 4-slot tube + red bomb +3".
        public static string Describe(ContinueOffer offer, SortBoard board, LocalizationService loc)
        {
            var bombs = new StringBuilder();
            if (offer.FuseBonus != null)
                for (var bomb = 0; bomb < offer.FuseBonus.Length; bomb++)
                {
                    if (offer.FuseBonus[bomb] <= 0) continue;
                    var color = BombColor(board, bomb);
                    var name = color >= 0 ? loc.Get($"color.{color}") : string.Empty;
                    bombs.Append(loc.Format("continue.bomb", name, offer.FuseBonus[bomb]));
                }
            return loc.Format("continue.offer", Price, bombs);
        }

        private static int BombColor(SortBoard board, int bomb)
        {
            for (var b = 0; b < board.BottleCount; b++)
                for (var slot = 0; slot < board.Count(b); slot++)
                    if (board.BombAt(b, slot) == bomb) return board.ColorAt(b, slot);
            return -1;
        }

        // Does nothing unless paid; tube is the added temporary tube's index when Applied.
        public ContinueResult TryBuy(SortBoard board, ContinueOffer offer, int level, int tryId, out int tube)
        {
            tube = -1;
            if (board == null || offer == null) return ContinueResult.Stale;
            // A used continue is reported first: applying one moves the board on, so its offer is also stale by then.
            if (board.ContinueUsed || UsedInTry(tryId)) return ContinueResult.AlreadyUsed;
            if (offer.BoardVersion != board.Version) return ContinueResult.Stale;
            if (!CanAfford) return ContinueResult.NotEnoughCoins;

            var boardVersion = board.Version;
            tube = board.ApplyContinue(offer); // changes nothing when it returns -1
            if (tube < 0) return ContinueResult.Stale;

            using (_data.Batch())
            {
                _wallet.TrySpend(Price);
                _data.Model.LastContinue = new ContinueReceipt
                {
                    TryId = tryId,
                    Level = level,
                    Coins = Price,
                    BoardVersion = boardVersion,
                    UtcTicks = _utcNow().Ticks
                };
                _data.Save();
            }
            return ContinueResult.Applied;
        }
    }
}
