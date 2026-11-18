using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Project.Contexts.Game.Sort
{
    // Plays on the given board and undoes all; sees hidden colors, so never show its route to the player as a hint.
    public static class BoardSolver
    {
        public enum Outcome
        {
            Solved,
            Unsolvable,
            LimitReached
        }

        private readonly struct Candidate
        {
            public readonly int From;
            public readonly int To;
            public readonly int Score;

            public Candidate(int from, int to, int score)
            {
                From = from;
                To = to;
                Score = score;
            }
        }

        // Cancelling counts as hitting the limit.
        public static Outcome Solve(SortBoard board, List<(int From, int To)> route, int nodeLimit = 200000,
            int timeoutMs = 0, int maxDepth = 400, CancellationToken cancel = default)
        {
            route.Clear();
            var search = new Search(board, route, nodeLimit, timeoutMs, maxDepth, cancel);
            if (search.Run()) return Outcome.Solved;
            return search.LimitHit ? Outcome.LimitReached : Outcome.Unsolvable;
        }

        private sealed class Search
        {
            private readonly SortBoard _board;
            private readonly List<(int From, int To)> _route;
            private readonly HashSet<string> _seen = new();
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private readonly long _timeoutMs;
            private readonly int _maxDepth;
            private readonly CancellationToken _cancel;
            private int _budget;

            public bool LimitHit { get; private set; }

            public Search(SortBoard board, List<(int, int)> route, int nodeLimit, int timeoutMs, int maxDepth,
                CancellationToken cancel)
            {
                _cancel = cancel;
                _board = board;
                _route = route;
                _budget = nodeLimit;
                _timeoutMs = timeoutMs;
                _maxDepth = maxDepth;
            }

            public bool Run()
            {
                if (_board.IsSolved()) return true;
                if (_board.IsFailed || _route.Count >= _maxDepth) return false;
                if (_budget-- <= 0 || _cancel.IsCancellationRequested ||
                    (_timeoutMs > 0 && _clock.ElapsedMilliseconds > _timeoutMs))
                {
                    LimitHit = true;
                    return false;
                }
                if (!_seen.Add(_board.StateKey())) return false;

                foreach (var move in Candidates())
                {
                    if (!_board.TryMove(move.From, move.To, out _)) continue;
                    if (!_board.IsFailed)
                    {
                        _route.Add((move.From, move.To));
                        if (Run()) return true;
                        _route.RemoveAt(_route.Count - 1);
                    }
                    _board.TryUndo(out _);
                    if (LimitHit) return false;
                }
                return false;
            }

            // Most promising moves first.
            private List<Candidate> Candidates()
            {
                var list = new List<Candidate>();
                for (var from = 0; from < _board.BottleCount; from++)
                {
                    if (!_board.CanSelectAsOrigin(from)) continue;
                    var run = _board.TopRun(from);
                    var count = _board.Count(from);
                    for (var to = 0; to < _board.BottleCount; to++)
                    {
                        if (!_board.CanMove(from, to)) continue;
                        var amount = _board.MoveAmount(from, to);
                        var toEmpty = _board.IsEmpty(to);
                        // Pouring a whole one-color bottle into an empty one no bigger than it changes nothing.
                        if (toEmpty && run == count && _board.Capacity(to) <= _board.Capacity(from)) continue;

                        var score = amount * 3;
                        if (!toEmpty) score += 35;
                        if (amount == count) score += 25;
                        if (_board.Count(to) + amount == _board.Capacity(to) && !_board.IsTemporary(to) &&
                            _board.TopRun(to) == _board.Count(to))
                        {
                            score += 35;
                            if (CurtainWaitsFor(_board.TopColor(from))) score += 90;
                        }
                        if (amount < count && !_board.IsKnown(from, count - amount - 1)) score += 15;
                        if (_board.IsTemporary(to)) score -= 20;
                        list.Add(new Candidate(from, to, score));
                    }
                }
                list.Sort((a, b) => b.Score.CompareTo(a.Score));
                return list;
            }

            private bool CurtainWaitsFor(int color)
            {
                for (var i = 0; i < _board.CurtainCount; i++)
                    if (_board.IsCurtainActive(i) && _board.Curtain(i).Color == color) return true;
                return false;
            }
        }
    }
}
