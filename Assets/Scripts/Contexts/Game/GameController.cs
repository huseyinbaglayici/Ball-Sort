using Project.Contexts.Application;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Contexts.Main;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Pooling;
using Project.Core.Services;
using Project.Core.StateMachine;
using TMPro;
using UnityEngine;
using Project.Common.UI.PopUps;

namespace Project.Contexts.Game
{
    /// <summary>
    /// The sort puzzle flow: loads the current level, turns taps on bottles into moves and shows the result.
    /// Rules live in <see cref="SortBoard"/>, visuals in <see cref="BoardView"/>, item creation in <see cref="ItemFactory"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ItemRegistry _items;
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private LevelPalette _palette;
        [SerializeField, Min(0)] private int _reward = 25;

        [Header("Scene")]
        [SerializeField] private BoardView _board;

        [Header("UI")]
        [SerializeField] private GameObject _hud;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _movesText;
        [SerializeField] private ResultPopup _resultPopup;

        private readonly StateMachine<GameState> _states = new();
        private ItemFactory _factory;
        private ProgressService _progress;
        private AudioService _audio;
        private HapticService _haptics;
        private SortBoard _model;
        private BottleItem _selected;
        // The winning move is made in the model; input waits until its last ball lands.
        private bool _winPending;
        // The deadlock check request: set when a move's last ball lands, kept until the board is stopped (no ball
        // flying; a lifted selection does not count), then checked once and cleared. Undo does not clear it.
        private bool _stuckCheckPending;
        private int _level;

        private void Awake()
        {
            _progress = Services.Get<ProgressService>();
            _audio = Services.Get<AudioService>();
            _haptics = Services.Get<HapticService>();
            _factory = new ItemFactory(Services.Get<PoolService>(), _items);
            _factory.Prewarm();

            _states.Add(GameState.Idle, new IdleState(this))
                   .Add(GameState.Playing, new PlayingState(this))
                   .Add(GameState.Won, new WonState(this))
                   .Add(GameState.Lost, new LostState(this));
            _states.ChangeState(GameState.Idle);
        }

        /// <summary>Shows the HUD and starts the current level.</summary>
        public void Enter()
        {
            _hud.SetActive(true);
            _states.ChangeState(GameState.Playing);
        }

        /// <summary>Stops the level, returns the board's items to the pools and hides the HUD.</summary>
        public void Exit() => _states.ChangeState(GameState.Idle);

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

        private void BeginLevel()
        {
            _level = _progress.Level;
            // Each retry shows the level's next layout variant.
            var data = _catalog != null ? _catalog.Get(_level, _progress.Attempt) : null;
            if (data == null)
            {
                Debug.LogError("[Game] No level found. Import levels, then run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            _model = new SortBoard(data);
            _selected = null;
            _winPending = false;
            _stuckCheckPending = false;
            _board.Build(data, _model, _factory, _palette);
            RefreshHud();
        }

        private void OnBottleTapped(BottleTapped evt)
        {
            if (_winPending || _model == null || _states.CurrentKey != GameState.Playing) return;

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
            if (_board.IsLocked(to) || !_model.CanMove(from, to))
            {
                // Not a legal target: the tap just moves the selection when that bottle can be picked up; otherwise
                // the selection drops and the tapped tube says no.
                Deselect();
                if (CanPick(to)) Select(tapped);
                else _board.Reject(to);
                return;
            }

            // The model moves at once; the view catches up. The lifted balls keep their pose and fly from there.
            _selected = null;
            _model.TryMove(from, to, out var move);
            if (move.RevealedSlot >= 0) _board.Reveal(move.From, move.RevealedSlot);
            _winPending = _model.IsSolved();
            _haptics.Play(HapticStrength.Light);
            _board.Transfer(move.From, move.To, move.Count, () => OnMoveLanded(move));
            _board.AssertMatches(_model);
            RefreshHud();
        }

        /// <summary>The last ball of a move landed. Never runs for a move whose flight was cancelled.</summary>
        private void OnMoveLanded(BallMove move)
        {
            if (move.Completed && _model.IsCompleted(move.To)) _board.SetCompleted(move.To, true);
            if (_winPending) _states.ChangeState(GameState.Won);
            else _stuckCheckPending = true;
        }

        /// <summary>A bottle can be picked up when the rules allow it and no ball is flying into or out of it.</summary>
        private bool CanPick(int bottle) => _model.CanSelectAsOrigin(bottle) && !_board.IsLocked(bottle);

        private void Select(BottleItem bottle)
        {
            _selected = bottle;
            _board.Select(bottle.Index, _model.TopRun(bottle.Index));
            _haptics.Play(HapticStrength.Light);
        }

        private void Deselect()
        {
            if (_selected == null) return;
            _board.Deselect(_selected.Index);
            _selected = null;
        }

        /// <summary>Works during motion too: balls still flying turn back from where they are.</summary>
        private void Undo()
        {
            if (_winPending || _model == null || _states.CurrentKey != GameState.Playing) return;
            Deselect();
            if (!_model.TryUndo(out var move)) return;

            if (move.Completed) _board.SetCompleted(move.To, false);
            _board.Transfer(move.To, move.From, move.Count, null);
            _board.AssertMatches(_model);
            RefreshHud();
        }

        private void Restart()
        {
            if (_states.CurrentKey != GameState.Playing) return;
            _progress.FailAttempt();
            _states.ChangeState(GameState.Playing);
        }

        private void Win()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Won);
        }

        private void Lose()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Lost);
        }

        private void RefreshHud()
        {
            _levelText.text = $"Level {_level}";
            _movesText.text = _model != null ? $"Moves {_model.MoveCount}" : string.Empty;
        }

        /// <summary>No level is shown: the board is empty and taps are ignored.</summary>
        private sealed class IdleState : State
        {
            private readonly GameController _c;
            public IdleState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._board.Clear();
                _c._model = null;
                _c._winPending = false;
                _c._stuckCheckPending = false;
                _c._hud.SetActive(false);
            }
        }

        private sealed class PlayingState : State
        {
            private readonly GameController _c;
            public PlayingState(GameController controller) => _c = controller;

            public override void Enter() => _c.BeginLevel();

            public override void Tick(float deltaTime)
            {
                if (!_c._stuckCheckPending || _c._board.IsAnimating) return;
                _c._stuckCheckPending = false;
                if (_c._model != null && !_c._winPending && _c._model.HasNoMove()) _c._states.ChangeState(GameState.Lost);
            }
        }

        /// <summary>
        /// No move is left. The try counts as failed, so the next try shows the level's next layout variant, the same as
        /// a restart.
        /// </summary>
        private sealed class LostState : State
        {
            private readonly GameController _c;
            public LostState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._audio.PlaySfx("lose");
                _c._haptics.Play(HapticStrength.Heavy);
                _c._progress.FailAttempt();
                Services.Get<PopupService>().Show(_c._resultPopup,
                    new ResultArgs(false, 0, () => _c._states.ChangeState(GameState.Playing)));
            }
        }

        private sealed class WonState : State
        {
            private readonly GameController _c;
            public WonState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._progress.CompleteLevel(_c._reward);
                _c._audio.PlaySfx("win");
                _c._haptics.Play(HapticStrength.Heavy);
                Services.Get<PopupService>().Show(_c._resultPopup,
                    new ResultArgs(true, _c._reward, () => _ = Services.Get<MainFlow>().GoHomeAsync()));
            }
        }
    }
}
