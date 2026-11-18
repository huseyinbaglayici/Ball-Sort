using NUnit.Framework;
using Project.Contexts.Game.Sort;

namespace Project.Tests
{
    public sealed class SortBoardRulesTests
    {
        private static MoveResult Move(SortBoard board, int from, int to)
        {
            Assert.IsTrue(board.TryMove(from, to, out var move), $"move {from}->{to} should be legal");
            return move;
        }

        [Test]
        public void GroupMovesAsMuchAsTargetHasRoom()
        {
            var board = BoardBuilder.Of("4|1 0 0 0", "4|2 2 0").Build();
            Assert.AreEqual(1, Move(board, 0, 1).Count);
            Assert.AreEqual(2, board.TopRun(0));
        }

        [Test]
        public void TargetMustBeEmptyOrShowSameColor()
        {
            var board = BoardBuilder.Of("4|0", "4|1", "4|").Build();
            Assert.IsFalse(board.CanMove(0, 1));
            Assert.IsTrue(board.CanMove(0, 2));
        }

        [Test]
        public void HiddenBallBreaksGroupEvenWhenItsColorMatches()
        {
            var board = BoardBuilder.Of("4|?0 0", "4|").Build();
            Assert.AreEqual(1, board.TopRun(0));
            Assert.IsFalse(board.IsKnown(0, 0));
        }

        [Test]
        public void AmountIsFixedAtStartRevealedBallDoesNotJoin()
        {
            var board = BoardBuilder.Of("4|?0 0 0", "4|").Build();
            var move = Move(board, 0, 1);
            Assert.AreEqual(2, move.Count);
            Assert.AreEqual(1, board.Count(0));
            Assert.IsTrue(board.IsKnown(0, 0));
            CollectionAssert.Contains(move.Revealed, (0, 0));
        }

        [Test]
        public void SpecExampleTwoHiddenUnderOpenReds()
        {
            // [hidden red, hidden red, red] + red on top: the movable group is 2, moving it opens only one red.
            var board = BoardBuilder.Of("4|?0 ?0 0", "4|1 0", "4|").Build();
            Move(board, 1, 0);
            Assert.AreEqual(2, board.TopRun(0));
            Assert.IsFalse(board.IsCompleted(0), "a hidden ball under a one-color stack is not complete");

            var move = Move(board, 0, 2);
            Assert.AreEqual(2, move.Count);
            Assert.IsTrue(board.IsKnown(0, 1));
            Assert.IsFalse(board.IsKnown(0, 0));

            Assert.IsTrue(board.TryUndo(out _));
            Assert.AreEqual(4, board.Count(0));
            Assert.IsFalse(board.IsKnown(0, 0));
            Assert.IsFalse(board.IsKnown(0, 1));
            Assert.AreEqual(2, board.TopRun(0));
        }

        [Test]
        public void FullTubeStaysOpenWhileALargerTubeCollectsItsColor()
        {
            var board = BoardBuilder.Of("4|0 0 0 0", "8|0 0 0 0").Build();
            Assert.IsFalse(board.IsCompleted(0));
            Assert.AreEqual(4, Move(board, 0, 1).Count);
            Assert.IsTrue(board.IsSolved());
        }

        [Test]
        public void LongTubeMustBeFullEvenWhenItStartsEmpty()
        {
            var board = BoardBuilder.Of("8|", "4|0 0 0 0", "4|0 0 0 0").Build();
            Assert.IsFalse(board.IsSolved());
            Move(board, 1, 0);
            Assert.IsFalse(board.IsSolved());
            var move = Move(board, 2, 0);
            CollectionAssert.Contains(move.Completed, 0);
            Assert.IsTrue(board.IsSolved());
        }

        [Test]
        public void LongTubeWithHiddenBallIsNotComplete()
        {
            var board = BoardBuilder.Of("8|?0 0 0 0 0 0 0 0").Build();
            Assert.IsFalse(board.IsCompleted(0));
        }

        [Test]
        public void CurtainedBottleIsUnusable()
        {
            var board = BoardBuilder.Of("4|0", "4|0", "4|").Curtain(1, 2, 4).Build();
            Assert.IsFalse(board.IsAccessible(1));
            Assert.IsFalse(board.CanSelectAsOrigin(1));
            Assert.IsFalse(board.CanMove(0, 1));
        }

        [Test]
        public void CompletedBottleIsClosedForPickingAndFreeMove()
        {
            var board = BoardBuilder.Of("4|0 0 0", "4|0", "4|", "4|1").Build();
            Move(board, 1, 0);
            Assert.IsTrue(board.IsCompleted(0));
            Assert.IsFalse(board.CanSelectAsOrigin(0));
            Assert.IsFalse(board.CanMove(0, 2));
            Assert.IsFalse(board.CanFreeMove(0, 3));
            Assert.IsFalse(board.TryMove(0, 2, out _));
            Assert.IsTrue(board.CanSelectAsOrigin(3));
        }

