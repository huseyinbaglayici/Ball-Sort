using System;
using System.Collections.Generic;
using Project.Contexts.Application.Economy;

namespace Project.Contexts.LiveOps
{
    // The offline race's rivals: each bot's finish time and score, stable per window, stage, room and bot.
    public static class OrbitRushBots
    {
        private static readonly string[] BotNames =
            { "Nova", "Kai", "Mira", "Orion", "Lyra", "Zed", "Vega", "Juno", "Rex", "Sky", "Pip", "Luma" };

        public static int Finished(OrbitRushModel model, double elapsed)
        {
            var count = 0;
            for (var i = 1; i < OrbitRushService.Capacity; i++)
                if (FinishTime(model, i) <= elapsed) count++;
            return count;
        }

        // When the bot that took the given place crossed the line; used to rank a winner behind them.
        public static double FinishBefore(OrbitRushModel model, int rank)
        {
            var times = new List<double>();
            for (var i = 1; i < OrbitRushService.Capacity; i++) times.Add(FinishTime(model, i));
            times.Sort();
            return rank - 1 < times.Count ? times[rank - 1] - 0.001 : double.MaxValue;
        }

        // Each bot crosses the line between a third of the stage time and past its end.
        public static double FinishTime(OrbitRushModel model, int bot) =>
            (0.35 + 0.95 * Random01(model, bot, 0)) * OrbitRushService.StageHours * 3600;

        public static OrbitRushRacer Racer(OrbitRushModel model, int bot, double elapsed)
        {
            var finish = FinishTime(model, bot);
            var target = OrbitRushService.Targets[Math.Min(model.Stage, OrbitRushService.Stages - 1)];
            var score = elapsed >= finish ? target : (int)Math.Floor(target * Math.Pow(elapsed / finish, 0.85));
            // Steps of three over twelve names keep the four bots apart.
            var name = BotNames[((int)(Random01(model, 0, 1) * BotNames.Length) + bot * 3) % BotNames.Length];
            return new OrbitRushRacer(name, Math.Min(score, target), false, elapsed >= finish ? finish : double.MaxValue);
        }

        // Stable per window, stage, room and bot.
        private static double Random01(OrbitRushModel model, int bot, int salt)
        {
            unchecked
            {
                var x = (uint)(model.Window * 73856093) ^ (uint)(model.Stage * 19349663) ^ (uint)(model.Attempt * 83492791) ^
                        (uint)(bot * 2654435761u) ^ (uint)(salt * 40503);
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                x *= 0x9E3779B1u;
                x ^= x >> 15;
                return (x & 0xFFFFFF) / (double)0x1000000;
            }
        }
    }
}
