using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Main;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Pooling;
using Project.Core.Save;
using Project.Core.SceneLoading;
using Project.Core.Services;
using Project.Core.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Contexts.Application
{
    /// <summary>
    /// Composition root in the Boot scene (build index 0). Creates every service in dependency
    /// order, registers it, then opens the first content scene. The Boot scene is never unloaded,
    /// so everything here (camera, EventSystem, popup canvas, audio) lives for the whole session.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private AudioService _audio;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _poolRoot;

        [Header("Data")]
        [SerializeField] private AudioLibrary _audioLibrary;
        [SerializeField] private List<LocalizationTable> _localizationTables = new();
        [SerializeField] private int _targetFrameRate = 60;

        private void Awake()
        {
            UnityEngine.Application.targetFrameRate = _targetFrameRate;

            var save = new SaveService();
            var settings = new SettingsService(save);
            _audio.Initialize(_audioLibrary, settings.Model.MusicMuted, settings.Model.SfxMuted);

            Services.Register(save);
            Services.Register(settings);
            Services.Register(_audio);
            Services.Register(new HapticService(settings.Model.HapticsEnabled));
            Services.Register(new LocalizationService(_localizationTables, settings.Model.Language));
            Services.Register(new PoolService(_poolRoot));
            Services.Register(new PopupService(_popupRoot));
            Services.Register(new SceneLoader());
            Services.Register(new ProgressService(save));
            Services.Register(_loadingOverlay);

            RegisterDebugActions();
        }

        private async void Start()
        {
            await _loadingOverlay.ShowAsync();
            await Services.Get<SceneLoader>().SwitchToAsync(SceneNames.Main, _loadingOverlay.SetProgress);

            // A new player (level 1) goes straight into the first level; everyone else lands on Home.
            var flow = Services.Get<MainFlow>();
            if (Services.Get<ProgressService>().Level == 1) await flow.GoToGameAsync();
            else await flow.GoHomeAsync();
        }

        private void OnDestroy() => Services.Clear();

        private static void RegisterDebugActions()
        {
            DebugPanel.AddAction("Go Home", () => _ = Services.Get<MainFlow>().GoHomeAsync());
            DebugPanel.AddAction("Add 100 coins", () => Services.Get<ProgressService>().AddCoins(100));
            DebugPanel.AddAction("Reset progress", () => Services.Get<ProgressService>().ResetProgress());
            DebugPanel.AddAction("Toggle language", () =>
            {
                var loc = Services.Get<LocalizationService>();
                var next = loc.LanguageCode == "en" ? "tr" : "en";
                loc.SetLanguage(next);
                var settings = Services.Get<SettingsService>();
                settings.Model.Language = next;
                settings.Save();
            });
            DebugPanel.AddAction("Log loaded scenes", () =>
            {
                for (var i = 0; i < SceneManager.sceneCount; i++) Debug.Log($"[Scenes] {SceneManager.GetSceneAt(i).name}");
            });
        }
    }
}
