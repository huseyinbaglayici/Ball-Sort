using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Preview
{
    // Debug scene driver: three real tubes for the completion moment, or a small real board for the boosters.
    public sealed class TubeFxPreview : MonoBehaviour
    {
        private static readonly int[] Sizes = { 4, 8, 12 };
        private const float Pitch = 2.1f;
        private const float SlowMotion = 0.25f;
        // GUI is laid out for this screen height and scaled to the real one.
        private const float GuiHeight = 1280f;
        private const float CameraMargin = 2.6f;
        private const float MinOrthoSize = 7.2f;

        [SerializeField] private ItemRegistry _registry;
        [SerializeField] private LevelPalette _palette;
        [SerializeField] private BallAnimationConfig _config;
        [SerializeField] private Camera _camera;
        [SerializeField] private int _colorIndex;
        [SerializeField] private BoardView _boardView;

        private PoolService _pools;
        private ItemFactory _factory;
        private Transform _board;
        private readonly List<BottleItem> _tubes = new();
        private CompleteFx _fx;
        private int _size = 4;
        private bool _slow;
        private float _replayedAt = -1f;
        private GUIStyle _button, _label;
        private bool _boosters;
        private SortBoard _model;
        private LevelData _level;
        private int _selected = -1;
        private FreeMoveHint _hint;
        private readonly List<int> _targets = new();

        private void Start()
        {
            _pools = new PoolService(new GameObject("Pool").transform);
            _factory = new ItemFactory(_pools, _registry);
            _board = new GameObject("Board").transform;
            _hint = _config.Boosters.Hint;
            Build();
        }

        private void OnDestroy()
        {
            // The hint is switched on the shared config asset; put it back.
            if (_config != null) _config.Boosters.Hint = _hint;
            if (_level != null) Destroy(_level);
            Time.timeScale = 1f;
            _pools?.Dispose();
        }

        // Middle tube full of one color, neighbours partly filled; the camera fits the middle tube.
        private void Build()
        {
            StopFx();
            foreach (var tube in _tubes)
            {
                while (tube.BallCount > 0) _factory.Release(tube.Pop());
                _factory.Release(tube);
            }
            _tubes.Clear();
            if (_boosters) return;

            AddTube(-1, 4, new[] { 1, 2, 1 });
            var full = new int[_size];
            for (var i = 0; i < full.Length; i++) full[i] = _colorIndex;
            AddTube(0, _size, full);
            AddTube(1, 4, new[] { 2, 1 });

            var height = TubeArt.MouthLocalY + TubeArt.Rise(_size);
            _board.position = new Vector3(0f, -height * 0.5f, 0f);
            _camera.orthographicSize = Mathf.Max(MinOrthoSize, height * 0.5f + CameraMargin);
        }

        private void AddTube(int column, int capacity, int[] colors)
        {
            var tube = _factory.Get<BottleItem>(ItemType.Bottle, _board);
            tube.Setup(_tubes.Count, capacity, new Vector2(column * Pitch, 0f), TubeArt.Rise(capacity), _config);
            foreach (var color in colors)
            {
                var ball = _factory.Get<BallItem>(ItemType.Ball, tube.transform);
                ball.Setup(color, _palette.GetOrb(color), _palette.Get(color), false, _config);
                tube.Push(ball, snap: true);
            }
            _tubes.Add(tube);
        }

        // Back to the dim, open tube, then the whole completion: charge, cap, glass, orbs and finish.
        private void Replay()
        {
            StopFx();
            var tube = _tubes[1];
            tube.SetCompleted(false, false);
            tube.SetCompleted(true, true);
            _fx = _factory.Get<CompleteFx>(ItemType.CompleteFx, _board);
            _fx.Play(tube, _config, done =>
            {
                if (_fx == done) _fx = null;
                _factory.Release(done);
            });
            _replayedAt = Time.time;
        }

        private void StopFx()
        {
            if (_fx == null) return;
            var fx = _fx;
            _fx = null;
            _factory.Release(fx);
        }

        // Bottom to mouth; tube 1 can finish tube 0, and the top of tube 2 can only be free-moved onto 0 or 1.
        private void BuildBoard()
        {
            _selected = -1;
            if (_level == null) _level = ScriptableObject.CreateInstance<LevelData>();
            _level.Bottles.Clear();
            int[][] stacks = { new[] { 1, 1, 1 }, new[] { 2, 2, 1 }, new[] { 2, 2 }, new int[0] };
            for (var b = 0; b < stacks.Length; b++)
            {
                var bottle = BottleData.CreateEmpty(4, new Vector2(b, 0f));
                for (var i = 0; i < stacks[b].Length; i++) bottle.Slots[i] = new SlotData { Color = stacks[b][i] };
                _level.Bottles.Add(bottle);
            }
            _model = new SortBoard(_level);
            _boardView.Build(_level, 1, _model, _factory, _palette);
        }

        private void ShowSection(bool boosters)
        {
            _boosters = boosters;
            if (!boosters) _boardView.Clear();
            _camera.orthographicSize = MinOrthoSize;
            Build();
            if (boosters) BuildBoard();
        }

        private void Move(int from, int to)
        {
            if (_boardView.IsAnimating && _selected < 0) return;
            Drop(keepLifted: true);
            var free = !_model.CanMove(from, to) && _model.CanFreeMove(from, to);
            if (_boardView.IsLocked(to) || !(free || _model.CanMove(from, to))) return;
            MoveResult move;
            if (free) _model.TryFreeMove(from, to, out move);
            else _model.TryMove(from, to, out move);
            _boardView.Sync(_model, move);
            _boardView.Transfer(move.From, move.To, move.Count, () =>
            {
                foreach (var bottle in move.Completed)
                    if (_model.IsCompleted(bottle)) _boardView.SetCompleted(bottle, true);
                _boardView.PlayObstacles(move);
            });
        }

        private void Undo()
        {
            Drop();
            if (!_model.TryUndo(out var move)) return;
            _boardView.Transfer(move.To, move.From, move.Count, null, silent: true);
            _boardView.Sync(_model);
            _boardView.AssertMatches(_model);
        }

        private void ExtraTube()
        {
            if (_boardView.IsAnimating) return;
            Drop();
            _boardView.AddBottle(_model, _model.AddBottle(2), null);
        }

        // Lifts tube 2 with Free Move armed and marks every tube that can take its top, in the given hint.
        private void ArmFree(FreeMoveHint hint)
        {
            if (_boardView.IsAnimating || !_model.CanSelectAsOrigin(2)) return;
            Drop();
            _config.Boosters.Hint = hint;
            _selected = 2;
            _boardView.Select(_selected, _model.TopRun(_selected));
            _targets.Clear();
            for (var to = 0; to < _model.BottleCount; to++)
                if (to != _selected && (_model.CanMove(_selected, to) || _model.CanFreeMove(_selected, to))) _targets.Add(to);
            _boardView.ShowTargets(_selected, _targets);
        }

        // Drops lifted balls back; a move takes them instead, so it clears the selection first.
        private void Drop(bool keepLifted = false)
        {
            if (_selected < 0) return;
            var lifted = _selected;
            _selected = -1;
            if (!keepLifted) _boardView.Deselect(lifted);
            _boardView.HideTargets();
        }

        private void OnGUI()
        {
            var scale = Screen.height / GuiHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 22 };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };

            var top = (width - 40f) / 2f;
            if (GUI.Button(new Rect(20f, 20f, top - 8f, 56f), _boosters ? "Finish" : "[Finish]", _button)) ShowSection(false);
            if (GUI.Button(new Rect(20f + top, 20f, top - 8f, 56f), _boosters ? "[Boosters]" : "Boosters", _button)) ShowSection(true);
            if (_boosters)
            {
                BoosterGui(width);
                return;
            }

            var y = GuiHeight - 200f;
            var since = _replayedAt < 0f ? "-" : $"{Time.time - _replayedAt:0.00}s";
            GUI.Label(new Rect(20f, y, width - 40f, 36f), $"size {_size}   speed {Time.timeScale:0.##}x   t {since}", _label);
            y += 44f;

            var cell = (width - 40f) / (Sizes.Length + 1);
            for (var i = 0; i < Sizes.Length; i++)
            {
                var rect = new Rect(20f + cell * i, y, cell - 8f, 56f);
                if (!GUI.Button(rect, Sizes[i] == _size ? $"[{Sizes[i]}]" : Sizes[i].ToString(), _button)) continue;
                _size = Sizes[i];
                Build();
            }
            if (GUI.Button(new Rect(20f + cell * Sizes.Length, y, cell - 8f, 56f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
            y += 64f;
            if (GUI.Button(new Rect(20f, y, width - 40f, 64f), "Replay", _button)) Replay();
        }

        private void BoosterGui(float width)
        {
            var y = GuiHeight - 334f;
            var third = (width - 40f) / 3f;
            if (_selected >= 0)
            {
                GUI.Label(new Rect(20f, y - 40f, width - 40f, 36f), "Free Move armed: drop onto a marked tube", _label);
                if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Drop on 0", _button)) Move(_selected, 0);
                if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), "Drop on 3", _button)) Move(_selected, 3);
                if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), "Cancel", _button)) Drop();
            }
            else
            {
                if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Finish 0", _button)) Move(1, 0);
                if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), "Move 2>3", _button)) Move(2, 3);
                if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), "Reset", _button)) BuildBoard();
            }
            y += 64f;
            if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Undo", _button)) Undo();
            if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), "Extra Tube", _button)) ExtraTube();
            if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
            y += 64f;
            var half = (width - 40f) / 2f;
            if (GUI.Button(new Rect(20f, y, half - 8f, 56f), "Free Move A: portals", _button)) ArmFree(FreeMoveHint.Portals);
            if (GUI.Button(new Rect(20f + half, y, half - 8f, 56f), "Free Move B: slots", _button)) ArmFree(FreeMoveHint.SlotOutlines);
            y += 64f;
            if (GUI.Button(new Rect(20f, y, half - 8f, 56f), "Win", _button)) _boardView.PlayResult(true);
            if (GUI.Button(new Rect(20f + half, y, half - 8f, 56f), "Lose", _button)) _boardView.PlayResult(false);
        }
    }
}
