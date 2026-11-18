using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Contexts.Game.Sort
{
    public enum RescueKind
    {
        // No legal transfer is left; the caller decides when to ask.
        NoMove,
        // A fuse ran out on the last transfer.
        Bomb
    }

    // Only valid while the board is at BoardVersion; Route is a booster-free win replayed from the rescued board.
    public sealed class ContinueOffer
    {
        public int BoardVersion;
        public bool Rewind;
        public int TubeCapacity = RescueAdvisor.TubeCapacity;
        public int[] FuseBonus;
        public List<(int From, int To)> Route = new();
    }

    public sealed class RescueReport
    {
        public RescueKind Kind;
        // Bomb only: the board one transfer back is winnable as is, so a plain Undo may be offered.
        public bool UndoWins;
        // Null when not provable in time, no win found, or Continue already used this try.
        public ContinueOffer Offer;
        public bool TimedOut;
    }

    // Only offers what a search proved winnable and a replay under the real rules confirmed.
    public static class RescueAdvisor
    {
        public const int TubeCapacity = Application.Economy.CampaignRules.ContinueTubeCapacity;
        public const int DefaultTimeoutMs = 1500;
        public const int NodeLimit = 200000;
        // Fuse given to the exploded bombs while searching, so the search itself never trips them.
        private const int OpenFuse = 100000;

        // Copies the board on the caller, searches on a worker; recheck BoardVersion before paying.
        public static Task<RescueReport> AnalyzeAsync(SortBoard board, int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancel = default)
        {
            var copy = board.Clone();
            return Task.Run(() => Analyze(copy, timeoutMs, cancel), cancel);
        }

        public static RescueReport Analyze(SortBoard board, int timeoutMs = DefaultTimeoutMs, CancellationToken cancel = default)
        {
            var clock = Stopwatch.StartNew();
            var report = new RescueReport { Kind = board.IsFailed ? RescueKind.Bomb : RescueKind.NoMove };
            if (report.Kind == RescueKind.Bomb) AnalyzeBomb(board, report, clock, timeoutMs, cancel);
            else if (!board.ContinueUsed) AnalyzeNoMove(board, report, clock, timeoutMs, cancel);
            return report;
        }

        private static void AnalyzeNoMove(SortBoard board, RescueReport report, Stopwatch clock, int timeoutMs,
            CancellationToken cancel)
        {
            var search = board.Clone();
            search.AddBottle(TubeCapacity);
            var route = new List<(int, int)>();
            if (!Solve(search, route, report, clock, timeoutMs, cancel)) return;

            var offer = new ContinueOffer
            {
                BoardVersion = board.Version, Rewind = false, FuseBonus = new int[board.BombCount], Route = route
            };
            if (Replays(board, offer)) report.Offer = offer;
        }

        private static void AnalyzeBomb(SortBoard board, RescueReport report, Stopwatch clock, int timeoutMs,
            CancellationToken cancel)
        {
            var exploded = new List<int>();
            for (var i = 0; i < board.BombCount; i++)
                if (board.IsBombLit(i) && !board.IsBombDefused(i) && board.BombFuse(i) <= 0) exploded.Add(i);

            var rewound = board.Clone();
            if (!rewound.TryUndo(out _)) return;

            report.UndoWins = Solve(rewound.Clone(), new List<(int, int)>(), report, clock, timeoutMs, cancel);
            if (board.ContinueUsed) return;

            var search = rewound.Clone();
            search.AddBottle(TubeCapacity);
            foreach (var bomb in exploded) search.SetFuse(bomb, OpenFuse);
            var probe = search.Clone();
            var route = new List<(int, int)>();
            if (!Solve(search, route, report, clock, timeoutMs, cancel)) return;

            // Each exploded bomb gets what it burns on the route plus one spare, and at least one more than it had.
            var burned = new int[board.BombCount];
            foreach (var (from, to) in route)
            {
                probe.TryMove(from, to, out _);
                foreach (var bomb in exploded)
                    if (probe.IsBombDefused(bomb) && burned[bomb] == 0) burned[bomb] = OpenFuse - probe.BombFuse(bomb);
            }

            var offer = new ContinueOffer
            {
                BoardVersion = board.Version, Rewind = true, FuseBonus = new int[board.BombCount], Route = route
            };
            foreach (var bomb in exploded)
            {
                var had = rewound.BombFuse(bomb);
                var fuse = System.Math.Max(burned[bomb] + 1, had + 1);
                offer.FuseBonus[bomb] = fuse - had;
            }
            if (Replays(board, offer)) report.Offer = offer;
        }

        private static bool Solve(SortBoard board, List<(int, int)> route, RescueReport report, Stopwatch clock, int timeoutMs,
            CancellationToken cancel)
        {
            var left = timeoutMs - (int)clock.ElapsedMilliseconds;
            if (left <= 0)
            {
                report.TimedOut = true;
                return false;
            }
            var outcome = BoardSolver.Solve(board, route, NodeLimit, left, cancel: cancel);
            if (outcome == BoardSolver.Outcome.LimitReached) report.TimedOut = true;
            return outcome == BoardSolver.Outcome.Solved;
        }

        // The route must win under the real rules without exploding.
        private static bool Replays(SortBoard board, ContinueOffer offer)
        {
            var check = board.Clone();
            if (check.ApplyContinue(offer) < 0) return false;
            foreach (var (from, to) in offer.Route)
                if (!check.TryMove(from, to, out _) || check.IsFailed) return false;
            return check.IsSolved();
        }
    }
}
