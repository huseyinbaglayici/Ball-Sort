using System.Collections.Generic;
using Project.Contexts.Game.Levels;

namespace Project.Contexts.Game.Sort
{
    // What one successful transfer changed, for the view.
    public sealed class MoveResult
    {
        public int From;
        public int To;
        public int Count;
        // Free Move booster: the group went onto a different color.
        public bool Free;
        public bool Exploded;
        public readonly List<(int Bottle, int Slot)> Revealed = new();
        public readonly List<int> Completed = new();
        public readonly List<int> CurtainsLifted = new();
        public readonly List<int> LockboxesOpened = new();
        public readonly List<int> BombsActivated = new();
        public readonly List<int> BombsDefused = new();
    }

    // Puzzle rules on plain data; slots run bottom to top and a sealed bottle is closed for good.
    public sealed class SortBoard
    {
        public const int MinTargetCapacity = 4;

        private struct Ball
        {
            public int Color;
            public bool Hidden;
            public bool Special;
            public string Key;
            // Bomb index + 1, so a default (empty) slot carries no bomb.
            public int BombId;
            public int Bomb => BombId - 1;
        }

        private sealed class State
        {
            public Ball[][] Slots;
            public int[] Count;
            // Booster and rescue tubes: never a completion target, must be empty to win.
            public bool[] Temporary;
            public bool[] CurtainActive;
            public bool[] LockboxActive;
            public int[] BombFuse;
            public bool[] BombLit;
            public bool[] BombDefused;
            public bool Failed;
            public int Moves;

            public State Clone()
            {
                var copy = (State)MemberwiseClone();
                copy.Slots = new Ball[Slots.Length][];
                for (var b = 0; b < Slots.Length; b++) copy.Slots[b] = (Ball[])Slots[b].Clone();
                copy.Count = (int[])Count.Clone();
                copy.Temporary = (bool[])Temporary.Clone();
                copy.CurtainActive = (bool[])CurtainActive.Clone();
                copy.LockboxActive = (bool[])LockboxActive.Clone();
                copy.BombFuse = (int[])BombFuse.Clone();
                copy.BombLit = (bool[])BombLit.Clone();
                copy.BombDefused = (bool[])BombDefused.Clone();
                return copy;
            }
        }

        private readonly (int Bottle, int Color, int Need)[] _curtains;
        private readonly (string Id, int[] Bottles)[] _lockboxes;
        private readonly int[] _bombBudget;
        private readonly Stack<(State Before, MoveResult Move)> _history = new();
        private readonly Dictionary<int, int> _colorTotal;
        private readonly int _largestCapacity;
        private State _s;
        private int _version;

        public SortBoard(LevelData level)
        {
            var bottles = level.Bottles;
            _s = new State
            {
                Slots = new Ball[bottles.Count][], Count = new int[bottles.Count], Temporary = new bool[bottles.Count]
            };
            _colorTotal = new Dictionary<int, int>();
            var budgets = new List<int>();
            var bombLit = new List<bool>();
            for (var b = 0; b < bottles.Count; b++)
            {
                var slots = bottles[b].Slots;
                _s.Slots[b] = new Ball[slots.Count];
                for (var s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot.IsEmpty) break;
                    var special = slot.Kind == SlotKind.Special;
                    var bomb = -1;
                    if (slot.IsBomb)
                    {
                        bomb = budgets.Count;
                        budgets.Add(slot.Fuse);
                        bombLit.Add(slot.FuseLit);
                    }
                    _s.Slots[b][s] = new Ball
                    {
                        Color = slot.Color, Hidden = special, Special = special,
                        Key = slot.IsKey ? slot.Key : null, BombId = bomb + 1
                    };
                    _s.Count[b]++;
                    _colorTotal.TryGetValue(slot.Color, out var total);
                    _colorTotal[slot.Color] = total + 1;
                }
                _largestCapacity = System.Math.Max(_largestCapacity, slots.Count);
            }

            _curtains = new (int, int, int)[level.Curtains.Count];
            for (var i = 0; i < _curtains.Length; i++)
            {
                var c = level.Curtains[i];
                _curtains[i] = (c.Bottle, c.Color, c.Need);
            }
            _lockboxes = new (string, int[])[level.Lockboxes.Count];
            for (var i = 0; i < _lockboxes.Length; i++)
                _lockboxes[i] = (level.Lockboxes[i].Id, level.Lockboxes[i].Bottles.ToArray());

