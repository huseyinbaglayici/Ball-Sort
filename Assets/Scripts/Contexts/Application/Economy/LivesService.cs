using System;
using Project.Core.Events;

namespace Project.Contexts.Application.Economy
{
    // Refills are computed from saved device-UTC timestamps on read, so time passes while the app is closed.
    public sealed class LivesService
    {
        private static readonly long RefillTicks = TimeSpan.FromMinutes(CampaignRules.LifeRefillMinutes).Ticks;

        private readonly PlayerData _data;
        private readonly Func<DateTime> _utcNow;
        private bool _wasUnlimited;

        public LivesService(PlayerData data, Func<DateTime> utcNow = null)
        {
            _data = data;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _wasUnlimited = IsUnlimited;
            Refresh();
        }

        private LivesModel Lives => _data.Model.Lives;
        private long Now => _utcNow().Ticks;

        public int Count
        {
            get
            {
                Refresh();
                return Lives.Count;
            }
        }

        public bool IsUnlimited => Lives.UnlimitedUntilTicks > Now;

        public bool CanPlay => IsUnlimited || Count > 0;

        public TimeSpan NextLifeIn
        {
            get
            {
                Refresh();
                return Lives.NextRefillTicks > 0 ? TimeSpan.FromTicks(Math.Max(0, Lives.NextRefillTicks - Now)) : TimeSpan.Zero;
            }
        }

        public TimeSpan UnlimitedLeft => TimeSpan.FromTicks(Math.Max(0, Lives.UnlimitedUntilTicks - Now));

        // Unlimited lives are never spent.
        public bool TrySpend()
        {
            Refresh();
            if (IsUnlimited) return true;
            if (Lives.Count == 0) return false;

            Lives.Count--;
            if (Lives.NextRefillTicks == 0) Lives.NextRefillTicks = Now + RefillTicks;
            Changed(LivesChangeReason.Spent);
            return true;
        }

        // Capped at the maximum; refill timers of the missing lives keep running.
        public void Add(int count)
        {
            if (count <= 0) return;
            Refresh();
            Lives.Count = Math.Min(CampaignRules.MaxLives, Lives.Count + count);
            if (Lives.Count >= CampaignRules.MaxLives) Lives.NextRefillTicks = 0;
            Changed(LivesChangeReason.Granted);
        }

        // A second grant extends the remaining time.
        public void GrantUnlimited(int minutes)
        {
            if (minutes <= 0) return;
            Refresh();
            Lives.UnlimitedUntilTicks = Math.Max(Now, Lives.UnlimitedUntilTicks) + TimeSpan.FromMinutes(minutes).Ticks;
            Changed(LivesChangeReason.UnlimitedStarted);
        }

        // Raises LivesChanged only if a life arrived.
        public void Refresh()
        {
            var unlimited = IsUnlimited;
            var changed = unlimited != _wasUnlimited;
            var reason = _wasUnlimited && !unlimited ? LivesChangeReason.UnlimitedEnded : LivesChangeReason.Other;

            if (Lives.Count >= CampaignRules.MaxLives)
            {
                if (Lives.NextRefillTicks != 0)
                {
                    Lives.NextRefillTicks = 0;
                    changed = true;
                }
            }
            else
            {
                var now = Now;
                var next = Lives.NextRefillTicks > 0 ? Lives.NextRefillTicks : now + RefillTicks;
                var earned = now < next ? 0 : 1 + (int)Math.Min(CampaignRules.MaxLives, (now - next) / RefillTicks);
                var filled = Math.Min(CampaignRules.MaxLives, Lives.Count + earned);
                var nextAfter = filled == CampaignRules.MaxLives ? 0 : next + earned * RefillTicks;
                if (filled != Lives.Count || nextAfter != Lives.NextRefillTicks) changed = true;
                if (filled > Lives.Count && reason == LivesChangeReason.Other) reason = LivesChangeReason.Refilled;
                Lives.Count = filled;
                Lives.NextRefillTicks = nextAfter;
            }

            if (changed) Changed(reason);
        }

        private void Changed(LivesChangeReason reason)
        {
            _wasUnlimited = IsUnlimited;
            _data.Save();
            EventBus<LivesChanged>.Raise(new LivesChanged(Lives.Count, _wasUnlimited, reason));
        }
    }
}
