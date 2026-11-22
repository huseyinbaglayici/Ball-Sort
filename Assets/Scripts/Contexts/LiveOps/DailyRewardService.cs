using System;
using Project.Contexts.Application.Economy;
using Project.Core.Events;

namespace Project.Contexts.LiveOps
{
    public readonly struct DailyRewardChanged : IEvent { }

    // Seven-day run on the local calendar; a skipped day restarts it at day 1.
    public sealed class DailyRewardService
    {
        public static readonly int[] Rewards = { 50, 75, 100, 150, 200, 300, 500 };

        private readonly PlayerData _data;
        private readonly WalletService _wallet;
        private readonly Func<DateTime> _localNow;

        public DailyRewardService(PlayerData data, WalletService wallet, Func<DateTime> localNow = null)
        {
            _data = data;
            _wallet = wallet;
            _localNow = localNow ?? (() => DateTime.Now);
        }

        private DailyRewardModel Model => _data.Model.Daily;

        public static int DayNumber(DateTime local) => (int)(local.Date.Ticks / TimeSpan.TicksPerDay);

        public int Today => DayNumber(_localNow());

        // A clock set back never pays twice.
        public bool CanClaim => Today > Model.LastClaimDay;

        // The day to claim, or the one claimed today.
        public int CurrentIndex
        {
            get
            {
                if (!CanClaim) return Math.Clamp(Model.Streak - 1, 0, Rewards.Length - 1);
                return Model.LastClaimDay == Today - 1 ? Model.Streak % Rewards.Length : 0;
            }
        }

        public bool IsClaimed(int index) => CanClaim ? index < CurrentIndex : index <= CurrentIndex;

        public bool ShouldAutoShow => CanClaim && Model.LastAutoShownDay != Today;

        public void MarkAutoShown()
        {
            Model.LastAutoShownDay = Today;
            _data.Save();
        }

        // Returns the coins paid; 0 when nothing was claimable.
        public int Claim()
        {
            if (!CanClaim) return 0;
            var index = CurrentIndex;
            var amount = Rewards[index];
            using (_data.Batch())
            {
                Model.Streak = index + 1;
                Model.LastClaimDay = Today;
                _wallet.Add(amount);
                _data.Save();
            }
            EventBus<DailyRewardChanged>.Raise(default);
            return amount;
        }
    }
}
