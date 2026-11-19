using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Contexts.Game.Sort;
using Project.Core.Audio;
using UnityEngine;

namespace Project.Tests
{
    public sealed class AudioContactTests
    {
        private const string CatalogPath = "Assets/Art/Audio/AudioEvents.json";

        [Serializable]
        private sealed class Catalog
        {
            public List<CatalogEvent> events = new();
        }

        [Serializable]
        private sealed class CatalogEvent
        {
            public string id;
            public List<string> files = new();
        }

        private static string ProjectPath(string assetPath) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);

        private static Dictionary<string, List<string>> LoadCatalog() =>
            JsonUtility.FromJson<Catalog>(File.ReadAllText(ProjectPath(CatalogPath)))
                .events.ToDictionary(e => e.id, e => e.files);

        [Test]
        public void BasicContactsCoverSelectReturnAndPartialTransfer()
        {
            for (var n = 1; n <= OrbContacts.MaxBalls; n++)
            {
                foreach (var kind in new[] { ContactKind.Select, ContactKind.Return })
                {
                    var contacts = OrbContacts.Basic(kind, n);
                    Assert.AreEqual(n, contacts.Count);
                    Assert.AreEqual(1, contacts.Count(c => c.Haptic));
                    if (kind == ContactKind.Return)
                    {
                        Assert.IsTrue(contacts[^1].Haptic);
                        Assert.AreEqual("land_last", contacts[^1].Sound);
                    }
                }
                for (var m = 1; m <= n; m++)
                {
                    var contacts = OrbContacts.Basic(ContactKind.Transfer, n, m);
                    Assert.AreEqual(m, contacts.Count);
                    Assert.IsTrue(contacts.All(c => c.Haptic));
                }
            }
        }

        [Test]
        public void PlannedSoundsExistAndFollowTheMove()
        {
            var catalog = LoadCatalog();
            for (var n = 1; n <= OrbContacts.MaxBalls; n++)
            {
                var back = OrbContacts.Plan(ContactKind.Return, n);
                Assert.AreEqual(1, back.Count(c => c.Haptic));
                Assert.AreEqual("land_last", back[^1].Sound);
                Assert.AreEqual(1, OrbContacts.Plan(ContactKind.Select, n).Count(c => c.Haptic));
                for (var m = 1; m <= n; m++)
                foreach (var empty in new[] { true, false })
                foreach (var tower in new[] { true, false })
                {
                    var c = OrbContacts.Plan(ContactKind.Transfer, n, m, empty, tower);
                    Assert.AreEqual(m, c.Count);
                    Assert.AreEqual(m, c.Count(x => x.Haptic));
                    Assert.IsTrue(c.All(x => catalog.ContainsKey(x.Sound)), $"unknown sound in {n}/{m}");
                    if (m < n) Assert.AreEqual("land_partial", c[^1].Sound);
                }
            }
        }

        [Test]
        public void LongRunsDuckEveryFourAndCloseAtFullLevel()
        {
            Assert.IsTrue(OrbContacts.Plan(ContactKind.Transfer, 4, 4).All(c => c.GainDb == 0f));
            var run = OrbContacts.Plan(ContactKind.Transfer, 9, 9);
            for (var i = 0; i < run.Count - 1; i++)
                Assert.AreEqual(i % 4 == 0 ? OrbContacts.FirstOfFourDb : OrbContacts.OtherDb, run[i].GainDb, $"contact {i}");
            Assert.AreEqual(0f, run[^1].GainDb);
        }

        [Test]
        public void BagNeverRepeatsWithinARoundOrBackToBack()
        {
            var catalog = LoadCatalog();
            foreach (var (id, files) in catalog)
            {
                var bag = new AudioBag<string>(files, n => (int)Math.Floor(0.42 * n));
                string previous = null;
                for (var round = 0; round < 3; round++)
                {
                    var used = new HashSet<string>();
                    for (var i = 0; i < files.Count; i++)
                    {
                        var current = bag.Next();
                        Assert.IsTrue(used.Add(current), $"{id}: {current} repeated within a round");
                        if (files.Count > 1) Assert.AreNotEqual(previous, current, $"{id}: back-to-back repeat");
                        previous = current;
                        Assert.IsTrue(File.Exists(ProjectPath(current)), $"{id}: {current} missing");
                    }
                }
            }
        }
    }
}
