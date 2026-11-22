using System.Collections.Generic;
using NUnit.Framework;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Tests
{
    // The level editor's checks: the validator catches broken layouts, the solver never calls a dead board solvable.
    public sealed class LevelCheckTests
    {
        private readonly List<LevelData> _made = new();

        [TearDown]
        public void Clean()
        {
            foreach (var level in _made) Object.DestroyImmediate(level);
            _made.Clear();
        }

        // Bottom to top per tube; -1 leaves the rest empty.
        private LevelData Level(int capacity, params int[][] tubes)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            _made.Add(level);
            for (var b = 0; b < tubes.Length; b++)
            {
                var bottle = BottleData.CreateEmpty(capacity, new Vector2(b * 2f, 0f));
                for (var s = 0; s < tubes[b].Length; s++) bottle.Slots[s] = new SlotData { Color = tubes[b][s] };
                level.Bottles.Add(bottle);
            }
            return level;
        }

        private static void SetKey(LevelData level, int bottle, int slot, string id)
        {
            var data = level.Bottles[bottle].Slots[slot];
            data.Key = id;
            level.Bottles[bottle].Slots[slot] = data;
        }

        private static List<string> Errors(LevelData level)
        {
            var errors = new List<string>();
            LevelValidator.Validate(level, errors);
            return errors;
        }

        private static BoardSolver.Outcome Solve(LevelData level) =>
            BoardSolver.Solve(new SortBoard(level), new List<(int, int)>(), 2000000, 10000);

        [Test]
        public void SimpleBoardIsValidAndSolvable()
        {
            var level = Level(4, new[] { 0, 1, 0, 1 }, new[] { 1, 0, 1, 0 }, new int[0]);
            CollectionAssert.IsEmpty(Errors(level));
            Assert.AreEqual(BoardSolver.Outcome.Solved, Solve(level));
        }

        [Test]
        public void TubeSizeOutsideTheArtIsAnError()
        {
            var level = Level(5, new[] { 0, 0, 0, 0, 0 }, new int[0]);
            Assert.IsTrue(Errors(level).Exists(e => e.Contains("tubes come in")));
        }

        [Test]
        public void KeyInsideItsOwnBoxIsAnError()
        {
            var level = Level(4, new[] { 0, 0, 0, 0 }, new[] { 1, 1, 1, 1 }, new int[0]);
            SetKey(level, 1, 0, "A");
            level.Lockboxes.Add(new LockboxData { Id = "A", Bottles = { 1 } });
            Assert.IsTrue(Errors(level).Exists(e => e.Contains("its own box")));
        }

        [Test]
        public void KeyWithoutABoxAndBoxWithoutAKeyAreErrors()
        {
            var level = Level(4, new[] { 0, 0, 0, 0 }, new[] { 1, 1, 1, 1 }, new int[0]);
            SetKey(level, 0, 0, "A");
            level.Lockboxes.Add(new LockboxData { Id = "B", Bottles = { 1 } });
            var errors = Errors(level);
            Assert.IsTrue(errors.Exists(e => e.Contains("Key A opens no lockbox")));
            Assert.IsTrue(errors.Exists(e => e.Contains("Lockbox B has no key")));
        }

        [Test]
        public void FrozenTubeWaitingForMoreBallsThanTheLevelHasIsAnError()
        {
            var level = Level(4, new[] { 0, 0, 0, 0 }, new int[0], new int[0]);
            level.Curtains.Add(new CurtainData { Bottle = 2, Color = 0, Need = 8 });
            Assert.IsTrue(Errors(level).Exists(e => e.Contains("waits for 8")));
        }

        [Test]
        public void TooManyTubesInARowIsAnError()
        {
            var tubes = new int[LevelRules.MaxPerRow + 1][];
            for (var i = 0; i < tubes.Length; i++) tubes[i] = new int[0];
            tubes[0] = new[] { 0, 0, 0, 0 };
            Assert.IsTrue(Errors(Level(4, tubes)).Exists(e => e.Contains("A row has")));
        }

        // Each key waits behind the other box: valid on paper, but no line can ever open either box.
        [Test]
        public void CrossLockedKeysAreProvenUnsolvable()
        {
            var level = Level(4, new[] { 0, 1, 1, 1 }, new[] { 1, 0, 0, 0 }, new int[0], new int[0]);
            SetKey(level, 0, 0, "B");
            SetKey(level, 1, 0, "A");
            level.Lockboxes.Add(new LockboxData { Id = "A", Bottles = { 0 } });
            level.Lockboxes.Add(new LockboxData { Id = "B", Bottles = { 1 } });
            CollectionAssert.IsEmpty(Errors(level));
            Assert.AreEqual(BoardSolver.Outcome.Unsolvable, Solve(level));
        }

        // The only way on is pouring a one-color run with its key at the bottom into an empty tube.
        [Test]
        public void PouringARunThatFlipsAKeyUpIsTried()
        {
            var level = Level(4, new[] { 2, 2 }, new[] { 2, 2, 0, 0 }, new[] { 0, 0, 1, 1 }, new int[0], new[] { 1, 1 });
            SetKey(level, 0, 0, "A");
            level.Lockboxes.Add(new LockboxData { Id = "A", Bottles = { 1, 2 } });
            CollectionAssert.IsEmpty(Errors(level));
            Assert.AreEqual(BoardSolver.Outcome.Solved, Solve(level));
        }

        [Test]
        public void SearchThatRunsOutOfDepthIsNotCalledUnsolvable()
        {
            var level = Level(4, new[] { 0, 1, 0, 1 }, new[] { 1, 0, 1, 0 }, new int[0]);
            var outcome = BoardSolver.Solve(new SortBoard(level), new List<(int, int)>(), 2000000, 0, maxDepth: 1);
            Assert.AreEqual(BoardSolver.Outcome.LimitReached, outcome);
        }
    }
}