            _bombBudget = budgets.ToArray();
            _s.CurtainActive = Filled(_curtains.Length, true);
            _s.LockboxActive = Filled(_lockboxes.Length, true);
            _s.BombFuse = (int[])_bombBudget.Clone();
            _s.BombLit = bombLit.ToArray();
            _s.BombDefused = new bool[_bombBudget.Length];
            Settle(new MoveResult());
        }

        // Undo history of the copy holds only the last move.
        private SortBoard(SortBoard source)
        {
            _curtains = source._curtains;
            _lockboxes = source._lockboxes;
            _bombBudget = source._bombBudget;
            _colorTotal = source._colorTotal;
            _largestCapacity = source._largestCapacity;
            _s = source._s.Clone();
            _version = source._version;
            ContinueUsed = source.ContinueUsed;
            if (source._history.Count > 0)
            {
                var last = source._history.Peek();
                _history.Push((last.Before.Clone(), last.Move));
            }
        }

        public SortBoard Clone() => new(this);

        // Bumped by every move, Undo, added tube and Continue; offers made for another version are stale.
        public int Version => _version;

        // Continue is offered once per try.
        public bool ContinueUsed { get; private set; }

        public bool IsTemporary(int bottle) => _s.Temporary[bottle];

        public int BottleCount => _s.Slots.Length;
        public int MoveCount => _s.Moves;
        public bool CanUndo => _history.Count > 0;
        // A lit fuse reached 0. Undo can still take that move back.
        public bool IsFailed => _s.Failed;

        public int Capacity(int bottle) => _s.Slots[bottle].Length;
        public int Count(int bottle) => _s.Count[bottle];
        public bool IsEmpty(int bottle) => _s.Count[bottle] == 0;
        public bool IsFull(int bottle) => _s.Count[bottle] == Capacity(bottle);
        public int ColorAt(int bottle, int slot) => _s.Slots[bottle][slot].Color;
        public bool IsKnown(int bottle, int slot) => !_s.Slots[bottle][slot].Hidden;
        // Hidden in the level data; stays true after it is revealed.
        public bool IsSpecial(int bottle, int slot) => _s.Slots[bottle][slot].Special;
        // Null unless the ball is an unused key.
        public string KeyAt(int bottle, int slot) => _s.Slots[bottle][slot].Key;
        public int BombAt(int bottle, int slot) => _s.Slots[bottle][slot].Bomb;

        public int TopColor(int bottle) =>
            _s.Count[bottle] == 0 ? SlotData.EmptyColor : _s.Slots[bottle][_s.Count[bottle] - 1].Color;

        public int CurtainCount => _curtains.Length;
        public (int Bottle, int Color, int Need) Curtain(int index) => _curtains[index];
        public bool IsCurtainActive(int index) => _s.CurtainActive[index];

        public int LockboxCount => _lockboxes.Length;
        public string LockboxId(int index) => _lockboxes[index].Id;
        public IReadOnlyList<int> LockboxBottles(int index) => _lockboxes[index].Bottles;
        public bool IsLockboxActive(int index) => _s.LockboxActive[index];

        public int BombCount => _bombBudget.Length;
        public int BombFuse(int bomb) => _s.BombFuse[bomb];
        public bool IsBombLit(int bomb) => _s.BombLit[bomb];
        public bool IsBombDefused(int bomb) => _s.BombDefused[bomb];

        public int BombColor(int bomb)
        {
            for (var b = 0; b < BottleCount; b++)
                for (var s = 0; s < _s.Count[b]; s++)
                    if (_s.Slots[b][s].Bomb == bomb) return _s.Slots[b][s].Color;
            return SlotData.EmptyColor;
        }

        public bool IsAccessible(int bottle)
        {
            for (var i = 0; i < _curtains.Length; i++)
                if (_s.CurtainActive[i] && _curtains[i].Bottle == bottle) return false;
            for (var i = 0; i < _lockboxes.Length; i++)
                if (_s.LockboxActive[i] && System.Array.IndexOf(_lockboxes[i].Bottles, bottle) >= 0) return false;
            return true;
        }

