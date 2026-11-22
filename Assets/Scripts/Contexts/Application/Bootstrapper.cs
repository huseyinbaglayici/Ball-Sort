using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game;
using Project.Contexts.LiveOps;
using Project.Contexts.Main;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Pooling;
using Project.Core.Save;
using Project.Core.Services;
using Project.Core.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Contexts.Application
{
    // Composition root: registers every service first; Main is never unloaded, so they live all session.
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private AudioManager _audio;
        [SerializeField] private LoadingOverlay _loadingOverlay;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _poolRoot;

        [Header("Data")]
        [SerializeField] private List<LocalizationTable> _localizationTables = new();
        [SerializeField] private int _targetFrameRate = 60;

        private void Awake()
        {
            UnityEngine.Application.targetFrameRate = _targetFrameRate;

            var save = new SaveService();
            _audio.Initialize();
            var haptics = new HapticModule();
            var localization = new LocalizationService(_localizationTables);

            Services.Register(save);
            Services.Register(_audio);
            Services.Register(haptics);
            Services.Register(localization);
            Services.Register(new SettingsService(save, _audio, haptics, localization));
            Services.Register(new PoolService(_poolRoot));
            Services.Register(new PopupService(_popupRoot, _audio));

            // Campaign meta and economy share one save file so a win is written in one go.
            var playerData = new PlayerData(save);
            var wallet = new WalletService(playerData);
            var boosters = new BoosterInventory(playerData, wallet);
            var lives = new LivesService(playerData);
            Services.Register(playerData);
            Services.Register(wallet);
            Services.Register(boosters);
            Services.Register(lives);
            Services.Register(new MonetizationService(playerData, lives));
            Services.Register(new ProgressService(playerData, wallet, boosters, lives));
            Services.Register(new ContinueService(playerData, wallet));
            Services.Register(new DailyRewardService(playerData, wallet));
            var orbitRush = new OrbitRushService(playerData, wallet);
            Services.Register(orbitRush);
            // Every campaign win is one step on the event path.
            EventBus<LevelCompleted>.Subscribe(_ => orbitRush.RecordWin());
            Services.Register(_loadingOverlay);

            RegisterDebugActions();
        }

        private void Start()
        {
            var first = Services.Get<ProgressService>().Level == 1 ? MainState.Game : MainState.Home;
            Services.Get<MainFlow>().Begin(first);
        }

        private void OnDestroy() => Services.Clear();

        private static void RegisterDebugActions()
        {
            DebugPanel.AddAction("Go Home", () => _ = Services.Get<MainFlow>().GoHomeAsync());
            DebugPanel.AddAction("Add 100 coins", () => Services.Get<WalletService>().Add(100));
            DebugPanel.AddAction("Spend a life", () => Services.Get<LivesService>().TrySpend());
            DebugPanel.AddAction("Unlimited lives 30 min", () => Services.Get<LivesService>().GrantUnlimited(30));
            DebugPanel.AddAction("Toggle rewarded ad failure", () =>
            {
                var ads = Services.Get<MonetizationService>();
                ads.SimulateRewardedFailure = !ads.SimulateRewardedFailure;
                Debug.Log($"[Ads] Rewarded failure: {ads.SimulateRewardedFailure}");
            });
            DebugPanel.AddAction("Grant No Ads", () => Services.Get<MonetizationService>().GrantNoAds());
            DebugPanel.AddAction("Toggle ad group A/B", () =>
            {
                var ads = Services.Get<MonetizationService>();
                ads.SetGroup(ads.Group.Name == "A" ? 1 : 0);
                Debug.Log($"[Ads] Interstitial group {ads.Group.Name}: from L{ads.Group.StartLevel}, " +
                          $"{ads.Group.Points} wins and {ads.Group.ActiveSeconds} s between ads.");
            });
            DebugPanel.AddAction("Orbit Rush +1 win", () => Services.Get<OrbitRushService>().RecordWin());
            DebugPanel.AddAction("Reset progress", () => Services.Get<ProgressService>().ResetProgress());
            DebugPanel.AddAction("Toggle language", () =>
            {
                var next = Services.Get<LocalizationService>().LanguageCode == "en" ? "tr" : "en";
                Services.Get<SettingsService>().SetLanguage(next);
            });
            DebugPanel.AddAction("Log loaded scenes", () =>
            {
                for (var i = 0; i < SceneManager.sceneCount; i++) Debug.Log($"[Scenes] {SceneManager.GetSceneAt(i).name}");
            });
        }
    }
}
