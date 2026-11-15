using System.Collections.Generic;
using System.Text;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>
    /// Depth-first search over <see cref="SortBoard"/> moves. It proves the move rules can solve a layout
    /// (or finds that they cannot), so a rule change that breaks levels shows up immediately.
    /// </summary>
    public static class LevelSolver
    {
        public enum Outcome
        {
            Solved,
            Unsolvable,
            LimitReached
        }

        public static Outcome Solve(LevelData level, int nodeLimit, out int moveCount)
        {
            var path = new List<Vector2Int>();
            var outcome = Solve(level, nodeLimit, path);
            moveCount = path.Count;
            return outcome;
        }

        /// <summary>On success <paramref name="solution"/> holds the moves as (from, to) bottle indices.</summary>
        public static Outcome Solve(LevelData level, int nodeLimit, List<Vector2Int> solution)
        {
            var board = new SortBoard(level);
            var seen = new HashSet<string>();
            var budget = nodeLimit;
            var limitHit = false;
            solution.Clear();

            if (!Search(board, seen, solution, ref budget, ref limitHit)) return limitHit ? Outcome.LimitReached : Outcome.Unsolvable;
            return Outcome.Solved;
        }

        private static bool Search(SortBoard board, HashSet<string> seen, List<Vector2Int> path, ref int budget, ref bool limitHit)
        {
            if (board.IsSolved()) return true;
            if (budget-- <= 0)
            {
                limitHit = true;
                return false;
            }
            if (!seen.Add(Key(board))) return false;

            // Moves onto a non-empty bottle first, then onto an empty one.
            for (var pass = 0; pass < 2; pass++)
            for (var from = 0; from < board.BottleCount; from++)
            for (var to = 0; to < board.BottleCount; to++)
            {
                if (!board.CanMove(from, to)) continue;
                var toEmpty = board.IsEmpty(to);
                if (toEmpty != (pass == 1)) continue;
                // Moving a whole single-color bottle into an empty bottle changes nothing.
                if (toEmpty && board.TopRun(from) == board.Count(from)) continue;

                board.TryMove(from, to, out _);
                path.Add(new Vector2Int(from, to));
                if (Search(board, seen, path, ref budget, ref limitHit)) return true;
                path.RemoveAt(path.Count - 1);
                board.TryUndo(out _);
                if (limitHit) return false;
            }
            return false;
        }

        // Bottles are interchangeable, so the key sorts them.
        private static string Key(SortBoard board)
        {
            var parts = new string[board.BottleCount];
            var sb = new StringBuilder();
            for (var b = 0; b < board.BottleCount; b++)
            {
                sb.Clear();
                for (var s = 0; s < board.Count(b); s++)
                    sb.Append(board.IsKnown(b, s) ? board.ColorAt(b, s).ToString() : "?" + board.ColorAt(b, s)).Append(',');
                if (board.IsCompleted(b)) sb.Append('*');
                parts[b] = sb.ToString();
            }
            System.Array.Sort(parts, System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        [MenuItem("Tools/Project/Check Levels Solvable (first layouts)")]
        private static void CheckFirstLayouts() => CheckAll(allLayouts: false);

        [MenuItem("Tools/Project/Check Levels Solvable (all layouts)")]
        private static void CheckAllLayouts() => CheckAll(allLayouts: true);

        private static void CheckAll(bool allLayouts)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[LevelSolver] No LevelCatalog. Run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            int solved = 0, unsolvable = 0, limit = 0, total = 0;
            var problems = new List<string>();
            foreach (var entry in catalog.Levels)
            {
                var count = allLayouts ? entry.Variants.Count : System.Math.Min(1, entry.Variants.Count);
                for (var v = 0; v < count; v++)
                {
                    total++;
                    var outcome = Solve(entry.Variants[v], 200000, out _);
                    if (outcome == Outcome.Solved) solved++;
                    else
                    {
                        if (outcome == Outcome.Unsolvable) unsolvable++; else limit++;
                        problems.Add($"{entry.Variants[v].name}: {outcome}");
                    }
                }
            }

            Debug.Log($"[LevelSolver] {total} layouts: {solved} solved, {unsolvable} unsolvable, {limit} hit the search limit." +
                      (problems.Count > 0 ? "\n" + string.Join("\n", problems.GetRange(0, System.Math.Min(30, problems.Count))) : string.Empty));
        }
    }
}
