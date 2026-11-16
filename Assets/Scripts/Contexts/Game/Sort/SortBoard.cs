using System.Collections.Generic;
using Project.Contexts.Game.Levels;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// One completed move. <see cref="RevealedSlot"/> is the slot in <see cref="From"/> whose hidden color was uncovered, or -1.
    /// <see cref="Completed"/> is true when this move finished the target bottle (full, one color) and locked it.
    /// </summary>
    public readonly struct BallMove
    {
        public readonly int From;
        public readonly int To;
        public readonly int Count;
        public readonly int RevealedSlot;
        public readonly bool Completed;

        public BallMove(int from, int to, int count, int revealedSlot, bool completed)
        {
            From = from;
            To = to;
            Count = count;
            RevealedSlot = revealedSlot;
            Completed = completed;
        }
    }

    /// <summary>
    /// The puzzle rules on plain data (no Unity objects), so they can be tested without a scene.
    /// Slots run bottom to top. A Special ("question mark") unit hides its color until it becomes
    /// the top unit of its bottle; a hidden unit can not be moved.
    /// Move rule: the target must be empty or have the same top color and a free slot, and the amount is
    /// min(same-color run on top of the origin, free slots in the target).
    /// Completion rule: right after a move into a bottle, that bottle is
    /// completed when it is full of one fully visible color. A completed bottle is locked: it can be neither
    /// the origin nor the target of a move. Only the target of the last move is checked.
    /// Deadlock rule: see <see cref="HasNoMove"/>.
    /// </summary>
    public sealed class SortBoard
    {
        private readonly int[][] _colors;
        private readonly bool[][] _special;
        private readonly bool[][] _known;
        private readonly int[] _count;
        private readonly bool[] _completed;
        private readonly Stack<BallMove> _history = new();

        public SortBoard(LevelData level)
        {
            Capacity = level.Capacity;
            var bottles = level.Bottles;
            _colors = new int[bottles.Count][];
            _special = new bool[bottles.Count][];
            _known = new bool[bottles.Count][];
            _count = new int[bottles.Count];
            _completed = new bool[bottles.Count];

            for (var b = 0; b < bottles.Count; b++)
            {
                _colors[b] = new int[Capacity];
                _special[b] = new bool[Capacity];
                _known[b] = new bool[Capacity];
                for (var s = 0; s < Capacity; s++) _colors[b][s] = SlotData.EmptyColor;

                var slots = bottles[b].Slots;
                for (var s = 0; s < slots.Count && s < Capacity; s++)
                {
                    if (slots[s].IsEmpty) break;
                    _colors[b][s] = slots[s].Color;
                    _special[b][s] = slots[s].Kind == SlotKind.Special;
                    _known[b][s] = !_special[b][s];
                    _count[b]++;
                }
                RevealTop(b);
            }
        }

        public int BottleCount => _colors.Length;
        public int Capacity { get; }
        public int MoveCount => _history.Count;

        public int Count(int bottle) => _count[bottle];
        public bool IsEmpty(int bottle) => _count[bottle] == 0;
        public bool IsFull(int bottle) => _count[bottle] == Capacity;
        public bool IsCompleted(int bottle) => _completed[bottle];

        /// <summary>A bottle can be picked up when it holds ball and is not completed.</summary>
        public bool CanSelectAsOrigin(int bottle) => _count[bottle] > 0 && !_completed[bottle];
        public int ColorAt(int bottle, int slot) => _colors[bottle][slot];
        public bool IsKnown(int bottle, int slot) => _known[bottle][slot];
        public bool IsSpecial(int bottle, int slot) => _special[bottle][slot];

        public int TopColor(int bottle) =>
            _count[bottle] == 0 ? SlotData.EmptyColor : _colors[bottle][_count[bottle] - 1];

        /// <summary>Number of same-color units on top of the bottle that may be moved (stops at a hidden unit).</summary>
        public int TopRun(int bottle)
        {
            var n = _count[bottle];
            if (n == 0) return 0;

            var color = _colors[bottle][n - 1];
            var run = 0;
            for (var s = n - 1; s >= 0; s--)
            {
                if (_colors[bottle][s] != color || !_known[bottle][s]) break;
                run++;
            }
            return run;
        }

        public bool CanMove(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= BottleCount || to >= BottleCount) return false;
            if (!CanSelectAsOrigin(from) || _completed[to] || IsFull(to)) return false;
            return IsEmpty(to) || TopColor(to) == TopColor(from);
        }

        public bool TryMove(int from, int to, out BallMove move)
        {
            if (!CanMove(from, to))
            {
                move = default;
                return false;
            }

            var amount = System.Math.Min(TopRun(from), Capacity - _count[to]);
            for (var i = 0; i < amount; i++) Transfer(from, to);

            var revealed = RevealTop(from);
            var completed = ShouldComplete(to);
            _completed[to] = completed;
            move = new BallMove(from, to, amount, revealed, completed);
            _history.Push(move);
            return true;
        }

        /// <summary>Takes back the last move. A color that was uncovered stays known.</summary>
        public bool TryUndo(out BallMove move)
        {
            if (_history.Count == 0)
            {
                move = default;
                return false;
            }

            move = _history.Pop();
            if (move.Completed) _completed[move.To] = false;
            for (var i = 0; i < move.Count; i++) Transfer(move.To, move.From);
            return true;
        }

        /// <summary>Full of one color with every unit visible. The whole bottle is then one same-color run.</summary>
        private bool ShouldComplete(int bottle) =>
            !_completed[bottle] && _count[bottle] == Capacity && TopRun(bottle) == Capacity;

        /// <summary>
        /// Deadlock check. The board is stuck unless:
        /// <list type="number">
        /// <item>some origin's whole top run fits in the free slots of the bottles it may move to, counted together.
        /// An empty target does not count when the origin holds nothing below its top run, because that move only
        /// relocates a single-color bottle; or</item>
        /// <item>some origin may move to a bottle that is empty, or holds one fully visible color and is not full.</item>
        /// </list>
        /// So a board with only legal moves that cannot take a whole run anywhere useful is stuck.
        /// </summary>
        public bool HasNoMove()
        {
            for (var from = 0; from < BottleCount; from++)
            {
                if (!CanSelectAsOrigin(from)) continue;
                var run = TopRun(from);
                var below = _count[from] - run;
                var targets = 0;
                var free = 0;
                for (var to = 0; to < BottleCount; to++)
                {
                    if (!CanMove(from, to)) continue;
                    if (IsEmpty(to) && below == 0) continue;
                    targets++;
                    free += Capacity - _count[to];
                }
                if (targets > 0 && run <= free) return false;
            }

            for (var to = 0; to < BottleCount; to++)
            {
                if (_completed[to] || !(IsEmpty(to) || IsSingleVisibleColorNotFull(to))) continue;
                for (var from = 0; from < BottleCount; from++)
                    if (CanMove(from, to)) return false;
            }
            return true;
        }

        private bool IsSingleVisibleColorNotFull(int bottle) =>
            _count[bottle] > 0 && _count[bottle] < Capacity && TopRun(bottle) == _count[bottle];

        /// <summary>Solved when every bottle is empty or full of one known color.</summary>
        public bool IsSolved()
        {
            for (var b = 0; b < BottleCount; b++)
            {
                if (_count[b] == 0) continue;
                if (_count[b] != Capacity) return false;
                for (var s = 0; s < Capacity; s++)
                    if (_colors[b][s] != _colors[b][0] || !_known[b][s]) return false;
            }
            return true;
        }

        private void Transfer(int from, int to)
        {
            var fromSlot = --_count[from];
            var toSlot = _count[to]++;
            _colors[to][toSlot] = _colors[from][fromSlot];
            _special[to][toSlot] = _special[from][fromSlot];
            _known[to][toSlot] = true;

            _colors[from][fromSlot] = SlotData.EmptyColor;
            _special[from][fromSlot] = false;
            _known[from][fromSlot] = false;
        }

        /// <returns>The slot that was just uncovered, or -1.</returns>
        private int RevealTop(int bottle)
        {
            var n = _count[bottle];
            if (n == 0 || _known[bottle][n - 1]) return -1;
            _known[bottle][n - 1] = true;
            return n - 1;
        }
    }
}
