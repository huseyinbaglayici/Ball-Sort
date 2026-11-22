using System;
using PrimeTween;
using Project.Common.UI.PopUps;
using Project.Core.Audio;
using Project.Core.Events;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.LiveOps
{
    // Full-screen event map: the marker hops to the step reached, reached rewards wait to be claimed.
    public sealed class OrbitRushScreen : Popup
    {
        [Serializable]
        public struct Reward
        {
            public Button Button;
            public GameObject Ready;
            public GameObject Done;
            public TMP_Text Amount;
        }

        [SerializeField] private OrbitRushMapLayout _layout;
        [SerializeField] private RectTransform _marker;
        [SerializeField] private Reward[] _rewards = Array.Empty<Reward>();
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private Button _close;
        [SerializeField] private RectTransform _coinTarget;
        [SerializeField] private Sprite _coin;
        [SerializeField] private float _hopTime = 0.28f;

        private OrbitRushService _rush;
        private LocalizationService _localization;
        private int _shownStep = -1;
        private int _window = int.MinValue;
        private float _nextTick;
        private Sequence _hops;

        protected override void Awake()
        {
            base.Awake();
            _rush = Services.Get<OrbitRushService>();
            _localization = Services.Get<LocalizationService>();
            _close.onClick.AddListener(Close);
            for (var i = 0; i < _rewards.Length; i++)
            {
                var index = i;
                var rect = (RectTransform)_rewards[i].Button.transform;
                rect.anchorMin = rect.anchorMax = OrbitRushMapLayout.Anchor(_layout.Rewards[i]);
                rect.anchoredPosition = Vector2.zero;
                _rewards[i].Amount.text = OrbitRushService.Milestones[i].Coins.ToString();
                _rewards[i].Button.onClick.AddListener(() => Claim(index));
            }
        }

        private void OnEnable() => EventBus<OrbitRushChanged>.Subscribe(OnChanged);
        private void OnDisable() => EventBus<OrbitRushChanged>.Unsubscribe(OnChanged);
        private void OnChanged(OrbitRushChanged _) => Refresh();

        protected override void OnShow(object args)
        {
            // A new window starts the marker over at the start pad.
            if (_window != int.MinValue && _window != _rush.Window) _shownStep = 0;
            _window = _rush.Window;
            Refresh();
        }

        protected override void OnHidden() => _hops.Complete();

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.5f;
            if (_window != _rush.Window) OnShow(null);
            _timer.text = LiveOpsText.Countdown(_rush.TimeLeft, _localization);
        }

        private void Refresh()
        {
            var step = _rush.Progress;
            _progress.text = _localization.Format("rush.progress", step, OrbitRushService.Steps);
            _timer.text = LiveOpsText.Countdown(_rush.TimeLeft, _localization);
            for (var i = 0; i < _rewards.Length; i++)
            {
                var reward = _rewards[i];
                reward.Ready.SetActive(_rush.CanClaim(i));
                reward.Done.SetActive(_rush.IsClaimed(i));
                reward.Button.interactable = _rush.CanClaim(i);
            }
            MoveMarker(step);
        }

        // One hop per tile won since the screen last showed it.
        private void MoveMarker(int step)
        {
            _hops.Stop();
            if (_shownStep < 0 || _shownStep > step)
            {
                Place(step);
                _shownStep = step;
                return;
            }

            Place(_shownStep);
            _hops = Sequence.Create(useUnscaledTime: true);
            for (var s = _shownStep + 1; s <= step; s++)
            {
                var from = OrbitRushMapLayout.Anchor(_layout.Stops[s - 1]);
                var to = OrbitRushMapLayout.Anchor(_layout.Stops[s]);
                _hops.Chain(Tween.Custom(this, 0f, 1f, _hopTime, (screen, t) => screen.PlaceBetween(from, to, t), Ease.InOutSine));
            }
            _shownStep = step;
        }

        private void Place(int step) => PlaceBetween(Vector2.zero, OrbitRushMapLayout.Anchor(_layout.Stops[step]), 1f);

        // The marker arcs up between two tiles.
        private void PlaceBetween(Vector2 from, Vector2 to, float t)
        {
            _marker.anchorMin = _marker.anchorMax = Vector2.LerpUnclamped(from, to, t);
            _marker.anchoredPosition = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 70f);
        }

        private void Claim(int milestone)
        {
            if (_rush.Claim(milestone) <= 0) return;
            Services.Get<AudioManager>().PlayOneShotSound("coin_double");
            CoinBurst.Play((RectTransform)_rewards[milestone].Button.transform, _coinTarget, _coin);
        }
    }
}
