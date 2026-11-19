using System.Collections.Generic;

namespace Project.Contexts.Game.Sort
{
    // After landing one outcome plays by priority: explosion, win, defuse, bomb lit, box, curtain, cap; not on Undo.
    public static class MechanicSounds
    {
        public const string Hidden = "hidden";
        public const string BombBurst = "bomb_burst";
        public const string BombSafe = "bomb_safe";
        public const string BombArm = "bomb_arm";
        public const string BombLow = "bomb_low";
        public const string Unlock = "unlock";
        public const string Cover = "cover";
        private const int LowFuse = 2;

        // Plays at move start so the clip's shell shake lines up with the reveal.
        public static string OnMove(MoveResult move) => move.Revealed.Count > 0 ? Hidden : null;

        public static void OnLanded(MoveResult move, SortBoard board, bool won, List<string> events)
        {
            if (move.Exploded)
            {
                events.Add(BombBurst);
                return;
            }
            if (won) return;

            var outcome = move.BombsDefused.Count > 0 ? BombSafe
                : move.BombsActivated.Count > 0 ? BombArm
                : move.LockboxesOpened.Count > 0 ? Unlock
                : move.CurtainsLifted.Count > 0 ? Cover
                : CapSound(move, board);
            if (outcome != null) events.Add(outcome);

            for (var i = 0; i < board.BombCount; i++)
            {
                if (!board.IsBombLit(i) || board.IsBombDefused(i) || board.BombFuse(i) > LowFuse) continue;
                events.Add(BombLow);
                break;
            }
        }

        // tube_4 / tube_8 / tube_12 / tube_16, by the largest bottle completed.
        private static string CapSound(MoveResult move, SortBoard board)
        {
            var capacity = 0;
            foreach (var bottle in move.Completed) capacity = System.Math.Max(capacity, board.Capacity(bottle));
            return capacity > 0 ? $"tube_{capacity}" : null;
        }
    }
}
