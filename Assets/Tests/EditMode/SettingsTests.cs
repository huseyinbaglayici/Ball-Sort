using System;
using System.IO;
using NUnit.Framework;
using Project.Contexts.Game.Levels;
using Project.Contexts.Home;
using Project.Core.Audio;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Save;
using Project.Core.Settings;
using AssetDatabase = UnityEditor.AssetDatabase;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    public sealed class SettingsTests
    {
        private string _folder;
        private GameObject _audioHost;
        private LocalizationTable[] _tables;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ballsort-settings-" + Guid.NewGuid().ToString("N"));
            _audioHost = new GameObject("Audio");
            _tables = new[]
            {
                AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Configs/Localization_en.asset"),
                AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Configs/Localization_tr.asset")
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_audioHost);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // A fresh service set on the same save folder, like a relaunch.
        private (SettingsService Settings, LocalizationService Localization, AudioManager Audio) Launch()
        {
            var audio = _audioHost.GetComponent<AudioManager>() ?? _audioHost.AddComponent<AudioManager>();
            var localization = new LocalizationService(_tables);
            return (new SettingsService(new SaveService(_folder), audio, new HapticModule(), localization), localization, audio);
        }

        [Test]
        public void Tables_AreListedByTheirOwnNames()
        {
            var (_, localization, _) = Launch();
            CollectionAssert.AreEqual(new[] { "en", "tr" }, localization.Languages);
            Assert.AreEqual("English", localization.NativeName("en"));
            Assert.AreEqual("Türkçe", localization.NativeName("tr"));
        }

        [Test]
        public void LanguageChoice_SurvivesRelaunch_AndKeepsAudioPreferences()
        {
            var first = Launch();
            first.Settings.SetMusicMuted(true);
            var other = first.Localization.LanguageCode == "tr" ? "en" : "tr";
            first.Settings.SetLanguage(other);

            var second = Launch();
            Assert.AreEqual(other, second.Localization.LanguageCode);
            Assert.IsTrue(second.Settings.MusicMuted);
            Assert.IsFalse(second.Settings.SoundMuted);
        }

        [Test]
        public void NoSavedChoice_KeepsTheDeviceLanguage()
        {
            var device = new LocalizationService(_tables).LanguageCode;
            Assert.AreEqual(device, Launch().Localization.LanguageCode);
        }

        [Test]
        public void EmptyLinks_HideFeedbackAndLegalLinks()
        {
            var links = ScriptableObject.CreateInstance<AppLinks>();
            try
            {
                var rows = SettingsRows.For(2, links);
                Assert.IsTrue(rows.Language);
                Assert.IsFalse(rows.Feedback);
                Assert.IsFalse(rows.Privacy);
                Assert.IsFalse(rows.Terms);
                Assert.AreEqual(2, rows.Count(copyAddress: true));

                links.Set(" help@example.com ", "https://example.com/privacy", "");
                rows = SettingsRows.For(1, links);
                Assert.IsFalse(rows.Language);
                Assert.IsTrue(rows.Feedback);
                Assert.IsTrue(rows.Privacy);
                Assert.IsFalse(rows.Terms);
                Assert.AreEqual(2, rows.Count(copyAddress: false));
                Assert.AreEqual(3, rows.Count(copyAddress: true));
                Assert.AreEqual("help@example.com", links.SupportEmail);
            }
            finally
            {
                Object.DestroyImmediate(links);
            }
        }

        [Test]
        public void FeedbackMail_EscapesSubjectAndBody()
        {
            Assert.AreEqual("mailto:a@b.c?subject=Orbit%20Sort&body=1.0%20%2812%29",
                SettingsRows.FeedbackMail("a@b.c", "Orbit Sort", "1.0 (12)"));
        }

        [Test]
        public void SeenMechanics_ComeFromLevelsPlayed()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Configs/LevelCatalog.asset");
            Assert.AreEqual(Mechanic.None, LevelMechanics.Seen(catalog, 0));
            Assert.AreEqual(Mechanic.Portal, LevelMechanics.Seen(catalog, 1));
            var all = LevelMechanics.Seen(catalog, catalog.Count);
            Assert.AreNotEqual(Mechanic.Portal, all);
            Assert.AreEqual(0, HelpPage.PlayedCount(1, 0));
            Assert.AreEqual(1, HelpPage.PlayedCount(1, 2));
            Assert.AreEqual(4, HelpPage.PlayedCount(5, 0));
        }
    }
}
