using System;
using Project.Core.Localization;

namespace Project.Contexts.LiveOps
{
    public static class LiveOpsText
    {
        // Two largest units: "2d 14h", "5h 12m", "4m 09s".
        public static string Countdown(TimeSpan left, LocalizationService loc)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            if (left.TotalDays >= 1) return loc.Format("liveops.time_dh", (int)left.TotalDays, left.Hours);
            if (left.TotalHours >= 1) return loc.Format("liveops.time_hm", left.Hours, left.Minutes);
            return loc.Format("liveops.time_ms", left.Minutes, left.Seconds.ToString("00"));
        }
    }
}
