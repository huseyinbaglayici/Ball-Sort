using System.Collections.Generic;
using NUnit.Framework;
using Project.Contexts.Game.Sort;

namespace Project.Tests
{
    public sealed class RescueTests
    {
        private static void Move(SortBoard board, int from, int to) =>
            Assert.IsTrue(board.TryMove(from, to, out _), $"move {from}->{to} should be legal");

        private static void Replay(SortBoard board, List<(int From, int To)> route)
        {
            foreach (var (from, to) in route) Move(board, from, to);
        }

        [Test]
        public void TemporaryTubeNeverCompletesOrLiftsACurtain()
        {
            var board = BoardBuilder.Of("4|0 0", "4|0 0", "4|3").Curtain(2, 0, 4).Build();
            var tube = board.AddBottle(4);
            Assert.IsTrue(board.IsTemporary(tube));
            Move(board, 0, tube);
            Move(board, 1, tube);
            Assert.IsFalse(board.IsCompleted(tube));
            Assert.IsTrue(board.IsCurtainActive(0));
            Assert.IsFalse(board.IsSolved());
        }

        [Test]
        public void TemporaryTubeNeverDefusesABomb()
        {
            var board = BoardBuilder.Of("4|0 0 0b5!", "4|0").Build();
            var tube = board.AddBottle(4);
            Move(board, 0, tube);
            Move(board, 1, tube);
            Assert.IsFalse(board.IsBombDefused(0));
        }

        private static SortBoard Stuck() => BoardBuilder.Of("4|0 0 1 1", "4|1 1 0 0").Build();

        [Test]
        public void NoMoveContinueAddsAFourSlotTubeWithAVerifiedWin()
        {
            var board = Stuck();
            Assert.IsFalse(board.HasLegalMove());

            var report = RescueAdvisor.Analyze(board);
            Assert.AreEqual(RescueKind.NoMove, report.Kind);
            Assert.IsNotNull(report.Offer);
            Assert.IsFalse(report.Offer.Rewind);

            var tube = board.ApplyContinue(report.Offer);
            Assert.AreEqual(2, tube);
            Assert.IsTrue(board.IsTemporary(tube));
            Assert.AreEqual(4, board.Capacity(tube));
            Assert.IsTrue(board.ContinueUsed);
            Assert.IsFalse(board.CanUndo, "Undo history starts from the rescued board");

            Replay(board, report.Offer.Route);
            Assert.IsTrue(board.IsSolved());
            Assert.IsTrue(board.IsEmpty(tube));
        }

        [Test]
        public void ContinueIsOncePerTryAndAStaleOfferIsRefused()
        {
            var board = BoardBuilder.Of("4|0 0 1 1", "4|1 1 0 0", "4|2 2 2", "4|2").Build();
            var offer = RescueAdvisor.Analyze(board).Offer;
            Assert.IsNotNull(offer);
            Move(board, 2, 3);
            Assert.AreEqual(-1, board.ApplyContinue(offer), "the board changed since the offer");

            var fresh = RescueAdvisor.Analyze(board).Offer;
            Assert.GreaterOrEqual(board.ApplyContinue(fresh), 0);
            Assert.AreEqual(-1, board.ApplyContinue(fresh));
            Assert.IsNull(RescueAdvisor.Analyze(board).Offer, "no second Continue in the same try");
        }

        [Test]
        public void AnalysisNeverChangesTheBoard()
        {
            var board = Stuck();
            var version = board.Version;
            RescueAdvisor.Analyze(board);
            Assert.AreEqual(version, board.Version);
            Assert.AreEqual(2, board.BottleCount);
        }

        [Test]
        public void TimeoutTurnsTheOfferOff()
        {
            var report = RescueAdvisor.Analyze(Stuck(), timeoutMs: 0);
            Assert.IsTrue(report.TimedOut);
            Assert.IsNull(report.Offer);
        }

        [Test]
        public void CancellingTurnsTheOfferOff()
        {
            using var cancel = new System.Threading.CancellationTokenSource();
            cancel.Cancel();
            var report = RescueAdvisor.Analyze(Stuck(), cancel: cancel.Token);
            Assert.IsNull(report.Offer);
        }

        [Test]
        public void UndoOnAnExplodedBoardGivesTheRewoundBoardAndBombColorIsTheCarrier()
        {
            var board = BoardBuilder.Of("4|0 0 0b1!", "4|0", "4|1 1 1", "4|1", "4|").Build();
            Assert.AreEqual(0, board.BombColor(0));
            Move(board, 3, 4);
            Assert.IsTrue(board.IsFailed);
            Assert.IsTrue(board.TryUndo(out _));
            Assert.IsFalse(board.IsFailed);
            Assert.AreEqual(1, board.BombFuse(0));
            Assert.AreEqual(1, board.Count(3));
        }

