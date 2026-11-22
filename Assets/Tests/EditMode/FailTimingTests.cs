using NUnit.Framework;
using Project.Contexts.Game;
using Project.Contexts.Game.Sort;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    // Fail panel readable window and the shutter's opening budget.
    public sealed class FailTimingTests
    {
        private const string ConfigPath = "Assets/Configs/BallAnimationConfig.asset";

        private static BallAnimationConfig Config => AssetDatabase.LoadAssetAtPath<BallAnimationConfig>(ConfigPath);

        [Test]
        public void PanelWaitsForBothTheWindowAndTheSearch()
        {
            var window = new FailWindow(tryId: 3, version: 7, readableLeft: 1f);
            window.SearchDone();
            window.Tick(0.6f);
            Assert.IsFalse(window.TryOpen(3, 7), "fast search still waits for the readable second");
            window.Tick(0.4f);
            Assert.IsTrue(window.TryOpen(3, 7));
            Assert.IsFalse(window.TryOpen(3, 7), "opens once; a refused payment reshows without a second delay");

            var slow = new FailWindow(3, 7, 1f);
            slow.Tick(2f);
            Assert.IsFalse(slow.TryOpen(3, 7), "the readable second passed but the search is still running");
            slow.SearchDone();
            Assert.IsTrue(slow.TryOpen(3, 7));
        }

        [Test]
        public void StaleTryOrBoardNeverOpens()
        {
            var window = new FailWindow(3, 7, 0f);
            window.SearchDone();
            Assert.IsFalse(window.TryOpen(4, 7), "restarted try");
            Assert.IsFalse(window.TryOpen(3, 8), "board changed (Undo, Continue)");
        }

        [Test]
        public void ResumeFrameDoesNotCount()
        {
            var window = new FailWindow(1, 1, 1f);
            window.SearchDone();
            window.Tick(0f);
            window.Tick(0.5f);
            Assert.AreEqual(0.5f, window.Left, 1e-5f);
            Assert.IsFalse(window.TryOpen(1, 1));
        }

        [Test]
        public void ReadableMomentIsTheLastMeaningfulChange()
        {
            var config = Config;
            var plain = new MoveResult();
            Assert.AreEqual(10f, FailWindow.ReadableAt(10f, 9f, plain, config), 1e-5f, "plain move: the last landing");

            var hidden = new MoveResult();
            hidden.Revealed.Add((0, 0));
            Assert.AreEqual(Mathf.Max(10f, 9f + config.HiddenReadableTime), FailWindow.ReadableAt(10f, 9f, hidden, config), 1e-5f);

            var blast = new MoveResult { Exploded = true };
            Assert.AreEqual(10f + config.ExplodeReadableTime, FailWindow.ReadableAt(10f, 9f, blast, config), 1e-5f);

            var box = new MoveResult();
            box.LockboxesOpened.Add(0);
            Assert.AreEqual(10f + config.ShutterClearTime, FailWindow.ReadableAt(10f, 9f, box, config), 1e-5f);
        }

        [Test]
        public void ShutterOpensWithinTheApprovedBudget()
        {
            var config = Config;
            Assert.AreEqual(0.35f, config.ShutterClearTime, 1e-4f, "tubes usable 0.35 s after the key");
            Assert.LessOrEqual(config.ShutterShakeDuration + config.ShutterRollDelay, 0.1f + 1e-4f, "door moves within 0.10 s");
            Assert.LessOrEqual(config.LockDropDuration, config.ShutterRollDuration, "the padlock drop overlaps the roll");
            Assert.AreEqual(0.1f, config.ShutterFadeDuration, 1e-4f);
            Assert.AreEqual(1.5f, config.FailReadableWindow, 1e-4f);
        }
    }
}
