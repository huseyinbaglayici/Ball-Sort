using System.Collections.Generic;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // Checks a layout is solvable, so a rule change that breaks levels shows up at once.
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

        // Solution moves are (from, to) bottle indices.
        public static Outcome Solve(LevelData level, int nodeLimit, List<Vector2Int> solution)
        {
            var route = new List<(int From, int To)>();
            var outcome = BoardSolver.Solve(new SortBoard(level), route, nodeLimit);
            solution.Clear();
            foreach (var (from, to) in route) solution.Add(new Vector2Int(from, to));
            return outcome switch
            {
                BoardSolver.Outcome.Solved => Outcome.Solved,
                BoardSolver.Outcome.Unsolvable => Outcome.Unsolvable,
                _ => Outcome.LimitReached
            };
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
