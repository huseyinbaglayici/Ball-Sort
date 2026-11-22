using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Pooling;
using Project.Core.Services;
using Project.Core.StateMachine;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game
{
    // A try with a move that ends unwon costs a life; the fail screen doesn't, since Undo or Continue can resume it.
    public sealed partial class GameController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ItemRegistry _items;
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private LevelPalette _palette;

        [Header("Scene")]
        [SerializeField] private BoardView _board;

        [Header("UI")]
        [SerializeField] private GameObject _hud;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private ResultPopup _resultPopup;
        [SerializeField] private MessagePopup _messagePopup;
        [SerializeField] private BoosterBar _boosters;
        [SerializeField] private DifficultyTag _difficulty;
        [SerializeField] private SettingsMenu _settings;
        // The HUD coin counter and its icon, where a win's coins land.
        [SerializeField] private EconomyHud _coinHud;
        [SerializeField] private RectTransform _coinTarget;
        [SerializeField] private Sprite _coinSprite;

        private readonly StateMachine<GameState> _states = new();
        // Mechanic outcome sounds of the move that just landed (filled by MechanicSounds, played in order).
        private readonly System.Collections.Generic.List<string> _soundEvents = new();
        private ItemFactory _factory;
        private ProgressService _progress;
        private LivesService _lives;
        private MonetizationService _ads;
        private LocalizationService _loc;
        private AudioManager _audio;
        private HapticModule _haptics;
        private SortBoard _model;
        private BottleItem _selected;
        // The winning move is made in the model; input waits until its last ball lands.
        private bool _winPending;
        // Home was chosen; until the switch covers the board, landing moves and taps must not change the try.
        private bool _leaving;
        // Set when a move's last ball lands; checked once the board is still. Undo keeps it.
        private bool _stuckCheckPending;
        private int _level;
        // At least one move was made in this try (undo does not clear it); ending the try then costs a life.
        private bool _tryHasMoves;
        private BoosterInventory _inventory;
        private int _extraTubes;
        // Armed Free Move: the next transfer onto a different open color uses it and pays only then.
        private bool _freeArmed;
        private readonly List<int> _freeTargets = new();
        private ContinueService _continue;
        private PopupService _popups;
        // The last transfer made, so a Continue that rewinds an explosion can fly its balls back.
        private MoveResult _lastMove;
        // The next PlayingState.Enter resumes this try (after a fail-screen Undo or Continue) instead of a new one.
        private bool _resumeTry;
        private System.Threading.CancellationTokenSource _rescueSearch;
        // Fail timing: when the last move started, when its last meaningful change shows, and a resume frame to skip.
        private float _movedAt;
        private float _readableAt;
        private bool _resumed;

        private void Awake()
        {
            _progress = Services.Get<ProgressService>();
            _inventory = Services.Get<BoosterInventory>();
            if (_boosters != null) _boosters.Pressed += OnBoosterPressed;
            _lives = Services.Get<LivesService>();
            _ads = Services.Get<MonetizationService>();
            _loc = Services.Get<LocalizationService>();
            _continue = Services.Get<ContinueService>();
            _popups = Services.Get<PopupService>();
            if (_settings != null) _settings.ExitPressed += RequestQuit;
            _audio = Services.Get<AudioManager>();
            _haptics = Services.Get<HapticModule>();
            _factory = new ItemFactory(Services.Get<PoolService>(), _items);
            _factory.Prewarm();

            _states.Add(GameState.Idle, new IdleState(this))
                   .Add(GameState.Playing, new PlayingState(this))
                   .Add(GameState.Won, new WonState(this))
                   .Add(GameState.Lost, new LostState(this));
            _states.ChangeState(GameState.Idle);
        }

        private void OnDestroy()
        {
            if (_boosters != null) _boosters.Pressed -= OnBoosterPressed;
            if (_settings != null) _settings.ExitPressed -= RequestQuit;
        }

        public void Enter()
        {
            _hud.SetActive(true);
            _states.ChangeState(GameState.Playing);
        }

        public void Exit() => _states.ChangeState(GameState.Idle);

        // From a won level straight into the next one's board, built for the saved level.
        public void NextLevel()
        {
            if (_states.CurrentKey == GameState.Won) _states.ChangeState(GameState.Playing);
        }

        private void OnEnable()
        {
            EventBus<BottleTapped>.Subscribe(OnBottleTapped);
            DebugPanel.AddAction("Undo move", Undo);
            DebugPanel.AddAction("Restart level", Restart);
            DebugPanel.AddAction("Win level", Win);
            DebugPanel.AddAction("Lose level", Lose);
        }

        private void OnDisable()
        {
            EventBus<BottleTapped>.Unsubscribe(OnBottleTapped);
            DebugPanel.RemoveAction("Undo move");
            DebugPanel.RemoveAction("Restart level");
            DebugPanel.RemoveAction("Win level");
            DebugPanel.RemoveAction("Lose level");
        }

        private void Update() => _states.Tick(Time.deltaTime);

        // A new screen size moves the HUD: re-measure the band after the safe area applies and relayout once still.
        private void LateUpdate()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == _screen || _model == null || _board.IsAnimating) return;
            _screen = screen;
            _board.ScreenBand = HudBand();
            _board.Relayout();
        }

        // Background time never counts toward the fail panel wait.
        private void OnApplicationPause(bool paused)
        {
            if (!paused) _resumed = true;
        }

        private void BeginLevel()
        {
            _level = _progress.Level;
            // Each retry shows the next layout variant.
            var data = _catalog != null ? _catalog.Get(_level, _progress.PickVariant(_catalog.VariantCount(_level))) : null;
            if (data == null)
            {
                Debug.LogError("[Game] No level found. Import levels, then run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            _model = new SortBoard(data);
            _selected = null;
            _winPending = false;
            _leaving = false;
            _stuckCheckPending = false;
            _tryHasMoves = false;
            _extraTubes = 0;
            _freeArmed = false;
            _lastMove = null;
            _screen = new Vector2Int(Screen.width, Screen.height);
            _board.ScreenBand = HudBand();
            _board.Build(data, _level, _model, _factory, _palette);
            _board.PlayEntrance();
            _ads.BeginActivePlay();
            RefreshHud();
            EventBus<LevelStarted>.Raise(new LevelStarted(_level, data, _model));
        }

        // Gap kept between the board's art and each HUD block, as a share of the screen height.
        private const float HudGap = 0.006f;
        // Screen size the board was last laid out for.
        private Vector2Int _screen;

        // Free band (0-1 of screen height) from the tray top to the badge bottom, minus any entrance slide still running.
        private Vector2? HudBand()
        {
            var badge = _difficulty != null ? _difficulty.transform.Find("Badge") as RectTransform : null;
            var tray = _boosters != null ? _boosters.transform as RectTransform : null;
            if (badge == null || tray == null || Screen.height <= 0) return null;

            Canvas.ForceUpdateCanvases();
            var top = RestCorner(badge, 0).y / Screen.height - HudGap;
            var bottom = RestCorner(tray, 1).y / Screen.height + HudGap;
            return top > bottom ? new Vector2(bottom, top) : null;
        }

        // A corner (GetWorldCorners order) in screen pixels with every entrance above it at rest.
        private static Vector3 RestCorner(RectTransform rect, int corner)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var point = corners[corner];
            foreach (var entrance in rect.GetComponentsInParent<UiEntrance>(true))
                point -= entrance.transform.parent.TransformVector(entrance.Displacement);
            return point;
        }

        private void OnBottleTapped(BottleTapped evt)
        {
            if (_winPending || _leaving || _model == null || _states.CurrentKey != GameState.Playing) return;

            var tapped = evt.Bottle;
            if (_selected == null)
            {
                if (CanPick(tapped.Index)) Select(tapped);
                else _board.Refuse(tapped.Index);
                return;
            }

            if (_selected == tapped)
            {
                Deselect();
                return;
            }

            var from = _selected.Index;
            var to = tapped.Index;
            // With Free Move on, a target the normal rules allow still takes a normal move, which uses no charge.
            var free = _freeArmed && !_model.CanMove(from, to) && _model.CanFreeMove(from, to);
            if (_board.IsLocked(to) || !(free || _model.CanMove(from, to)))
            {
                // Illegal target: the group drops back and the tube shakes. Not a move; an armed Free Move stays armed.
                Deselect();
                _board.Reject(to);
                return;
            }

            // Free Move is paid only now, once the board accepts the transfer.
            if (free && !_inventory.TryUse(BoosterType.FreeMove, _level, out _))
            {
                Deselect();
                _freeArmed = false;
                ShowNotEnoughCoins(BoosterType.FreeMove);
                RefreshHud();
                return;
            }

            // The model moves at once; the view catches up. The lifted balls keep their pose and fly from there.
            _selected = null;
            MoveResult move;
            if (free) _model.TryFreeMove(from, to, out move);
            else _model.TryMove(from, to, out move);
            if (free) _freeArmed = false;
            _tryHasMoves = true;
            _lastMove = move;
            _movedAt = Time.time;
            _board.Sync(_model, move);
            EventBus<MoveMade>.Raise(new MoveMade(from, to));
            var reveal = MechanicSounds.OnMove(move);
            if (reveal != null) _audio.PlayOneShotSound(reveal);
            _winPending = _model.IsSolved();
            // Sounds and haptics of the move are played by the board, one per ball on its own landing frame.
            _board.Transfer(move.From, move.To, move.Count, () =>
            {
                if (free) _audio.PlayOneShotSound("free_commit");
                OnMoveLanded(move);
            });
            _board.AssertMatches(_model);
            RefreshHud();
        }

        // Never runs for a move whose flight was cancelled.
        private void OnMoveLanded(MoveResult move)
        {
            if (_leaving || _states.CurrentKey != GameState.Playing) return;
            _readableAt = FailWindow.ReadableAt(Time.time, _movedAt, move, _board.Animation);
            foreach (var bottle in move.Completed)
                if (_model.IsCompleted(bottle)) _board.SetCompleted(bottle, true);
            _board.PlayObstacles(move);
            if (move.Exploded && _model.IsFailed) _board.Explode();
            // The moment's mechanic sounds; Undo never comes through here, so rewound effects stay silent.
            _soundEvents.Clear();
            MechanicSounds.OnLanded(move, _model, _winPending, _soundEvents);
            foreach (var id in _soundEvents) _audio.PlayOneShotSound(id);
            if (_winPending) _states.ChangeState(GameState.Won);
            // A bomb whose fuse ran out ends the try once the move is seen.
            else if (_model.IsFailed) _states.ChangeState(GameState.Lost);
            else _stuckCheckPending = true;
        }

        // IsLocked: a ball is flying into or out of it.
        private bool CanPick(int bottle) => _model.CanSelectAsOrigin(bottle) && !_board.IsLocked(bottle);

        private void Select(BottleItem bottle)
        {
            _selected = bottle;
            // The board plays the lift sounds and the one selection haptic.
            _board.Select(bottle.Index, _model.TopRun(bottle.Index));
            ShowFreeTargets();
            EventBus<BottleSelected>.Raise(new BottleSelected(bottle.Index));
        }

        private void Deselect()
        {
            if (_selected == null) return;
            var index = _selected.Index;
            _board.Deselect(index);
            _selected = null;
            _board.HideTargets();
            EventBus<BottleDeselected>.Raise(new BottleDeselected(index));
        }

        private void Win()
        {
            if (_states.CurrentKey == GameState.Playing && !_winPending) _states.ChangeState(GameState.Won);
        }

        private void Lose()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Lost);
        }

        private void RefreshHud()
        {
            _levelText.text = _loc.Format("home.level", _level);
            if (_difficulty != null)
            {
                var difficulty = CampaignRules.DifficultyOf(_level);
                _difficulty.Set(EconomyText.Difficulty(difficulty, _loc), EconomyText.DifficultyMarks(difficulty));
            }
            if (_boosters != null)
                _boosters.Refresh(_level, _model != null && _model.CanUndo, _extraTubes < MaxExtraTubes, _freeArmed);
        }

        // No level shown: the board is empty and taps are ignored.
        private sealed class IdleState : State
        {
            private readonly GameController _c;
            public IdleState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.CancelRescueSearch();
                _c._ads.EndActivePlay();
                _c._resumeTry = false;
                _c.Deselect();
                _c._board.Clear();
                _c._model = null;
                _c._winPending = false;
                _c._leaving = false;
                _c._stuckCheckPending = false;
                _c._hud.SetActive(false);
                EventBus<LevelLeft>.Raise(default);
            }
        }

        private sealed class PlayingState : State
        {
            private readonly GameController _c;
            public PlayingState(GameController controller) => _c = controller;

            public override void Enter()
            {
                if (!_c._resumeTry)
                {
                    _c.BeginLevel();
                    return;
                }

                // Back from the fail screen into the same try (Undo or Continue): no new board, no new try.
                _c._resumeTry = false;
                _c._freeArmed = false;
                _c._stuckCheckPending = false;
                _c._ads.BeginActivePlay();
                _c.RefreshHud();
            }

            public override void Tick(float deltaTime)
            {
                if (!_c._stuckCheckPending || _c._leaving || _c._board.IsAnimating) return;
                _c._stuckCheckPending = false;
                if (_c._model != null && !_c._winPending && _c._model.HasNoMove()) _c._states.ChangeState(GameState.Lost);
            }
        }
    }
}