        [Test]
        public void BombContinueRewindsAndGivesTheRouteItsFusePlusOne()
        {
            // Defusing needs three transfers (2 to the 2s, then both 0 groups to the bomb), but only 1 is left.
            var board = BoardBuilder.Of("4|0b1!", "4|0 2", "4|0 0", "4|2 2 2", "4|").Build();
            Move(board, 2, 4);
            Assert.IsTrue(board.IsFailed);

            var report = RescueAdvisor.Analyze(board);
            Assert.AreEqual(RescueKind.Bomb, report.Kind);
            Assert.IsFalse(report.UndoWins, "one transfer back still cannot defuse in time");
            Assert.IsNotNull(report.Offer);
            Assert.IsTrue(report.Offer.Rewind);
            Assert.AreEqual(3, report.Offer.FuseBonus[0], "route defuses on its 3rd transfer: 3 + 1 spare = 4, so +3");

            var tube = board.ApplyContinue(report.Offer);
            Assert.IsFalse(board.IsFailed);
            Assert.AreEqual(4, board.BombFuse(0));
            Assert.AreEqual(0, board.MoveCount, "the exploding transfer is rewound");
            Assert.AreEqual(2, board.Count(2), "balls are back where they were");
            Assert.IsTrue(board.IsTemporary(tube));
            Replay(board, report.Offer.Route);
            Assert.IsTrue(board.IsSolved());
        }

        [Test]
        public void UndoIsOfferedOnlyWhenTheRewoundBoardWinsAsItIs()
        {
            var board = BoardBuilder.Of("4|0 0 0b1!", "4|0", "4|1 1 1", "4|1", "4|").Build();
            Move(board, 3, 4);
            Assert.IsTrue(board.IsFailed);
            var report = RescueAdvisor.Analyze(board);
            Assert.IsTrue(report.UndoWins);
            Assert.IsNotNull(report.Offer);
            Assert.GreaterOrEqual(report.Offer.FuseBonus[0], 1, "an exploded fuse always gets at least one more");
        }

        [Test]
        public void RewindRestoresHiddenBallsBoxesAndKeys()
        {
            // The exploding transfer also opened a hidden key and its box.
            var board = BoardBuilder.Of("4|?1kA 0", "4|2", "4|", "4|3b1!", "4|3 3 3").Lockbox("A", 1).Build();
            Move(board, 0, 2);
            Assert.IsTrue(board.IsFailed);
            Assert.IsFalse(board.IsLockboxActive(0));

            var offer = new ContinueOffer { BoardVersion = board.Version, Rewind = true, FuseBonus = new[] { 2 } };
            Assert.GreaterOrEqual(board.ApplyContinue(offer), 0);
            Assert.IsFalse(board.IsFailed);
            Assert.AreEqual(3, board.BombFuse(0), "rewound fuse 1 plus the bonus");
            Assert.IsTrue(board.IsLockboxActive(0));
            Assert.AreEqual("A", board.KeyAt(0, 0));
            Assert.IsFalse(board.IsKnown(0, 0));
        }

        private static List<string> Landed(MoveResult move, SortBoard board, bool won = false)
        {
            var events = new List<string>();
            MechanicSounds.OnLanded(move, board, won, events);
            return events;
        }

        [Test]
        public void ExplosionSoundsAloneAndWinLeavesTheOutcomeToTheFlow()
        {
            var board = BoardBuilder.Of("4|1 0b1!", "4|", "4|2").Build();
            board.TryMove(0, 1, out var move);
            CollectionAssert.AreEqual(new[] { MechanicSounds.BombBurst }, Landed(move, board));

            board = BoardBuilder.Of("4|0 0 0", "4|0").Build();
            board.TryMove(1, 0, out move);
            CollectionAssert.IsEmpty(Landed(move, board, won: true));
        }

        [Test]
        public void DefuseOutranksTheCapAndBoxOutranksCurtain()
        {
            var board = BoardBuilder.Of("4|0 0 0b3!", "4|0", "4|1").Build();
            board.TryMove(1, 0, out var move);
            CollectionAssert.AreEqual(new[] { MechanicSounds.BombSafe }, Landed(move, board));

            board = BoardBuilder.Of("8|0 0 0 0 0 0 0", "4|1kA 0", "4|2", "4|3").Lockbox("A", 2).Curtain(3, 0, 8).Build();
            board.TryMove(1, 0, out move);
            CollectionAssert.AreEqual(new[] { MechanicSounds.Unlock }, Landed(move, board));
        }

        [Test]
        public void CompletedTubePlaysItsCapacityCapAndRevealPlaysOnMove()
        {
            var board = BoardBuilder.Of("8|0 0 0 0 0 0 0", "4|?1 0", "4|1 1 1").Build();
            board.TryMove(1, 0, out var move);
            Assert.AreEqual(MechanicSounds.Hidden, MechanicSounds.OnMove(move));
            CollectionAssert.AreEqual(new[] { "tube_8" }, Landed(move, board));
        }

        [Test]
        public void LowFuseWarns()
        {
            var board = BoardBuilder.Of("4|0b3!", "4|1", "4|").Build();
            board.TryMove(1, 2, out var move);
            CollectionAssert.AreEqual(new[] { MechanicSounds.BombLow }, Landed(move, board));
        }
    }
}
