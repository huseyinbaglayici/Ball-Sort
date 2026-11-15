using Project.Contexts.Application;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Pooling;
using Project.Core.Services;
using Project.Core.StateMachine;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Project.Common.UI.PopUps;

namespace Project.Contexts.Game
{
    /// <summary>
    /// The sort puzzle flow: loads the current level, turns taps on bottles into moves and shows the result.
    /// Rules live in <see cref="SortBoard"/>, visuals in <see cref="BoardView"/>, item creation in <see cref="ItemFactory"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        // The retry counter of the level being played, kept for the session. Each retry shows the next layout variant.
        private static int _attemptLevel = -1;
        private static int _attempt;

        [Header("Data")]
        [SerializeField] private ItemRegistry _items;
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private LevelPalette _palette;
        [SerializeField, Min(0)] private int _reward = 25;

        [Header("Scene")]
        [SerializeField] private BoardView _board;

        [Header("UI")]
        [FormerlySerializedAs("_timerText")] [SerializeField] private TMP_Text _levelText;
        [FormerlySerializedAs("_progressText")] [SerializeField] private TMP_Text _movesText;
        [SerializeField] private ResultPopup _resultPopup;

        private readonly StateMachine<GameState> _states = new();
        private ItemFactory _factory;
        private ProgressService _progress;
        private AudioService _audio;
        private HapticService _haptics;
        private SortBoard _model;
        private BottleItem _selected;
        private bool _busy;
        private int _level;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _attemptLevel = -1;
            _attempt = 0;
        }

        private void Start()
        {
            _progress = Services.Get<ProgressService>();
            _audio = Services.Get<AudioService>();
            _haptics = Services.Get<HapticService>();
            _factory = new ItemFactory(Services.Get<PoolService>(), _items);
            _factory.Prewarm();

            _states.Add(GameState.Playing, new PlayingState(this))
                   .Add(GameState.Won, new WonState(this));
            _states.ChangeState(GameState.Playing);

            DebugPanel.AddAction("Undo move", Undo);
            DebugPanel.AddAction("Restart level", Restart);
            DebugPanel.AddAction("Win level", Win);
        }

        private void OnEnable() => EventBus<BottleTapped>.Subscribe(OnBottleTapped);

        private void OnDisable()
        {
            EventBus<BottleTapped>.Unsubscribe(OnBottleTapped);
            DebugPanel.RemoveAction("Undo move");
            DebugPanel.RemoveAction("Restart level");
            DebugPanel.RemoveAction("Win level");
        }

        private void Update() => _states.Tick(Time.deltaTime);

        private void BeginLevel()
        {
            _level = _progress.Level;
            if (_attemptLevel != _level)
            {
                _attemptLevel = _level;
                _attempt = 0;
            }

            var data = _catalog != null ? _catalog.Get(_level, _attempt) : null;
            if (data == null)
            {
                Debug.LogError("[Game] No level found. Import levels, then run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            _model = new SortBoard(data);
            _selected = null;
            _busy = false;
            _board.Build(data, _model, _factory, _palette);
            RefreshHud();
        }

        private void OnBottleTapped(BottleTapped evt)
        {
            if (_busy || _model == null || _states.CurrentKey != GameState.Playing) return;

            var tapped = evt.Bottle;
            if (_selected == null)
            {
                if (!_model.CanSelectAsOrigin(tapped.Index)) return;
                Select(tapped);
                return;
            }

            if (_selected == tapped)
            {
                Deselect();
                return;
            }

            var from = _selected.Index;
            var to = tapped.Index;
            Deselect();
            if (!_model.CanMove(from, to))
            {
                // Not a legal target: the tap just moves the selection when that bottle can be picked up.
                if (_model.CanSelectAsOrigin(to)) Select(tapped);
                return;
            }

            _model.TryMove(from, to, out var move);
            _busy = true;
            _audio.PlaySfx("collect");
            _haptics.Play(HapticStrength.Light);
            _board.Transfer(move.From, move.To, move.Count, () => OnMoveFinished(move));
        }

        private void OnMoveFinished(BallMove move)
        {
            if (move.RevealedSlot >= 0) _board.Reveal(move.From, move.RevealedSlot);
            if (move.Completed) _board.SetCompleted(move.To, true);
            _busy = false;
            RefreshHud();
            if (_model.IsSolved()) _states.ChangeState(GameState.Won);
        }

        private void Select(BottleItem bottle)
        {
            _selected = bottle;
            bottle.SetSelected(true);
            _haptics.Play(HapticStrength.Light);
        }

        private void Deselect()
        {
            if (_selected == null) return;
            _selected.SetSelected(false);
            _selected = null;
        }

        private void Undo()
        {
            if (_busy || _model == null || _states.CurrentKey != GameState.Playing) return;
            Deselect();
            if (!_model.TryUndo(out var move)) return;

            if (move.Completed) _board.SetCompleted(move.To, false);
            _busy = true;
            _board.Transfer(move.To, move.From, move.Count, () =>
            {
                _busy = false;
                RefreshHud();
            });
        }

        private void Restart()
        {
            if (_states.CurrentKey != GameState.Playing) return;
            _attempt++;
            _states.ChangeState(GameState.Playing);
        }

        private void Win()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Won);
        }

        private void RefreshHud()
        {
            _levelText.text = $"Level {_level}";
            _movesText.text = _model != null ? $"Moves {_model.MoveCount}" : string.Empty;
        }

        private sealed class PlayingState : State
        {
            private readonly GameController _c;
            public PlayingState(GameController controller) => _c = controller;

            public override void Enter() => _c.BeginLevel();
        }

        private sealed class WonState : State
        {
            private readonly GameController _c;
            public WonState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _attempt = 0;
                _c.Deselect();
                _c._progress.CompleteLevel(_c._reward);
                _c._audio.PlaySfx("win");
                _c._haptics.Play(HapticStrength.Heavy);
                Services.Get<PopupService>().Show(_c._resultPopup, new ResultArgs(true, _c._reward));
            }
        }
    }
}
