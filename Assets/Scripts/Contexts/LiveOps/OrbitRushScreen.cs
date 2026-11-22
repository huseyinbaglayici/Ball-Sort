using System;
using PrimeTween;
using Project.Common.UI;
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
    // Play: closes the screen and starts the next level from Home.
    public readonly struct OrbitRushArgs
    {
        public readonly Action Play;
        public OrbitRushArgs(Action play) => Play = play;
    }

    // The event screen: a race view of the five-racer room and a map view of the stage route, one action button under both.
    public sealed class OrbitRushScreen : Popup
    {
        [Serializable]
        public struct RacerRow
        {
            public GameObject Root;
            public Image Background;
            public TMP_Text Rank;
            public TMP_Text Initial;
            public GameObject PlayerFace;
            public TMP_Text Name;
            public RectTransform Fill;
            public TMP_Text Score;
        }

        [Serializable]
        public struct StageGift
        {
            public RectTransform Root;
            public GameObject Ready;
            public GameObject Done;
            public TMP_Text Amount;
        }

        [SerializeField] private OrbitRushMapLayout _layout;
        [SerializeField] private RectTransform _marker;
        [SerializeField] private StageGift[] _gifts = Array.Empty<StageGift>();
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _stage;
        [SerializeField] private Button _close;
        [SerializeField] private Button _info;
        [SerializeField] private GameObject _infoPanel;
        [SerializeField] private Button _infoClose;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private float _racePanelHeight = 880f;
        [SerializeField] private float _mapPanelHeight = 330f;
        [SerializeField] private GameObject _race;
        [SerializeField] private RacerRow[] _rows = Array.Empty<RacerRow>();
        [SerializeField] private float _fillWidth = 400f;
        [SerializeField] private TMP_Text _gear;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Button _action;
        [SerializeField] private LocalizedText _actionLabel;
        [SerializeField] private Button _toggle;
        [SerializeField] private LocalizedText _toggleLabel;
        [SerializeField] private RectTransform _coinTarget;
        [SerializeField] private Sprite _coin;
        [SerializeField] private Color _playerRow = new(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color[] _medals = Array.Empty<Color>();
        [SerializeField] private float _hopTime = 0.45f;

        private OrbitRushService _rush;
        private LocalizationService _localization;
        private AudioManager _audio;
        private OrbitRushArgs _args;
        private bool _mapView;
        private float _shownStop = -1f;
        private float _nextTick;
        private Tween _hop;

        protected override void Awake()
        {
            base.Awake();
            _rush = Services.Get<OrbitRushService>();
            _localization = Services.Get<LocalizationService>();
            _audio = Services.Get<AudioManager>();
            _close.onClick.AddListener(Close);
            _action.onClick.AddListener(OnAction);
            _toggle.onClick.AddListener(() =>
            {
                _mapView = !_mapView;
                Refresh();
            });
            _info.onClick.AddListener(() => _infoPanel.SetActive(true));
            _infoClose.onClick.AddListener(() => _infoPanel.SetActive(false));
            for (var i = 0; i < _gifts.Length; i++)
            {
                _gifts[i].Root.anchorMin = _gifts[i].Root.anchorMax = OrbitRushMapLayout.Anchor(_layout.Rewards[i]);
                _gifts[i].Root.anchoredPosition = Vector2.zero;
                _gifts[i].Amount.text = (i < OrbitRushService.Stages ? OrbitRushService.StageRewards[i] : OrbitRushService.GrandPrize).ToString();
                var stage = i;
                var button = _gifts[i].Root.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => OnGift(stage));
            }
        }

        private void OnEnable() => EventBus<OrbitRushChanged>.Subscribe(OnChanged);
        private void OnDisable() => EventBus<OrbitRushChanged>.Unsubscribe(OnChanged);
        private void OnChanged(OrbitRushChanged _) => Refresh();

        protected override void OnShow(object args)
        {
            _args = args is OrbitRushArgs a ? a : default;
            _infoPanel.SetActive(false);
            _rush.MarkIntroShown();
            // The race is the main view while it runs; before joining and after the last stage the route tells the story.
            _mapView = _rush.State is OrbitRushState.Intro or OrbitRushState.Join or OrbitRushState.Completed;
            Refresh();
        }

        protected override void OnHidden() => _hop.Complete();

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            Refresh();
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            var state = _rush.State;
            var racing = state is OrbitRushState.Racing or OrbitRushState.StageWon or OrbitRushState.StageLost;
            var map = _mapView || !racing;
            _timer.text = LiveOpsText.Countdown(state == OrbitRushState.Racing ? _rush.StageTimeLeft : _rush.TimeLeft, _localization);
            _stage.text = _localization.Format("rush.stage", Math.Min(_rush.StagesWon + 1, OrbitRushService.Stages), OrbitRushService.Stages);
            _race.SetActive(!map);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, map ? _mapPanelHeight : _racePanelHeight);
            _toggle.gameObject.SetActive(racing);
            _toggleLabel.Key = map ? "rush.view_race" : "rush.view_map";
            _gear.text = _localization.Format("rush.gear", _rush.Multiplier);
            if (!map) FillRows();
            FillGifts(state);
            MoveMarker(state);

            var (status, action) = state switch
            {
                OrbitRushState.Intro or OrbitRushState.Join => (_localization.Format("rush.join_body", OrbitRushService.Capacity - 1, OrbitRushService.Winners), "rush.join"),
                OrbitRushState.Racing => (_localization.Format("rush.racing_body", _rush.Score, _rush.Target, OrbitRushService.Winners), "rush.play"),
                OrbitRushState.StageWon => (_localization.Format("rush.won_body", _localization.Get("rush.place_" + _rush.WonRank), _rush.StageReward), "rush.claim"),
                OrbitRushState.StageLost => (_localization.Format("rush.lost_body", OrbitRushService.Winners), "rush.retry"),
                _ => (_localization.Format("rush.done_body", LiveOpsText.Countdown(_rush.TimeLeft, _localization)), "common.ok")
            };
            _status.text = status;
            _actionLabel.Key = action;
        }

        private void FillRows()
        {
            var racers = _rush.Racers();
            var target = Mathf.Max(1, _rush.Target);
            for (var i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                row.Root.SetActive(i < racers.Count);
                if (i >= racers.Count) continue;
                var racer = racers[i];
                var finished = racer.Score >= target && racer.FinishedAt < double.MaxValue;
                var medal = finished && i < OrbitRushService.Winners && i < _medals.Length;
                row.Rank.text = (i + 1).ToString();
                row.Rank.color = medal ? _medals[i] : Color.white;
                row.Name.text = racer.IsPlayer ? _localization.Get("rush.you") : racer.Name;
                row.Initial.gameObject.SetActive(!racer.IsPlayer);
                row.Initial.text = racer.IsPlayer ? string.Empty : racer.Name.Substring(0, 1);
                row.PlayerFace.SetActive(racer.IsPlayer);
                row.Background.color = racer.IsPlayer ? _playerRow : Color.white;
                row.Score.text = $"{racer.Score}/{target}";
                var share = Mathf.Clamp01(racer.Score / (float)target);
                row.Fill.gameObject.SetActive(share > 0f);
                row.Fill.sizeDelta = new Vector2(Mathf.Max(row.Fill.sizeDelta.y, _fillWidth * share), row.Fill.sizeDelta.y);
            }
        }

        private void FillGifts(OrbitRushState state)
        {
            for (var i = 0; i < _gifts.Length; i++)
            {
                var gift = _gifts[i];
                var grand = i == OrbitRushService.Stages;
                var done = grand ? state == OrbitRushState.Completed : i < _rush.StagesWon;
                var ready = !grand && state == OrbitRushState.StageWon && i == _rush.Stage;
                gift.Done.SetActive(done);
                gift.Ready.SetActive(ready);
            }
        }

        // The marker walks the stage's stretch of tiles by score share; finished stages put it on their gift.
        private void MoveMarker(OrbitRushState state)
        {
            var waypoints = _layout.Waypoints;
            float stop;
            if (state == OrbitRushState.Completed) stop = waypoints[^1];
            else if (state is OrbitRushState.Intro or OrbitRushState.Join) stop = waypoints[0];
            else
            {
                var stage = _rush.Stage;
                var share = Mathf.Clamp01(_rush.Score / (float)Mathf.Max(1, _rush.Target));
                stop = Mathf.Lerp(waypoints[stage], waypoints[stage + 1], share);
            }

            if (Mathf.Approximately(_shownStop, stop)) return;
            if (_shownStop < 0f || _shownStop > stop)
            {
                _hop.Stop();
                _shownStop = stop;
                Place(stop, 0f);
                return;
            }
            var from = _shownStop;
            _shownStop = stop;
            _hop.Stop();
            _hop = Tween.Custom(this, 0f, 1f, _hopTime * Mathf.Max(1f, stop - from),
                (screen, t) => screen.Place(Mathf.Lerp(from, stop, t), Mathf.Sin(t * Mathf.PI)), Ease.InOutSine, useUnscaledTime: true);
        }

        // A point between path tiles, lifted while it moves.
        private void Place(float stop, float lift)
        {
            var stops = _layout.Stops;
            var a = Mathf.Clamp(Mathf.FloorToInt(stop), 0, stops.Length - 1);
            var b = Mathf.Min(a + 1, stops.Length - 1);
            var pixel = Vector2.Lerp(stops[a], stops[b], stop - a);
            _marker.anchorMin = _marker.anchorMax = OrbitRushMapLayout.Anchor(pixel);
            _marker.anchoredPosition = new Vector2(0f, lift * 60f);
        }

        private void OnGift(int stage)
        {
            if (_rush.State == OrbitRushState.StageWon && stage == _rush.Stage) Claim(_gifts[stage].Root);
        }

        private void OnAction()
        {
            switch (_rush.State)
            {
                case OrbitRushState.Intro:
                case OrbitRushState.Join:
                    _rush.Join();
                    _mapView = false;
                    Refresh();
                    break;
                case OrbitRushState.Racing:
                    Close();
                    _args.Play?.Invoke();
                    break;
                case OrbitRushState.StageWon:
                    Claim(_gifts[_rush.Stage].Root);
                    break;
                case OrbitRushState.StageLost:
                    _rush.Retry();
                    _mapView = false;
                    Refresh();
                    break;
                default:
                    Close();
                    break;
            }
        }

        // The gift pays out, then the marker rolls on to the next stage on the map.
        private void Claim(RectTransform from)
        {
            if (_rush.ClaimStage() <= 0) return;
            _audio.PlayOneShotSound("coin_double");
            CoinBurst.Play(from, _coinTarget, _coin);
            _mapView = true;
            Refresh();
        }
    }
}
