using System;
using Project.Common.UI.PopUps;
using Project.Contexts.Main;
using Project.Core.Events;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.LiveOps
{
    // Home's way into the live ops: buttons, badges, the event timer pill and the daily auto-open.
    public sealed class LiveOpsHome : MonoBehaviour
    {
        [SerializeField] private Button _daily;
        [SerializeField] private GameObject _dailyBadge;
        // Event side button and the Orbit Rush tab both open the map.
        [SerializeField] private Button[] _event = Array.Empty<Button>();
        [SerializeField] private GameObject[] _eventBadges = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text _eventTimer;
        [SerializeField] private DailyRewardPopup _dailyPopup;
        [SerializeField] private OrbitRushScreen _orbitRush;
        // Lets the splash fade or the level close finish first.
        [SerializeField] private float _autoShowDelay = 0.6f;

        private DailyRewardService _dailyRewards;
        private OrbitRushService _rush;
        private LocalizationService _localization;
        private PopupService _popups;
        private float _nextTick;
        private bool _wired;

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            _dailyRewards = Services.Get<DailyRewardService>();
            _rush = Services.Get<OrbitRushService>();
            _localization = Services.Get<LocalizationService>();
            _popups = Services.Get<PopupService>();
            if (_daily != null) _daily.onClick.AddListener(ShowDaily);
            foreach (var button in _event)
                if (button != null) button.onClick.AddListener(ShowOrbitRush);
        }

        private void OnEnable()
        {
            Wire();
            EventBus<DailyRewardChanged>.Subscribe(OnDailyChanged);
            EventBus<OrbitRushChanged>.Subscribe(OnRushChanged);
            EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
            Refresh();
            _ = AutoShowAsync();
        }

        private void OnDisable()
        {
            EventBus<DailyRewardChanged>.Unsubscribe(OnDailyChanged);
            EventBus<OrbitRushChanged>.Unsubscribe(OnRushChanged);
            EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);
        }

        private void OnDailyChanged(DailyRewardChanged _) => Refresh();
        private void OnRushChanged(OrbitRushChanged _) => Refresh();
        private void OnLanguageChanged(LanguageChanged _) => Refresh();

        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            // A day or a window can roll over while Home is open.
            Refresh();
        }

        private void Refresh()
        {
            _nextTick = Time.unscaledTime + 1f;
            if (_dailyBadge != null) _dailyBadge.SetActive(_dailyRewards.CanClaim);
            var rushReady = _rush.HasClaimable;
            foreach (var badge in _eventBadges)
                if (badge != null) badge.SetActive(rushReady);
            if (_eventTimer != null) _eventTimer.text = LiveOpsText.Countdown(_rush.TimeLeft, _localization);
        }

        private async Awaitable AutoShowAsync()
        {
            await Awaitable.WaitForSecondsAsync(_autoShowDelay);
            if (this == null || !isActiveAndEnabled || !_dailyRewards.ShouldAutoShow) return;
            if (Services.TryGet<MainFlow>(out var flow) && flow.Current != MainState.Home) return;
            _dailyRewards.MarkAutoShown();
            ShowDaily();
        }

        private void ShowDaily() => _popups.Show(_dailyPopup);

        private void ShowOrbitRush() => _popups.Show(_orbitRush);
    }
}