        public bool IsCompleted(int bottle) => IsSealed(bottle);

        public int TopRun(int bottle) => TopRun(_s, bottle);

        public bool CanSelectAsOrigin(int bottle) =>
            !_s.Failed && IsAccessible(bottle) && !IsSealed(bottle) && TopRun(_s, bottle) > 0;

        public bool CanMove(int from, int to) =>
            CanTransfer(from, to) && (IsEmpty(to) || TopColor(to) == TopColor(from));

        // Free Move booster: only the color check is relaxed, so this is false wherever a normal move is legal.
        public bool CanFreeMove(int from, int to) =>
            CanTransfer(from, to) && !IsEmpty(to) && TopColor(to) != TopColor(from);

        public int MoveAmount(int from, int to) =>
            CanMove(from, to) || CanFreeMove(from, to) ? System.Math.Min(TopRun(_s, from), Capacity(to) - _s.Count[to]) : 0;

        public bool HasLegalMove()
        {
            for (var from = 0; from < BottleCount; from++)
                if (CanSelectAsOrigin(from))
                    for (var to = 0; to < BottleCount; to++)
                        if (CanMove(from, to)) return true;
            return false;
        }

        // Bottles over MinTargetCapacity must be complete, smaller ones complete or empty, temporary ones empty.
        public bool IsSolved()
        {
            if (_s.Failed) return false;
            foreach (var defused in _s.BombDefused) if (!defused) return false;
            foreach (var active in _s.CurtainActive) if (active) return false;
            foreach (var active in _s.LockboxActive) if (active) return false;
            for (var b = 0; b < BottleCount; b++)
            {
                if (_s.Count[b] == 0 && (Capacity(b) <= MinTargetCapacity || _s.Temporary[b])) continue;
                if (!IsFullMonochrome(_s, b)) return false;
            }
            return true;
        }

        public bool TryMove(int from, int to, out MoveResult move)
        {
            move = CanMove(from, to) ? Transfer(from, to, free: false) : null;
            return move != null;
        }

        // Burns lit fuses like any transfer.
        public bool TryFreeMove(int from, int to, out MoveResult move)
        {
            move = CanFreeMove(from, to) ? Transfer(from, to, free: true) : null;
            return move != null;
        }

        public bool TryUndo(out MoveResult move)
        {
            if (_history.Count == 0)
            {
                move = null;
                return false;
            }

            var entry = _history.Pop();
            _s = entry.Before;
            move = entry.Move;
            _version++;
            return true;
        }

        // Extra Tube booster or Continue tube. Burns no fuse; Undo history starts over here.
        public int AddBottle(int capacity)
        {
            var b = BottleCount;
            var slots = new Ball[b + 1][];
            System.Array.Copy(_s.Slots, slots, b);
            slots[b] = new Ball[capacity];
            _s.Slots = slots;
            System.Array.Resize(ref _s.Count, b + 1);
            System.Array.Resize(ref _s.Temporary, b + 1);
            _s.Temporary[b] = true;
            _history.Clear();
            _version++;
            return b;
        }

        // Returns the new tube's index, or -1 if the offer is stale or Continue was already used.
        public int ApplyContinue(ContinueOffer offer)
        {
            if (offer == null || ContinueUsed || offer.BoardVersion != _version || offer.Rewind != _s.Failed) return -1;
            if (offer.Rewind)
            {
                if (_history.Count == 0) return -1;
                _s = _history.Pop().Before;
            }
            var tube = AddBottle(offer.TubeCapacity);
            for (var i = 0; i < offer.FuseBonus.Length && i < _s.BombFuse.Length; i++) _s.BombFuse[i] += offer.FuseBonus[i];
            ContinueUsed = true;
            return tube;
        }

        // Rescue search only, on a copy.
        internal void SetFuse(int bomb, int fuse) => _s.BombFuse[bomb] = fuse;

