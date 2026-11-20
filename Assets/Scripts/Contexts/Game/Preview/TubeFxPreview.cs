using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Core.Pooling;
using UnityEngine;

namespace Project.Contexts.Game.Preview
{
    // Debug scene driver: three real tubes, the middle one plays the full completion moment.
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

        private PoolService _pools;
        private ItemFactory _factory;
        private Transform _board;
        private readonly List<BottleItem> _tubes = new();
        private CompleteFx _fx;
        private int _size = 4;
        private bool _slow;
        private float _replayedAt = -1f;
        private GUIStyle _button, _label;

        private void Start()
        {
            _pools = new PoolService(new GameObject("Pool").transform);
            _factory = new ItemFactory(_pools, _registry);
            _board = new GameObject("Board").transform;
            Build();
        }

        private void OnDestroy()
        {
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

        private void OnGUI()
        {
            var scale = Screen.height / GuiHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 22 };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };

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
    }
}
