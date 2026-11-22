using System;
using PrimeTween;
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
    // Home's way into the live ops: buttons, badges, the event icon's timer and count, and what opens on its own on arrival.
    public sealed class LiveOpsHome : MonoBehaviour
    {
        [SerializeField] private Button _daily;
        [SerializeField] private GameObject _dailyBadge;
        // The first is the event side button (hidden while locked); the rest are bar tabs.
        [SerializeField] private Button[] _event = Array.Empty<Button>();
        [SerializeField] private GameObject[] _eventBadges = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text _eventTimer;
        // The side button's name label gives way to the race count while racing.
        [SerializeField] private GameObject _eventLabel;
        [SerializeField] private GameObject _eventCount;
        [SerializeField] private TMP_Text _eventCountText;
        [SerializeField] private Button _play;
        [SerializeField] private DailyRewardPopup _dailyPopup;
        [SerializeField] private OrbitRushScreen _orbitRush;
        [SerializeField] private MessagePopup _messagePopup;
        // Won points fly from the Play button into the event icon.
        [SerializeField] private Sprite _collectable;
        // Lets the splash fade or the level close finish first.
        [SerializeField] private float _autoShowDelay = 0.6f;

        private DailyRewardService _dailyRewards;
        private OrbitRushService _rush;
        private LocalizationService _localization;
        private PopupService _popups;
        private float _nextTick;
        private bool _wired;
        private bool _collecting;

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
            _ = ArriveAsync();
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
            var state = _rush.State;
            if (_event.Length > 0 && _event[0] != null) _event[0].gameObject.SetActive(_rush.IsVisible);
            var notice = _rush.HasClaimable || state == OrbitRushState.StageLost;
            foreach (var badge in _eventBadges)
                if (badge != null) badge.SetActive(notice);
            if (_eventTimer != null) _eventTimer.text = LiveOpsText.Countdown(_rush.TimeLeft, _localization);

            var racing = state is OrbitRushState.Racing or OrbitRushState.StageWon or OrbitRushState.StageLost;
            if (_eventLabel != null) _eventLabel.SetActive(!racing);
            if (_eventCount != null) _eventCount.SetActive(racing);
            // While points fly in, the count waits for them.
            if (racing && _eventCountText != null && !_collecting)
                _eventCountText.text = $"{_rush.Score - _rush.PendingCollect}/{_rush.Target}";
        }

        // In order: the ended event's result, points won since the last visit, the event intro, the daily reward.
        private async Awaitable ArriveAsync()
        {
            await Awaitable.WaitForSecondsAsync(_autoShowDelay);
            if (this == null || !isActiveAndEnabled) return;
            if (Services.TryGet<MainFlow>(out var flow) && (flow.Current != MainState.Home || flow.IsSwitching)) return;

            if (_rush.TryTakeFinish(out var stages, out var coins))
                _popups.Show(_messagePopup, new MessageArgs("rush.finish_title",
                    _localization.Format("rush.finish_body", stages, OrbitRushService.Stages, coins), "common.ok", closable: true));
            if (_rush.PendingCollect > 0) PlayCollect();
            if (_rush.State == OrbitRushState.Intro) ShowOrbitRush();
            if (_dailyRewards.ShouldAutoShow)
            {
                _dailyRewards.MarkAutoShown();
                ShowDaily();
            }
        }

        // The race points fly into the event icon, then its count ticks up.
        private void PlayCollect()
        {
            var icon = _event.Length > 0 && _event[0] != null ? (RectTransform)_event[0].transform : null;
            if (icon == null || _play == null || !icon.gameObject.activeInHierarchy)
            {
                _rush.MarkCollected();
                return;
            }
            _collecting = true;
            var from = _rush.Score - _rush.PendingCollect;
            var to = _rush.Score;
            var target = _rush.Target;
            CoinBurst.Play((RectTransform)_play.transform, icon, _collectable, Mathf.Clamp(to - from, 1, 6), 150f);
            Tween.Custom(this, from, to, 0.5f, (home, value) =>
                {
                    if (home._eventCountText != null) home._eventCountText.text = $"{Mathf.RoundToInt(value)}/{target}";
                }, startDelay: 0.65f, useUnscaledTime: true)
                .OnComplete(this, home =>
                {
                    home._collecting = false;
                    home._rush.MarkCollected();
                    home.Refresh();
                });
        }

        private void ShowDaily() => _popups.Show(_dailyPopup);

        private void ShowOrbitRush()
        {
            if (!_rush.IsVisible)
            {
                _popups.Show(_messagePopup, new MessageArgs("rush.title",
                    _localization.Format("rush.locked_body", OrbitRushService.ActivationLevel), "common.ok", closable: true));
                return;
            }
            _popups.Show(_orbitRush, new OrbitRushArgs(() => { if (_play != null) _play.onClick.Invoke(); }));
        }
    }
}