        [Test]
        public void CurtainCountsNeverAddUpAcrossBottles()
        {
            var board = BoardBuilder.Of("4|0 0 0 0", "4|0 0 0", "4|0", "4|1").Curtain(3, 0, 8).Build();
            Move(board, 2, 1);
            Assert.IsTrue(board.IsCompleted(0) && board.IsCompleted(1));
            Assert.IsTrue(board.IsCurtainActive(0));
        }

        [Test]
        public void CurtainLiftsOnTheMoveThatCompletesItsColorAtNoExtraMove()
        {
            var board = BoardBuilder.Of("8|0 0 0 0 0 0 0", "4|0", "4|1 1 1 ?1").Curtain(2, 0, 8).Build();
            Assert.IsFalse(board.IsKnown(2, 3));
            var move = Move(board, 1, 0);
            CollectionAssert.Contains(move.CurtainsLifted, 0);
            Assert.IsTrue(board.IsAccessible(2));
            Assert.IsTrue(board.IsKnown(2, 3), "the lifted bottle's mouth comes open");
            Assert.AreEqual(1, board.MoveCount);

            board.TryUndo(out _);
            Assert.IsTrue(board.IsCurtainActive(0));
            Assert.IsFalse(board.IsKnown(2, 3));
        }

        [Test]
        public void TwoCurtainsWithTheSameConditionLiftTogether()
        {
            var board = BoardBuilder.Of("4|0 0 0", "4|0", "4|1", "4|2").Curtain(2, 0, 4).Curtain(3, 0, 4).Build();
            var move = Move(board, 1, 0);
            Assert.AreEqual(2, move.CurtainsLifted.Count);
        }

        [Test]
        public void KeyAtAnOpenMouthOpensItsBoxInTheSameMove()
        {
            var board = BoardBuilder.Of("4|1kA 0", "4|2", "4|").Lockbox("A", 1).Build();
            Assert.IsFalse(board.IsAccessible(1));
            var move = Move(board, 0, 2);
            CollectionAssert.Contains(move.LockboxesOpened, 0);
            Assert.IsTrue(board.IsAccessible(1));
            Assert.IsNull(board.KeyAt(0, 0), "a key is used once");
        }

        [Test]
        public void HiddenKeyOpensOnlyOnceItComesOpen()
        {
            var board = BoardBuilder.Of("4|?1kA 0", "4|2", "4|").Lockbox("A", 1).Build();
            var move = Move(board, 0, 2);
            CollectionAssert.Contains(move.Revealed, (0, 0));
            CollectionAssert.Contains(move.LockboxesOpened, 0);
        }

        [Test]
        public void KeyInsideAClosedBoxDoesNotTrigger()
        {
            var board = BoardBuilder.Of("4|0", "4|1kA", "4|").Lockbox("A", 1).Build();
            Assert.IsTrue(board.IsLockboxActive(0));
        }

        [Test]
        public void ChainOpensInOneMoveAndUndoClosesEverything()
        {
            // Opening A exposes key B at the mouth of a bottle inside A, so B opens in the same move.
            var board = BoardBuilder.Of("4|1kA 0", "4|2 2kB", "4|3", "4|").Lockbox("A", 1).Lockbox("B", 2).Build();
            var move = Move(board, 0, 3);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, move.LockboxesOpened);
            Assert.IsTrue(board.IsAccessible(2));

            board.TryUndo(out _);
            Assert.IsTrue(board.IsLockboxActive(0) && board.IsLockboxActive(1));
            Assert.AreEqual("A", board.KeyAt(0, 0));
            Assert.AreEqual("B", board.KeyAt(1, 1));
        }

        [Test]
        public void ChainKeyUnderABallNeedsThatBallMoved()
        {
            var board = BoardBuilder.Of("4|1kA 0", "4|2kB 3", "4|4", "4|", "4|").Lockbox("A", 1).Lockbox("B", 2).Build();
            Move(board, 0, 3);
            Assert.IsTrue(board.IsLockboxActive(1));
            var move = Move(board, 1, 4);
            CollectionAssert.Contains(move.LockboxesOpened, 1);
        }

        [Test]
        public void PassiveBombLightsWhenItJoinsTheMouthGroupWithoutBurning()
        {
            var board = BoardBuilder.Of("4|0b3 1", "4|", "4|").Build();
            Assert.IsFalse(board.IsBombLit(0));
            var move = Move(board, 0, 1);
            CollectionAssert.Contains(move.BombsActivated, 0);
            Assert.AreEqual(3, board.BombFuse(0));

            Move(board, 1, 2);
            Assert.AreEqual(2, board.BombFuse(0), "every later transfer burns one, even between other bottles");
        }