        // Bottle order matters: obstacles are tied to bottle indices.
        internal string StateKey()
        {
            var sb = new System.Text.StringBuilder();
            for (var b = 0; b < BottleCount; b++)
            {
                for (var s = 0; s < _s.Count[b]; s++)
                {
                    var ball = _s.Slots[b][s];
                    if (ball.Hidden) sb.Append('?');
                    sb.Append(ball.Color);
                    if (ball.Key != null) sb.Append('k').Append(ball.Key);
                    if (ball.BombId != 0) sb.Append('b').Append(ball.BombId);
                    sb.Append(',');
                }
                sb.Append('|');
            }
            foreach (var active in _s.CurtainActive) sb.Append(active ? '1' : '0');
            foreach (var active in _s.LockboxActive) sb.Append(active ? '1' : '0');
            for (var i = 0; i < _s.BombFuse.Length; i++)
                sb.Append(_s.BombFuse[i]).Append(_s.BombDefused[i] ? 'd' : _s.BombLit[i] ? 'l' : 'p');
            return sb.ToString();
        }

        private bool CanTransfer(int from, int to)
        {
            if (_s.Failed || from == to || from < 0 || to < 0 || from >= BottleCount || to >= BottleCount) return false;
            if (!CanSelectAsOrigin(from) || !IsAccessible(to) || IsFull(to)) return false;
            return IsEmpty(to) || !_s.Slots[to][_s.Count[to] - 1].Hidden;
        }

        private MoveResult Transfer(int from, int to, bool free)
        {
            var amount = System.Math.Min(TopRun(_s, from), Capacity(to) - _s.Count[to]);
            var before = _s.Clone();
            var completedBefore = new bool[BottleCount];
            for (var b = 0; b < BottleCount; b++) completedBefore[b] = IsSealed(b);

            for (var i = 0; i < amount; i++)
            {
                var ball = _s.Slots[from][--_s.Count[from]];
                _s.Slots[from][_s.Count[from]] = default;
                _s.Slots[to][_s.Count[to]++] = ball;
            }

            // One tick per transfer, whatever the group size; bombs lit by this move's settle don't pay for it.
            for (var i = 0; i < _bombBudget.Length; i++)
                if (_s.BombLit[i] && !_s.BombDefused[i]) _s.BombFuse[i] = System.Math.Max(0, _s.BombFuse[i] - 1);
            _s.Moves++;
            _version++;

            var move = new MoveResult { From = from, To = to, Count = amount, Free = free };
            Settle(move);
            for (var b = 0; b < BottleCount; b++)
                if (!completedBefore[b] && IsSealed(b)) move.Completed.Add(b);
            _history.Push((before, move));
            return move;
        }

        private void Settle(MoveResult move)
        {
            for (var b = 0; b < BottleCount; b++)
                if (IsAccessible(b)) RevealTop(b, move);

            var changed = true;
            while (changed)
            {
                changed = false;
                // An open key at an accessible mouth opens its box once; chains resolve in the same move.
                for (var b = 0; b < BottleCount; b++)
                {
                    var n = _s.Count[b];
                    if (n == 0 || !IsAccessible(b)) continue;
                    ref var top = ref _s.Slots[b][n - 1];
                    if (top.Key == null || top.Hidden) continue;
                    var id = top.Key;
                    top.Key = null;
                    for (var i = 0; i < _lockboxes.Length; i++)
                    {
                        if (!_s.LockboxActive[i] || _lockboxes[i].Id != id) continue;
                        _s.LockboxActive[i] = false;
                        move.LockboxesOpened.Add(i);
                        changed = true;
                        foreach (var inside in _lockboxes[i].Bottles) RevealTop(inside, move);
                    }
                }

                for (var i = 0; i < _curtains.Length; i++)
                {
                    if (!_s.CurtainActive[i] || !CurtainCondition(_curtains[i].Color, _curtains[i].Need)) continue;
                    _s.CurtainActive[i] = false;
                    move.CurtainsLifted.Add(i);
                    changed = true;
                    RevealTop(_curtains[i].Bottle, move);
                }
            }

            SettleBombs(move);
        }

        private bool CurtainCondition(int color, int need)
        {
            for (var b = 0; b < BottleCount; b++)
                if (IsAccessible(b) && IsFullMonochrome(_s, b) && _s.Slots[b][0].Color == color && _s.Count[b] >= need)
                    return true;
            return false;
        }

