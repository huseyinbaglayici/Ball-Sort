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
        // Shutter section: locked tubes in the open sample (1, 4, or a tall 8), 0 when off.
        private int _shutterTubes;
        private bool _loop;
        private float _openedAt = -1f;
        // Win section: a solved board whose last tube completes as the level-won beat plays.
        private bool _win;
        private float _wonAt = -1f;
        // Obstacle sections: a hidden ball comes open, or a frozen tube melts, after one real move.
        private enum Obstacle { None, Hidden, Frozen }
        private Obstacle _obstacle;
        private float _obstacleAt = -1f;
        // A move's flight before the obstacle reacts, for the loop.
        private const float MoveTime = 0.8f;
        // Loop restarts after the open has played out and rested a moment.
        private const float LoopRest = 1.6f;
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
            if (_boosters || _shutterTubes > 0 || _win || _obstacle != Obstacle.None) return;

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
            _level.Lockboxes.Clear();
            _level.Curtains.Clear();
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

        // A key ball in the first tube; the next tubes sit under one shutter.
        private void BuildShutterBoard()
        {
            _selected = -1;
            _openedAt = -1f;
            if (_level == null) _level = ScriptableObject.CreateInstance<LevelData>();
            _level.Bottles.Clear();
            _level.Lockboxes.Clear();
            _level.Curtains.Clear();
            var tall = _shutterTubes > 4;
            var locked = tall ? 1 : _shutterTubes;
            var keyTube = BottleData.CreateEmpty(4, new Vector2(0f, 0f));
            // Under a ball, or the box opens as soon as the board is built.
            keyTube.Slots[0] = new SlotData { Color = 1 };
            keyTube.Slots[1] = new SlotData { Color = 1, Key = "k" };
            keyTube.Slots[2] = new SlotData { Color = 2 };
            _level.Bottles.Add(keyTube);
            var box = new LockboxData { Id = "k" };
            for (var t = 0; t < locked; t++)
            {
                var capacity = tall ? 8 : 4;
                var bottle = BottleData.CreateEmpty(capacity, new Vector2(t + 1, 0f));
                var fill = tall ? 5 : 2 + t % 2;
                for (var i = 0; i < fill; i++) bottle.Slots[i] = new SlotData { Color = (i + t) % 2 + 1 };
                box.Bottles.Add(_level.Bottles.Count);
                _level.Bottles.Add(bottle);
            }
            _level.Lockboxes.Add(box);
            var spare = BottleData.CreateEmpty(4, new Vector2(locked + 1, 0f));
            spare.Slots[0] = new SlotData { Color = 2 };
            _level.Bottles.Add(spare);
            _model = new SortBoard(_level);
            _boardView.Build(_level, 20, _model, _factory, _palette);
        }

        // The game's own open: the ball over the key moves to the spare tube, the key reaches the mouth and opens the box.
        private void OpenShutters()
        {
            if (_model == null || _boardView.IsAnimating) return;
            Move(0, _model.BottleCount - 1);
            _openedAt = Time.time;
        }

        private void Update()
        {
            if (!_loop) return;
            if (_obstacle != Obstacle.None)
            {
                var play = _obstacle == Obstacle.Hidden
                    ? _config.HiddenShakeDuration + _config.HiddenOrbDelay + _config.HiddenOrbDuration + _config.HiddenSparkTime
                    : _config.MeltDuration + _config.FrozenChunkStagger * (FrozenTubeItem.ChunkCount - 1) + _config.FrozenChunkTime;
                if (_obstacleAt >= 0f && Time.time - _obstacleAt < MoveTime + play + LoopRest) return;
                PlayObstacle();
                return;
            }
            if (_win)
            {
                var winCycle = WinFx.PanelAt(_config, 4) + _config.Win.AfterPanel + LoopRest;
                if (_wonAt >= 0f && Time.time - _wonAt < winCycle) return;
                PlayWin();
                return;
            }
            if (_shutterTubes == 0) return;
            var cycle = KeyFlightFx.Duration(_config) + _config.ShutterClearTime + _config.ShutterFadeDuration + LoopRest;
            if (_openedAt >= 0f && Time.time - _openedAt < cycle) return;
            BuildShutterBoard();
            OpenShutters();
        }

        // Four full tubes; three are already capped, the fourth completes now and the win beat follows as in the game.
        private void PlayWin()
        {
            _selected = -1;
            if (_level == null) _level = ScriptableObject.CreateInstance<LevelData>();
            _level.Bottles.Clear();
            _level.Lockboxes.Clear();
            _level.Curtains.Clear();
            for (var b = 0; b < 4; b++)
            {
                var bottle = BottleData.CreateEmpty(4, new Vector2(b, 0f));
                for (var i = 0; i < 4; i++) bottle.Slots[i] = new SlotData { Color = b + 1 };
                _level.Bottles.Add(bottle);
            }
            _model = new SortBoard(_level);
            _boardView.Build(_level, 1, _model, _factory, _palette);
            for (var b = 0; b < 3; b++) _boardView.SetCompleted(b, true, celebrate: false);
            _boardView.SetCompleted(3, true);
            _boardView.PlayResult(true);
            _wonAt = Time.time;
        }

        private void ShowWin()
        {
            _boosters = false;
            _shutterTubes = 0;
            _loop = false;
            _obstacle = Obstacle.None;
            _win = true;
            _camera.orthographicSize = MinOrthoSize;
            Build();
            PlayWin();
        }

        private void ShowObstacle(Obstacle obstacle)
        {
            _boosters = false;
            _win = false;
            _shutterTubes = 0;
            _obstacle = obstacle;
            _camera.orthographicSize = MinOrthoSize;
            Build();
            BuildObstacleBoard();
        }

        // Hidden: three tubes with a hidden ball under a normal one and a spare. Frozen: a frozen tube that melts when tube 1 fills with its color.
        private void BuildObstacleBoard()
        {
            _selected = -1;
            _obstacleAt = -1f;
            if (_level == null) _level = ScriptableObject.CreateInstance<LevelData>();
            _level.Bottles.Clear();
            _level.Lockboxes.Clear();
            _level.Curtains.Clear();
            int[][] stacks = _obstacle == Obstacle.Hidden
                ? new[] { new[] { 1, 2, 3 }, new[] { 2, 3, 1 }, new[] { 3, 1, 2 }, new int[0] }
                : new[] { new[] { 2, 3, 3 }, new[] { 1, 1, 1 }, new[] { 2, 1 }, new[] { 3 } };
            for (var b = 0; b < stacks.Length; b++)
            {
                var bottle = BottleData.CreateEmpty(4, new Vector2(b, 0f));
                for (var i = 0; i < stacks[b].Length; i++)
                {
                    var hidden = _obstacle == Obstacle.Hidden && i < 2;
                    bottle.Slots[i] = new SlotData { Color = stacks[b][i], Hidden = hidden, Kind = hidden ? SlotKind.Special : SlotKind.Normal };
                }
                _level.Bottles.Add(bottle);
            }
            if (_obstacle == Obstacle.Frozen) _level.Curtains.Add(new CurtainData { Bottle = 0, Color = 1, Need = 4 });
            _model = new SortBoard(_level);
            _boardView.Build(_level, 1, _model, _factory, _palette);
        }

        // One real move: the hidden ball under it comes open, or tube 1 fills and the ice melts.
        private void PlayObstacle()
        {
            BuildObstacleBoard();
            if (_obstacle == Obstacle.Hidden) Move(0, 3);
            else Move(2, 1);
            _obstacleAt = Time.time;
        }

        private void ObstacleGui(float width)
        {
            var y = GuiHeight - 200f;
            var third = (width - 40f) / 3f;
            var since = _obstacleAt < 0f ? "-" : $"{Time.time - _obstacleAt:0.00}s";
            var what = _obstacle == Obstacle.Hidden ? "Hidden: charge, crack light, shards, orb pop" : "Frozen: tremble, crack beats, chunks break off";
            GUI.Label(new Rect(20f, y - 76f, width - 40f, 36f), what, _label);
            GUI.Label(new Rect(20f, y - 40f, width - 40f, 36f), $"speed {Time.timeScale:0.##}x   t {since}", _label);
            if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Replay", _button)) PlayObstacle();
            if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), _loop ? "[Loop]" : "Loop", _button)) _loop = !_loop;
            if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
        }

        private void ShowShutters(int tubes)
        {
            _boosters = false;
            _win = false;
            _obstacle = Obstacle.None;
            _shutterTubes = tubes;
            _camera.orthographicSize = MinOrthoSize;
            Build();
            BuildShutterBoard();
        }

        private void ShowSection(bool boosters)
        {
            _shutterTubes = 0;
            _loop = false;
            _win = false;
            _obstacle = Obstacle.None;
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

            var top = (width - 40f) / 6f;
            var finish = !_boosters && _shutterTubes == 0 && !_win && _obstacle == Obstacle.None;
            if (GUI.Button(new Rect(20f, 20f, top - 8f, 56f), finish ? "[Finish]" : "Finish", _button)) ShowSection(false);
            if (GUI.Button(new Rect(20f + top, 20f, top - 8f, 56f), _boosters ? "[Boosters]" : "Boosters", _button)) ShowSection(true);
            if (GUI.Button(new Rect(20f + top * 2f, 20f, top - 8f, 56f), _shutterTubes > 0 ? "[Shutter]" : "Shutter", _button)) ShowShutters(1);
            if (GUI.Button(new Rect(20f + top * 3f, 20f, top - 8f, 56f), _win ? "[Win]" : "Win", _button)) ShowWin();
            if (GUI.Button(new Rect(20f + top * 4f, 20f, top - 8f, 56f), _obstacle == Obstacle.Hidden ? "[Hidden]" : "Hidden", _button))
                ShowObstacle(Obstacle.Hidden);
            if (GUI.Button(new Rect(20f + top * 5f, 20f, top - 8f, 56f), _obstacle == Obstacle.Frozen ? "[Frozen]" : "Frozen", _button))
                ShowObstacle(Obstacle.Frozen);
            if (_obstacle != Obstacle.None)
            {
                ObstacleGui(width);
                return;
            }
            if (_win)
            {
                WinGui(width);
                return;
            }
            if (_shutterTubes > 0)
            {
                ShutterGui(width);
                return;
            }
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

        private void WinGui(float width)
        {
            var y = GuiHeight - 200f;
            var third = (width - 40f) / 3f;
            var since = _wonAt < 0f ? "-" : $"{Time.time - _wonAt:0.00}s";
            GUI.Label(new Rect(20f, y - 76f, width - 40f, 36f), "Win: caps ripple, orbit sweep, burst, mascot, ribbon; the panel would open at the marked time", _label);
            GUI.Label(new Rect(20f, y - 40f, width - 40f, 36f), $"speed {Time.timeScale:0.##}x   t {since}   panel at {WinFx.PanelAt(_config, 4):0.00}s", _label);
            if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Replay", _button)) PlayWin();
            if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), _loop ? "[Loop]" : "Loop", _button)) _loop = !_loop;
            if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
        }

        private void ShutterGui(float width)
        {
            var y = GuiHeight - 270f;
            var third = (width - 40f) / 3f;
            var since = _openedAt < 0f ? "-" : $"{Time.time - _openedAt:0.00}s";
            GUI.Label(new Rect(20f, y - 76f, width - 40f, 36f), "Open: the key leaves its ball, holds at the centre, turns in the lock", _label);
            GUI.Label(new Rect(20f, y - 40f, width - 40f, 36f), $"speed {Time.timeScale:0.##}x   t {since}", _label);
            if (GUI.Button(new Rect(20f, y, third - 8f, 56f), _shutterTubes == 1 ? "[1 tube]" : "1 tube", _button)) ShowShutters(1);
            if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), _shutterTubes == 4 ? "[4 tubes]" : "4 tubes", _button)) ShowShutters(4);
            if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), _shutterTubes == 8 ? "[Tall]" : "Tall", _button)) ShowShutters(8);
            y += 64f;
            if (GUI.Button(new Rect(20f, y, third - 8f, 56f), "Open", _button)) OpenShutters();
            if (GUI.Button(new Rect(20f + third, y, third - 8f, 56f), "Reset", _button))
            {
                _loop = false;
                BuildShutterBoard();
            }
            if (GUI.Button(new Rect(20f + third * 2f, y, third - 8f, 56f), _loop ? "[Loop]" : "Loop", _button)) _loop = !_loop;
            y += 64f;
            // Key: closes the shutter again and replays the whole key flight at once.
            var half = (width - 40f) / 2f;
            if (GUI.Button(new Rect(20f, y, half - 8f, 56f), "Key", _button))
            {
                BuildShutterBoard();
                OpenShutters();
            }
            if (GUI.Button(new Rect(20f + half, y, half - 8f, 56f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
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