        [Test]
        public void GroupSizeAndInvalidTapsAndExtraTubeDoNotChangeTheCost()
        {
            var board = BoardBuilder.Of("4|0b5!", "4|1 1 1", "4|", "4|2").Build();
            Assert.IsFalse(board.TryMove(1, 3, out _));
            board.AddBottle(2);
            Assert.AreEqual(5, board.BombFuse(0));
            Move(board, 1, 2);
            Assert.AreEqual(4, board.BombFuse(0));
        }

        [Test]
        public void FuseAtZeroFailsAndUndoTakesItBack()
        {
            var board = BoardBuilder.Of("4|1 0b1!", "4|", "4|2").Build();
            var move = Move(board, 0, 1);
            Assert.IsTrue(move.Exploded);
            Assert.IsTrue(board.IsFailed);
            Assert.IsFalse(board.CanSelectAsOrigin(2));

            Assert.IsTrue(board.TryUndo(out _));
            Assert.IsFalse(board.IsFailed);
            Assert.AreEqual(1, board.BombFuse(0));
        }

        [Test]
        public void ExplodedBoardCountsAsStuckSoTheFailCheckEndsTheTry()
        {
            var board = BoardBuilder.Of("4|1 0b1!", "4|", "4|2").Build();
            Assert.IsFalse(board.HasNoMove());
            Move(board, 0, 1);
            Assert.IsTrue(board.IsFailed);
            Assert.IsTrue(board.HasNoMove());
        }

        [Test]
        public void CompletingAnotherTubeOnTheLastAllowedMoveStillExplodes()
        {
            var board = BoardBuilder.Of("4|1 0b1!", "4|2 2 2", "4|2").Build();
            var move = Move(board, 2, 1);
            CollectionAssert.Contains(move.Completed, 1);
            Assert.IsTrue(move.Exploded);
            Assert.IsTrue(board.IsFailed);
            Assert.IsFalse(board.IsSolved());
        }

        [Test]
        public void DefusingOnTheLastAllowedMoveWins()
        {
            var board = BoardBuilder.Of("4|0 0 0b1!", "4|0").Build();
            var move = Move(board, 1, 0);
            CollectionAssert.Contains(move.BombsDefused, 0);
            Assert.IsFalse(board.IsFailed);
            Assert.IsTrue(board.IsSolved());
        }

        [Test]
        public void BombOpenAtTheMouthAtStartIsLitAndBlocksTheWinUntilDefused()
        {
            var board = BoardBuilder.Of("4|0 0 0 0", "4|1 1 1", "4|1b9").Build();
            Assert.IsTrue(board.IsBombLit(0));
            Move(board, 2, 1);
            Assert.IsTrue(board.IsBombDefused(0));
            Assert.IsTrue(board.IsSolved());
        }

        [Test]
        public void TwoSlotTubeNeverDefusesABomb()
        {
            var board = BoardBuilder.Of("4|1 0b4!", "2|0").Build();
            Move(board, 0, 1);
            Assert.IsFalse(board.IsBombDefused(0));
        }

        [Test]
        public void FreeMoveRelaxesOnlyTheTargetColor()
        {
            var board = BoardBuilder.Of("4|0 0 0", "4|1 1", "4|", "4|0", "4|2").Curtain(4, 3, 4).Build();
            Assert.IsTrue(board.CanFreeMove(0, 1));
            Assert.IsFalse(board.CanFreeMove(0, 2), "an empty target is a normal move");
            Assert.IsFalse(board.CanFreeMove(0, 3), "a same-color target is a normal move");
            Assert.IsFalse(board.CanFreeMove(0, 4), "a curtain still closes the bottle");

            Assert.IsTrue(board.TryFreeMove(0, 1, out var move));
            Assert.IsTrue(move.Free);
            Assert.AreEqual(2, move.Count, "capacity still limits the group");
        }

        [Test]
        public void ExtraTubeIsNeverATargetAndMustBeEmptyToWin()
        {
            var board = BoardBuilder.Of("4|0 0 0 0", "4|1 1 1", "4|1", "4|").Build();
            Move(board, 2, 3);
            var extra = board.AddBottle(2);
            Assert.IsFalse(board.CanUndo, "Undo history starts over after adding the tube");

            Move(board, 3, extra);
            Assert.IsFalse(board.IsCompleted(extra));
            Assert.IsFalse(board.IsSolved());
            Move(board, extra, 1);
            Assert.IsTrue(board.IsSolved());
        }
    }
}
