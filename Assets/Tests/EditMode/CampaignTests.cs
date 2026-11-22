using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    // Each imported booster-free solution must replay to a win with the recorded group sizes.
    public sealed class CampaignTests
    {
        private const string CatalogPath = "Assets/Configs/LevelCatalog.asset";
        private const string SolutionsPath = "Assets/Tests/EditMode/CampaignSolutions.json";

        [Serializable]
        private sealed class Solution
        {
            public string Level;
            public List<int> Moves = new();
        }

        [Serializable]
        private sealed class SolutionFile
        {
            public List<Solution> Boards = new();
        }

        private static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        private static Dictionary<string, Solution> _solutions;

        private static Dictionary<string, Solution> LoadSolutions()
        {
            if (_solutions != null) return _solutions;
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SolutionsPath);
            if (text == null) return new();
            return _solutions = JsonUtility.FromJson<SolutionFile>(text.text).Boards.ToDictionary(s => s.Level);
        }

        private static IEnumerable<(int Number, int Variant, LevelData Level)> Boards()
        {
            var catalog = Catalog;
            if (catalog == null) yield break;
            foreach (var entry in catalog.Levels)
                for (var v = 0; v < entry.Variants.Count; v++)
                    yield return (entry.Number, v, entry.Variants[v]);
        }

        private static IEnumerable<string> BoardNames() => Boards().Select(b => b.Level.name);

        [Test]
        public void CampaignHasOneHundredLevelsOfFiveVariantsWithASolutionEach()
        {
            Assert.IsNotNull(Catalog, "Import the campaign first");
            Assert.AreEqual(100, Catalog.Count);
            Assert.IsTrue(Catalog.Levels.All(e => e.Variants.Count == 5));
            var solutions = LoadSolutions();
            Assert.AreEqual(500, solutions.Count);
            Assert.IsTrue(BoardNames().All(solutions.ContainsKey));
        }

        [TestCaseSource(nameof(BoardNames))]
        public void SolutionReplaysToAWin(string name)
        {
            var level = Boards().First(b => b.Level.name == name).Level;
            var moves = LoadSolutions()[name].Moves;
            var board = new SortBoard(level);
            Assert.IsFalse(board.IsSolved(), "the board must not start solved");

            for (var i = 0; i < moves.Count; i += 3)
            {
                int from = moves[i], to = moves[i + 1], count = moves[i + 2];
                Assert.AreEqual(count, board.MoveAmount(from, to), $"move {i / 3}: group size {from}->{to}");
                Assert.IsTrue(board.TryMove(from, to, out _), $"move {i / 3}: {from}->{to} is illegal");
                Assert.IsFalse(board.IsFailed, $"move {i / 3}: a bomb exploded");
            }
            Assert.IsTrue(board.IsSolved(), "the solution does not reach the goal");
        }

        // The in-game solver backs the fail screen, so it must finish every board a player can get.
        [Test]
        public void SolverFinishesEveryBoard()
        {
            var failures = new List<string>();
            var route = new List<(int From, int To)>();
            foreach (var (_, _, level) in Boards())
            {
                var outcome = BoardSolver.Solve(new SortBoard(level), route, 400000, 5000);
                if (outcome != BoardSolver.Outcome.Solved) failures.Add($"{level.name}: {outcome}");
            }
            CollectionAssert.IsEmpty(failures);
        }

        [Test]
        public void NoBoardAfterLevelTenIsWonInOneMove()
        {
            var failures = new List<string>();
            foreach (var (number, _, level) in Boards())
            {
                if (number <= 10) continue;
                var board = new SortBoard(level);
                for (var from = 0; from < board.BottleCount; from++)
                for (var to = 0; to < board.BottleCount; to++)
                {
                    if (!board.TryMove(from, to, out _)) continue;
                    if (board.IsSolved()) failures.Add($"{level.name} {from}->{to}");
                    board.TryUndo(out _);
                }
            }
            CollectionAssert.IsEmpty(failures);
        }

        [Test]
        public void MechanicsFirstAppearAtTheirLevels()
        {
            var failures = new List<string>();
            foreach (var (number, _, level) in Boards())
            {
                var slots = level.Bottles.SelectMany(b => b.Slots).ToList();
                if (number < 7 && slots.Any(s => !s.IsEmpty && s.Kind == SlotKind.Special)) failures.Add($"{level.name}: hidden");
                if (number < 20 && level.Bottles.Any(b => b.Capacity > 4)) failures.Add($"{level.name}: long tube");
                if (number < 40 && level.Curtains.Count > 0) failures.Add($"{level.name}: curtain");
                if (number < 60 && (level.Lockboxes.Count > 0 || slots.Any(s => s.IsKey))) failures.Add($"{level.name}: lockbox");
                if (number < 80 && slots.Any(s => s.IsBomb)) failures.Add($"{level.name}: bomb");
            }
            CollectionAssert.IsEmpty(failures);
        }

        [Test]
        public void CapacitiesAndRowsFollowTheLayoutRules()
        {
            var failures = new List<string>();
            foreach (var (_, _, level) in Boards())
            {
                if (level.Bottles.Any(b => !LevelRules.IsTubeCapacity(b.Capacity))) failures.Add($"{level.name}: capacity");
                var widest = level.Bottles.GroupBy(b => Mathf.Round(b.Position.y * 100f)).Max(row => row.Count());
                if (widest > LevelRules.MaxPerRow) failures.Add($"{level.name}: {widest} bottles in a row");
            }
            CollectionAssert.IsEmpty(failures);
        }

        // A box whose key starts inside another box must open after that box.
        [Test]
        public void KeysOpenTheirBoxesInOrder()
        {
            var solutions = LoadSolutions();
            var failures = new List<string>();
            foreach (var (number, _, level) in Boards())
            {
                var keys = KeyBottles(level);
                if (keys.Count == 0) continue;
                if ((keys.Count == 1) != (number == 60)) failures.Add($"{level.name}: {keys.Count} keys");

                var board = new SortBoard(level);
                var opened = new Dictionary<string, int>();
                var moves = solutions[level.name].Moves;
                for (var i = 0; i < moves.Count; i += 3)
                {
                    board.TryMove(moves[i], moves[i + 1], out var move);
                    foreach (var box in move.LockboxesOpened) opened.TryAdd(board.LockboxId(box), i / 3 + 1);
                }

                foreach (var (boxId, bottle) in keys)
                {
                    if (!opened.ContainsKey(boxId)) { failures.Add($"{level.name}: box {boxId} never opens"); continue; }
                    var parent = level.Lockboxes.FirstOrDefault(b => b.Bottles.Contains(bottle));
                    if (parent == null && boxId != "A") failures.Add($"{level.name}: key {boxId} outside a box");
                    if (parent != null && !(opened[parent.Id] < opened[boxId])) failures.Add($"{level.name}: key {boxId} before box {parent.Id}");
                }
            }
            CollectionAssert.IsEmpty(failures);
        }

        // Breadth-first search that either inner box can be opened first.
        [TestCase(85, "B", "C")]
        [TestCase(85, "C", "B")]
        [TestCase(95, "B", "C")]
        [TestCase(95, "C", "B")]
        public void EitherBranchCanOpenFirst(int number, string wanted, string other)
        {
            var level = Catalog.Levels.First(e => e.Number == number).Variants[0];
            var queue = new Queue<List<(int, int)>>();
            queue.Enqueue(new List<(int, int)>());
            var seen = new HashSet<string>();
            for (var visited = 0; queue.Count > 0 && visited < 20000; visited++)
            {
                var path = queue.Dequeue();
                var board = Replay(level, path);
                if (!IsBoxActive(board, other)) continue;
                if (!IsBoxActive(board, wanted)) return;
                for (var from = 0; from < board.BottleCount; from++)
                for (var to = 0; to < board.BottleCount; to++)
                {
                    if (!board.TryMove(from, to, out _)) continue;
                    if (seen.Add(Key(board))) queue.Enqueue(new List<(int, int)>(path) { (from, to) });
                    board.TryUndo(out _);
                }
            }
            Assert.Fail($"Box {wanted} can not be opened before box {other}.");
        }

        private static SortBoard Replay(LevelData level, List<(int From, int To)> path)
        {
            var board = new SortBoard(level);
            foreach (var (from, to) in path) board.TryMove(from, to, out _);
            return board;
        }

        private static bool IsBoxActive(SortBoard board, string id)
        {
            for (var i = 0; i < board.LockboxCount; i++)
                if (board.LockboxId(i) == id) return board.IsLockboxActive(i);
            return false;
        }

        private static string Key(SortBoard board)
        {
            var sb = new StringBuilder();
            for (var b = 0; b < board.BottleCount; b++)
            {
                for (var s = 0; s < board.Count(b); s++)
                    sb.Append(board.IsKnown(b, s) ? "" : "?").Append(board.ColorAt(b, s)).Append(board.KeyAt(b, s)).Append(',');
                sb.Append('|');
            }
            for (var i = 0; i < board.LockboxCount; i++) sb.Append(board.IsLockboxActive(i) ? '1' : '0');
            for (var i = 0; i < board.CurtainCount; i++) sb.Append(board.IsCurtainActive(i) ? '1' : '0');
            for (var i = 0; i < board.BombCount; i++) sb.Append(board.BombFuse(i)).Append(board.IsBombLit(i) ? 'l' : 'p');
            return sb.ToString();
        }

        private static List<(string BoxId, int Bottle)> KeyBottles(LevelData level)
        {
            var keys = new List<(string, int)>();
            for (var b = 0; b < level.Bottles.Count; b++)
                foreach (var slot in level.Bottles[b].Slots)
                    if (slot.IsKey) keys.Add((slot.Key, b));
            return keys;
        }
    }
}
