using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Application.Economy;
using Project.Contexts.LiveOps;
using Project.Core.Save;
using Project.Core.StateMachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    // The flow keeps working after a failing state, a damaged save or out-of-range race values.
    public sealed class HardeningTests
    {
        private enum Key { A, B, C }

        private sealed class Throwing : State
        {
            public override void Enter() => throw new InvalidOperationException("boom");
        }

        private sealed class Counting : State
        {
            public int Entered;
            public override void Enter() => Entered++;
        }

        private string _folder;

        [SetUp]
        public void SetUp() => _folder = Path.Combine(Path.GetTempPath(), "ballsort-hardening-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void StateThatThrowsDoesNotLockTheMachine()
        {
            var machine = new StateMachine<Key>();
            var counting = new Counting();
            machine.Add(Key.A, new Throwing()).Add(Key.B, counting);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            machine.ChangeState(Key.A);
            machine.ChangeState(Key.B);

            Assert.AreEqual(Key.B, machine.CurrentKey);
            Assert.AreEqual(1, counting.Entered);
        }

        [Test]
        public void OldSaveWithEmptyArraysMigratesWithoutThrowing()
        {
            var model = new ProgressModel
            {
                Version = 2,
                Level = 30,
                BoosterStock = new int[0],
                OpeningGranted = null,
                Tutorial = new TutorialModel { Boosters = new bool[0] }
            };

            Assert.DoesNotThrow(() => PlayerData.Migrate(model));
            Assert.AreEqual(CampaignRules.BoosterCount, model.BoosterStock.Length);
            Assert.AreEqual(CampaignRules.BoosterCount, model.OpeningGranted.Length);
            Assert.AreEqual(CampaignRules.BoosterCount, model.Tutorial.Boosters.Length);
            // A version 2 player at level 30 has passed every booster unlock.
            foreach (var type in CampaignRules.AllBoosters)
                if (CampaignRules.UnlockLevel(type) < 30) Assert.IsTrue(model.Tutorial.Boosters[(int)type]);
        }

        [Test]
        public void DamagedRaceValuesAreClampedBeforeUse()
        {
            var data = new PlayerData(new SaveService(_folder));
            var wallet = new WalletService(data);
            var now = new DateTime(2026, 11, 22, 10, 0, 0);
            var rush = new OrbitRushService(data, wallet, () => now);
            data.Model.Level = OrbitRushService.ActivationLevel;
            rush.MarkIntroShown();
            rush.Join();

            data.Model.OrbitRush.Gear = 99;
            data.Model.OrbitRush.Stage = -4;
            data.Model.OrbitRush.WonRank = 12;

            Assert.DoesNotThrow(() => _ = rush.Multiplier);
            Assert.AreEqual(OrbitRushService.GearMultipliers[^1], rush.Multiplier);
            Assert.AreEqual(0, rush.Stage);
            Assert.LessOrEqual(rush.WonRank, OrbitRushService.Winners);
        }
    }
}