        // Defuse before the zero check, so completing the bomb's bottle on its last allowed move still wins.
        private void SettleBombs(MoveResult move)
        {
            for (var b = 0; b < BottleCount; b++)
            {
                var full = IsFullMonochrome(_s, b);
                var run = IsAccessible(b) ? TopRun(_s, b) : 0;
                for (var s = 0; s < _s.Count[b]; s++)
                {
                    var bomb = _s.Slots[b][s].Bomb;
                    if (bomb < 0 || _s.BombDefused[bomb]) continue;
                    if (full)
                    {
                        _s.BombDefused[bomb] = true;
                        _s.BombLit[bomb] = false;
                        move.BombsDefused.Add(bomb);
                    }
                    else if (!_s.BombLit[bomb] && s >= _s.Count[b] - run)
                    {
                        // Lit on joining the movable group at a mouth.
                        _s.BombLit[bomb] = true;
                        move.BombsActivated.Add(bomb);
                    }
                }
            }

            for (var i = 0; i < _bombBudget.Length; i++)
                if (_s.BombLit[i] && !_s.BombDefused[i] && _s.BombFuse[i] <= 0) _s.Failed = true;
            move.Exploded = _s.Failed;
        }

        private void RevealTop(int bottle, MoveResult move)
        {
            var n = _s.Count[bottle];
            if (n == 0 || !_s.Slots[bottle][n - 1].Hidden) return;
            _s.Slots[bottle][n - 1].Hidden = false;
            move.Revealed.Add((bottle, n - 1));
        }

        private static int TopRun(State s, int bottle)
        {
            var n = s.Count[bottle];
            if (n == 0 || s.Slots[bottle][n - 1].Hidden) return 0;
            var color = s.Slots[bottle][n - 1].Color;
            var run = 0;
            for (var i = n - 1; i >= 0 && !s.Slots[bottle][i].Hidden && s.Slots[bottle][i].Color == color; i--) run++;
            return run;
        }

        // Full and one color, unless a larger bottle is still meant to collect more of that color.
        private bool IsSealed(int bottle)
        {
            if (!IsFullMonochrome(_s, bottle)) return false;
            var capacity = Capacity(bottle);
            return capacity >= _largestCapacity || _colorTotal[_s.Slots[bottle][0].Color] <= capacity;
        }

        private static bool IsFullMonochrome(State s, int bottle)
        {
            var slots = s.Slots[bottle];
            if (s.Temporary[bottle] || slots.Length < MinTargetCapacity || s.Count[bottle] != slots.Length) return false;
            foreach (var ball in slots)
                if (ball.Hidden || ball.Color != slots[0].Color) return false;
            return true;
        }

        // Stuck unless a top run fits across its legal targets or can enter an empty or one-color, non-full bottle.
        public bool HasNoMove()
        {
            for (var from = 0; from < BottleCount; from++)
            {
                if (!CanSelectAsOrigin(from)) continue;
                var run = TopRun(_s, from);
                var below = _s.Count[from] - run;
                var targets = 0;
                var free = 0;
                for (var to = 0; to < BottleCount; to++)
                {
                    if (!CanMove(from, to)) continue;
                    if (IsEmpty(to) && below == 0) continue;
                    targets++;
                    free += Capacity(to) - _s.Count[to];
                }
                if (targets > 0 && run <= free) return false;
            }

            for (var to = 0; to < BottleCount; to++)
            {
                if (!(IsEmpty(to) || IsSingleVisibleColorNotFull(to))) continue;
                for (var from = 0; from < BottleCount; from++)
                {
                    if (!CanMove(from, to)) continue;
                    // Pouring a single-color bottle into an empty one no bigger than it changes nothing.
                    if (IsEmpty(to) && TopRun(_s, from) == _s.Count[from] && Capacity(to) <= Capacity(from)) continue;
                    return false;
                }
            }
            return true;
        }

        private bool IsSingleVisibleColorNotFull(int bottle) =>
            _s.Count[bottle] > 0 && !IsFull(bottle) && TopRun(_s, bottle) == _s.Count[bottle];

        private static bool[] Filled(int length, bool value)
        {
            var array = new bool[length];
            for (var i = 0; i < length; i++) array[i] = value;
            return array;
        }
    }
}
