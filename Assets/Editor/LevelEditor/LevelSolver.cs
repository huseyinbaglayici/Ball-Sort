using System;
using System.Collections.Generic;
using System.Diagnostics;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Project.Editor.LevelEditor
{
    // Checks a layout is solvable without boosters, so a rule change or an edit that breaks a level shows up at once.
    public static class LevelSolver
    {
        public const int DefaultNodeLimit = 3000000;
        public const int DefaultTimeoutMs = 20000;

        public enum Outcome
        {
            Solved,
            // Every reachable position was tried; no line finishes it.
            Unsolvable,
            // The search stopped at its node, time or depth limit before it could tell.
            NotProven,
            // The layout itself is broken (validator errors or a board that can't be built).
            Invalid
        }

        public sealed class Report
        {
            public Outcome Outcome;
            public readonly List<(int From, int To)> Route = new();
            public long Milliseconds;
            public string Detail = string.Empty;

            public string Summary => Outcome switch
            {
                Outcome.Solved => $"Solvable in {Route.Count} moves ({Milliseconds} ms)",
                Outcome.Unsolvable => $"Unsolvable: every position was tried ({Milliseconds} ms)",
                Outcome.NotProven => $"Not proven: the search hit its limit after {Milliseconds} ms",
                _ => $"Invalid: {Detail}"
            };
        }

        public static Report Check(LevelData level, int nodeLimit = DefaultNodeLimit, int timeoutMs = DefaultTimeoutMs)
        {
            var report = new Report();
            var errors = new List<string>();
            LevelValidator.Validate(level, errors);
            if (errors.Count > 0)
            {
                report.Outcome = Outcome.Invalid;
                report.Detail = errors[0];
                return report;
            }

            var clock = Stopwatch.StartNew();
            try
            {
                var outcome = BoardSolver.Solve(new SortBoard(level), report.Route, nodeLimit, timeoutMs);
                report.Outcome = outcome switch
                {
                    BoardSolver.Outcome.Solved => Outcome.Solved,
                    BoardSolver.Outcome.Unsolvable => Outcome.Unsolvable,
                    _ => Outcome.NotProven
                };
            }
            catch (Exception e)
            {
                report.Outcome = Outcome.Invalid;
                report.Detail = e.Message;
            }
            report.Milliseconds = clock.ElapsedMilliseconds;
            return report;
        }

        [MenuItem("Tools/Project/Check Levels Solvable (first layouts)")]
        private static void CheckFirstLayouts() => CheckCatalog(allLayouts: false);

        [MenuItem("Tools/Project/Check Levels Solvable (all layouts)")]
        public static void CheckAllLayouts() => CheckCatalog(allLayouts: true);

        private static void CheckCatalog(bool allLayouts)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[LevelSolver] No LevelCatalog. Run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            var counts = new int[Enum.GetValues(typeof(Outcome)).Length];
            var problems = new List<string>();
            var total = 0;
            try
            {
                foreach (var entry in catalog.Levels)
                {
                    var count = allLayouts ? entry.Variants.Count : Math.Min(1, entry.Variants.Count);
                    for (var v = 0; v < count; v++)
                    {
                        var level = entry.Variants[v];
                        if (EditorUtility.DisplayCancelableProgressBar("Checking levels", level.name, entry.Number / (float)catalog.Count)) return;
                        total++;
                        var report = Check(level, DefaultNodeLimit, 5000);
                        counts[(int)report.Outcome]++;
                        if (report.Outcome != Outcome.Solved) problems.Add($"{level.name}: {report.Summary}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var line = $"[LevelSolver] {total} layouts: {counts[(int)Outcome.Solved]} solvable, {counts[(int)Outcome.Unsolvable]} unsolvable, " +
                       $"{counts[(int)Outcome.NotProven]} not proven, {counts[(int)Outcome.Invalid]} invalid.";
            if (problems.Count == 0) Debug.Log(line);
            else Debug.LogWarning(line + "\n" + string.Join("\n", problems));
        }
    }
}
