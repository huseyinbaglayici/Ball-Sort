using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Levels;
using Project.Core.Save;

namespace Project.Tests
{
    public sealed class TutorialTests
    {
        private string _folder;
        private PlayerData _data;
        private TutorialService _tutorial;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ballsort-tutorial-" + Guid.NewGuid().ToString("N"));
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // Reloads from disk, like an app restart.
        private void Build()
        {
            _data = new PlayerData(new SaveService(_folder));
            _tutorial = new TutorialService(_data);
        }

        [Test]
        public void MechanicIntroShowsOnceAcrossRestarts()
        {
            var board = Mechanic.Portal | Mechanic.HiddenBall;
            var intro = _tutorial.Pick(7, board, Mechanic.Portal);
            Assert.AreEqual(Mechanic.HiddenBall, intro.Mechanic);

            _tutorial.MarkShown(intro);
            Assert.AreEqual(Mechanic.None, _tutorial.Pick(7, board, Mechanic.Portal).Mechanic, "same level replayed");
            Build();
            Assert.IsTrue(_tutorial.IsIntroduced(Mechanic.HiddenBall));
            Assert.AreEqual(Mechanic.None, _tutorial.Pick(8, board, board).Mechanic);
        }

        [Test]
        public void PortalAloneNeedsNoIntro()
        {
            Assert.AreEqual(Mechanic.None, _tutorial.NextMechanic(Mechanic.Portal, Mechanic.None));
        }

        [Test]
        public void ObstaclesComeBeforeTheTallTube()
        {
            var board = Mechanic.Tower | Mechanic.Bomb | Mechanic.FrozenTube;
            Assert.AreEqual(Mechanic.FrozenTube, _tutorial.NextMechanic(board, Mechanic.None));
            _tutorial.MarkShown(Intro.Of(Mechanic.FrozenTube));
            Assert.AreEqual(Mechanic.Bomb, _tutorial.NextMechanic(board, Mechanic.None));
            _tutorial.MarkShown(Intro.Of(Mechanic.Bomb));
            Assert.AreEqual(Mechanic.Tower, _tutorial.NextMechanic(board, Mechanic.None));
        }

        [Test]
        public void MechanicMetOnEarlierLevelsIsNotIntroduced()
        {
            Assert.AreEqual(Mechanic.None, _tutorial.NextMechanic(Mechanic.HiddenBall | Mechanic.Tower, Mechanic.HiddenBall | Mechanic.Tower));
            Assert.AreEqual(Mechanic.Tower, _tutorial.NextMechanic(Mechanic.HiddenBall | Mechanic.Tower, Mechanic.HiddenBall));
        }

        [Test]
        public void BoardLessonLevelsTakeNoCard()
        {
            Assert.IsTrue(_tutorial.Pick(TutorialService.TapLevel, Mechanic.Portal, Mechanic.None).IsNone);
            Assert.IsTrue(_tutorial.Pick(TutorialService.SameColorLevel, Mechanic.Portal, Mechanic.Portal).IsNone);
        }

        [Test]
        public void BoosterIntroWaitsForAFreeLevelAndShowsOnce()
        {
            var first = _tutorial.Pick(3, Mechanic.Portal, Mechanic.Portal);
            Assert.IsTrue(first.IsBooster);
            Assert.AreEqual(BoosterType.Undo, first.Booster);
            _tutorial.MarkShown(first);
            Assert.IsTrue(_tutorial.Pick(4, Mechanic.Portal, Mechanic.Portal).IsNone, "Extra Tube is still locked");

            // Extra Tube unlocks on the hidden-ball level; the item card goes first.
            var onUnlock = _tutorial.Pick(CampaignRules.UnlockLevel(BoosterType.ExtraTube), Mechanic.HiddenBall, Mechanic.Portal);
            Assert.AreEqual(Mechanic.HiddenBall, onUnlock.Mechanic);
            _tutorial.MarkShown(onUnlock);
            var next = _tutorial.Pick(8, Mechanic.HiddenBall, Mechanic.HiddenBall);
            Assert.AreEqual(BoosterType.ExtraTube, next.Booster);
            _tutorial.MarkShown(next);

            Build();
            Assert.IsTrue(_tutorial.IsIntroduced(BoosterType.Undo));
            Assert.IsTrue(_tutorial.IsIntroduced(BoosterType.ExtraTube));
            Assert.IsFalse(_tutorial.IsIntroduced(BoosterType.FreeMove));
            Assert.IsTrue(_tutorial.Pick(9, Mechanic.Portal, Mechanic.Portal).IsNone);
        }

        [Test]
        public void NoCardsOutsideTheCampaign()
        {
            Assert.IsTrue(_tutorial.Pick(0, Mechanic.Bomb, Mechanic.None).IsNone);
            Assert.IsTrue(_tutorial.Pick(CampaignRules.MaxLevel + 1, Mechanic.Bomb, Mechanic.None).IsNone);
        }

        [Test]
        public void VersionTwoSaveSkipsBoostersAlreadyPassed()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, PlayerData.Key + ".json"), "{\"Version\":2,\"Level\":12,\"Coins\":75}");
            Build();

            Assert.AreEqual(ProgressModel.CurrentVersion, _data.Model.Version);
            Assert.IsNotNull(_data.Model.Tutorial);
            Assert.IsTrue(_tutorial.IsIntroduced(BoosterType.Undo));
            Assert.IsTrue(_tutorial.IsIntroduced(BoosterType.ExtraTube));
            Assert.IsFalse(_tutorial.IsIntroduced(BoosterType.FreeMove));
            Assert.AreEqual(0, _data.Model.Tutorial.Mechanics);
        }

        [Test]
        public void NewSaveStartsWithNothingShown()
        {
            Assert.AreEqual(ProgressModel.CurrentVersion, _data.Model.Version);
            foreach (var type in CampaignRules.AllBoosters) Assert.IsFalse(_tutorial.IsIntroduced(type));
            foreach (var mechanic in TutorialService.IntroOrder) Assert.IsFalse(_tutorial.IsIntroduced(mechanic));
        }
    }
}
