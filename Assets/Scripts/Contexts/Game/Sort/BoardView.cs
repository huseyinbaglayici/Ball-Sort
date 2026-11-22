using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Core.Audio;
using Project.Core.Haptics;
using Project.Core.Services;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Stacks follow the model at once, balls catch up by tween; only bottles with a ball in flight are locked.
    public sealed partial class BoardView : MonoBehaviour
    {
        // Tubes: idle 0, raised 10, receiving 11. Target portal is above all; source portal above idle, below raised.
        private const int TargetPortalOrder = 20;
        private const int SourcePortalOrder = 5;
        // The touchdown ring's far half draws under idle tubes, its near half over them and under raised ones.
        private const int TouchFarOrder = -1;
        private const int TouchNearOrder = 1;
        // Slot rims draw inside the tube's group like an orb would, under the front glass.
        private const int SlotInnerOrder = TubeArt.FrontOrder - 3;
        private const int SlotRimOrder = TubeArt.FrontOrder - 2;
        private const int SlotGlossOrder = TubeArt.FrontOrder - 1;
        // Edge of each slot sprite's art as a share of its half width, so all three match the orb's size.
        private const float SlotRimEdge = 0.94f;
        private const float SlotInnerEdge = 0.97f;
        private const float MouthSmear = 0.35f;
        [SerializeField] private BallAnimationConfig _animation;
        // The camera is fixed; the board scales to fit.
        [SerializeField] private BoardLayout _layout = new();

        private readonly List<BottleItem> _bottles = new();
        // (from, to) bottles each flying ball locks.
        private readonly Dictionary<BallItem, Vector2Int> _flights = new();
        // Contact to play on landing; absent for a silent move (Undo).
        private readonly Dictionary<BallItem, (OrbContact contact, bool last)> _landings = new();
        // Tween target for delayed lift sounds, so they can be stopped together.
        private readonly object _contactClock = new();
        private readonly List<float> _liftDelays = new();
        private int[] _locks = Array.Empty<int>();
        // A receiving bottle draws above the one the balls come from.
        private int[] _incoming = Array.Empty<int>();
        private ItemFactory _factory;
        private LevelPalette _palette;
        // Last model synced, for reveals that wait for a cover to go.
        private SortBoard _model;
        private AudioManager _audio;
        private HapticModule _haptics;
        private readonly List<PortalView> _portals = new();
        // Free Move hints over the tubes that can take the lifted balls.
        private readonly List<GameObject> _slotOutlines = new();
        // Slot of each rim, so it breathes with the ball that would sit there.
        private readonly List<int> _slotOf = new();
        // Glass focus is on while Free Move targets are shown.
        private bool _focused;
        private readonly List<BottleItem> _idleTubes = new();
        // Tween target of the tap-me idle, so it stops on its own.
        private readonly object _idleClock = new();
        private readonly List<TouchRingView> _rings = new();
        private readonly ObstacleMarkers _markers = new();
        private bool[] _capped = Array.Empty<bool>();
        // Completion effect still playing beside each bottle.
        private CompleteFx[] _fx = Array.Empty<CompleteFx>();
        // The win or fail beat still playing over the board, and the tube that completed last.
        private Item _result;
        private int _lastCompleted = -1;
        // Level grid positions, then capacities of every bottle (level ones first, added ones after).
        private readonly List<Vector2> _positions = new();
        private readonly List<int> _capacities = new();
        private int _levelNumber;
        private float _settledAt;
        private Action _restoreDescent;

        private void Awake()
        {
            // Optional so the debug preview can drive a board without the Boot services.
            Services.TryGet(out _audio);
            Services.TryGet(out _haptics);
        }

        // Also locked: a tube under a still-showing obstacle, and one the model has but the view has not placed yet.
        public bool IsLocked(int bottle) => bottle >= _locks.Length || _locks[bottle] > 0 || Time.time < _settledAt || _markers.IsCovered(bottle);

        public int BottleCount => _bottles.Count;

        // World point on a tube: x across its width (-0.5..0.5), y from its bottom (0) to its mouth (1).
        public Vector3 TubePoint(int bottle, float x, float y)
        {
            var tube = _bottles[bottle];
            return tube.transform.TransformPoint(new Vector3(x * tube.Size.x, y * tube.MouthLocalY));
        }

        public float WinPopupDelay => _animation.WinPopupDelay;
        public BallAnimationConfig Animation => _animation;

        // Free HUD band (0-1 of screen height) set before Build; null keeps the layout's fallback.
        public Vector2? ScreenBand { get; set; }

        public bool IsAnimating => _flights.Count > 0 || Time.time < _settledAt || _restoreDescent != null;

        public void Build(LevelData level, int levelNumber, SortBoard board, ItemFactory factory, LevelPalette palette)
        {
            Clear();
            _factory = factory;
            _palette = palette;
            _markers.Bind(factory, transform, _animation, palette, RevealUncovered);

            _positions.Clear();
            _capacities.Clear();
            for (var b = 0; b < level.Bottles.Count; b++)
            {
                _positions.Add(level.Bottles[b].Position);
                _capacities.Add(board.Capacity(b));
            }
            _levelNumber = levelNumber;
            _lastCompleted = -1;

            var camera = Camera.main;
            var origins = Place(out var rises, out var scale);
            var cameraCenter = camera != null ? camera.transform.position : Vector3.zero;
            transform.localScale = Vector3.one * scale;
            transform.position = new Vector3(cameraCenter.x, cameraCenter.y, 0f);

            _locks = new int[level.Bottles.Count];
            _incoming = new int[level.Bottles.Count];
            _capped = new bool[level.Bottles.Count];
            _fx = new CompleteFx[level.Bottles.Count];
            for (var b = 0; b < level.Bottles.Count; b++)
            {
                var bottle = _factory.Get<BottleItem>(ItemType.Bottle, transform);
                bottle.Setup(b, board.Capacity(b), BottleLocal(origins[b]), rises[b], _animation);
                _bottles.Add(bottle);

                for (var s = 0; s < board.Count(b); s++)
                {
                    var color = board.ColorAt(b, s);
                    var type = board.IsSpecial(b, s) ? ItemType.QuestionMark : ItemType.Ball;
                    var ball = _factory.Get<BallItem>(type, bottle.transform);
                    ball.Setup(color, _palette.GetOrb(color), _palette.Get(color), hidden: !board.IsKnown(b, s), _animation);
                    bottle.Push(ball, snap: true);
                }
            }

            for (var b = 0; b < _bottles.Count; b++)
                if (board.IsCompleted(b)) SetCompleted(b, true, celebrate: false);
            Sync(board);
        }

        // Level start and restart: tubes settle into their seats with a small rise and swell, left to right; barely seen.
        private const float EntranceRise = 0.08f;
        private const float EntranceScale = 0.96f;
        private const float EntranceTime = 0.2f;
        private const float EntranceStagger = 0.02f;

        // Taps wait for the last tube to settle.
        public void PlayEntrance()
        {
            if (_bottles.Count == 0) return;
            var order = new List<BottleItem>(_bottles);
            order.Sort((p, q) => p.transform.localPosition.x.CompareTo(q.transform.localPosition.x));
            for (var i = 0; i < order.Count; i++)
            {
                var root = order[i].transform;
                var seat = root.localPosition;
                var delay = i * EntranceStagger;
                root.localPosition = seat - Vector3.up * EntranceRise;
                root.localScale = Vector3.one * EntranceScale;
                PrimeTween.Tween.LocalPositionY(root, seat.y - EntranceRise, seat.y, EntranceTime, PrimeTween.Ease.OutCubic, startDelay: delay);
                PrimeTween.Tween.Scale(root, EntranceScale, 1f, EntranceTime, PrimeTween.Ease.OutCubic, startDelay: delay);
            }
            _settledAt = Time.time + (order.Count - 1) * EntranceStagger + EntranceTime;
        }

        // A board that slides in seats its tubes at once; taps wait the given time for it to arrive.
        public void SkipEntrance(float arrival)
        {
            foreach (var bottle in _bottles) PrimeTween.Tween.CompleteAll(bottle.transform);
            _settledAt = Time.time + arrival;
        }

        // Call only while no ball is flying.
        public void AddBottle(SortBoard board, int index, Action onPlaced)
        {
            Debug.Assert(index == _bottles.Count, "[BoardView] Bottles are added at the end.");
            _capacities.Add(board.Capacity(index));
            var origins = Place(out var rises, out var scale);
            var relayout = Mathf.Max(_animation.Boosters.RelayoutTime, 0.0001f);

            // Every tube slides at once, linearly; the view widens as a linear zoom, so the scale follows 1 / ortho.
            var camera = Camera.main;
            if (camera != null && !Mathf.Approximately(transform.localScale.x, scale))
            {
                var fromOrtho = camera.orthographicSize / transform.localScale.x;
                var toOrtho = camera.orthographicSize / scale;
                PrimeTween.Tween.Custom(transform, fromOrtho, toOrtho, relayout,
                    (board, ortho) => board.localScale = Vector3.one * (camera.orthographicSize / ortho), PrimeTween.Ease.Linear);
            }
            for (var b = 0; b < _bottles.Count; b++)
            {
                var target = BottleLocal(origins[b]);
                if (_bottles[b].transform.localPosition != target)
                    PrimeTween.Tween.LocalPosition(_bottles[b].transform, target, relayout, PrimeTween.Ease.Linear);
            }

            Array.Resize(ref _locks, index + 1);
            Array.Resize(ref _incoming, index + 1);
            Array.Resize(ref _capped, index + 1);
            Array.Resize(ref _fx, index + 1);
            var bottle = _factory.Get<BottleItem>(ItemType.Bottle, transform);
            bottle.Setup(index, board.Capacity(index), BottleLocal(origins[index]), rises[index], _animation);
            _bottles.Add(bottle);
            Descend(bottle, onPlaced);
            Sync(board);
        }

        // The new tube glides down from orbit on a soft arc, fading and growing in, then touches down on a thin ring.
        private void Descend(BottleItem bottle, Action onPlaced)
        {
            var b = _animation.Boosters;
            var root = bottle.transform;
            var seat = root.localPosition;
            // Comes in from the side away from the board's middle.
            var side = seat.x >= 0f ? b.DescentSide : -b.DescentSide;
            var from = seat + new Vector3(side, b.DescentHeight, 0f);
            // The settle closes AppearTime; the glide takes the rest.
            var time = Mathf.Max(b.AppearTime - b.LandSquashTime - b.LandSettleTime, 0.0001f);
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(false);
            var colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
            var fadeShare = Mathf.Max(b.DescentFadeShare, 0.0001f);
            // A pooled tube must not keep a faded colour if the board is cleared mid-descent.
            _restoreDescent = () =>
            {
                for (var i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].color = colors[i];
            };
            // Only the new tube waits for its landing; the rest are playable once the board has slid.
            _settledAt = Time.time + b.RelayoutTime;
            var index = bottle.Index;
            _locks[index]++;

            PrimeTween.Tween.Custom(root, 0f, 1f, time, (t, k) =>
                {
                    var x = PrimeTween.Easing.Evaluate(k, PrimeTween.Ease.OutCubic);
                    var y = PrimeTween.Easing.Evaluate(k, PrimeTween.Ease.OutSine);
                    t.localPosition = new Vector3(Mathf.LerpUnclamped(from.x, seat.x, x), Mathf.LerpUnclamped(from.y, seat.y, y), seat.z);
                    t.localScale = Vector3.one * Mathf.Lerp(b.DescentScaleFrom, 1f, x);
                    var fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / fadeShare));
                    for (var i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) renderers[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, colors[i].a * fade);
                }, PrimeTween.Ease.Linear)
                .OnComplete(() =>
                {
                    root.localPosition = seat;
                    _restoreDescent?.Invoke();
                    _restoreDescent = null;
                    var squash = new Vector3(b.LandSquash.x, b.LandSquash.y, 1f);
                    PrimeTween.Sequence.Create()
                        .Chain(PrimeTween.Tween.Scale(root, Vector3.one, squash, b.LandSquashTime, PrimeTween.Ease.OutQuad))
                        .Chain(PrimeTween.Tween.Scale(root, squash, Vector3.one, b.LandSettleTime, PrimeTween.Ease.OutBack));
                    _locks[index]--;
                    _rings.RemoveAll(ring => ring.IsGone);
                    var touch = new TouchRingView(transform, seat + Vector3.up * b.TouchRingY, b, TouchNearOrder, TouchFarOrder);
                    _rings.Add(touch);
                    touch.Play();
                    onPlaced?.Invoke();
                });
        }

        // Lays the board out again in place, for a new screen size or HUD band. Call only while nothing animates.
        public void Relayout()
        {
            if (_bottles.Count == 0) return;
            var origins = Place(out _, out var scale);
            transform.localScale = Vector3.one * scale;
            for (var b = 0; b < _bottles.Count; b++) _bottles[b].transform.localPosition = BottleLocal(origins[b]);
        }

        private Vector2[] Place(out float[] rises, out float scale)
        {
            var camera = Camera.main;
            var origins = _layout.Place(_positions, _capacities, _levelNumber, camera != null ? camera.aspect : 9f / 16f,
                out rises, out var orthoSize, ScreenBand);
            scale = camera != null ? camera.orthographicSize / orthoSize : 1f;
            return origins;
        }

        private static Vector3 BottleLocal(Vector2 origin) => origin + Vector2.up * TubeArt.BottomFromOrigin;

        public void Clear()
        {
            StopContacts();
            _landings.Clear();
            PrimeTween.Tween.StopAll(transform);
            foreach (var bottle in _bottles) PrimeTween.Tween.StopAll(bottle.transform);
            _restoreDescent?.Invoke();
            _restoreDescent = null;
            _settledAt = 0f;
            _markers.Clear();
            _model = null;
            Array.Clear(_capped, 0, _capped.Length);
            for (var b = 0; b < _fx.Length; b++) StopFx(b);
            StopResult();
            _flights.Clear();
            StopIdle();
            ClearOutlines();
            _focused = false;
            foreach (var portal in _portals) portal.Destroy();
            _portals.Clear();
            foreach (var ring in _rings) ring.Destroy();
            _rings.Clear();
            Array.Clear(_locks, 0, _locks.Length);
            Array.Clear(_incoming, 0, _incoming.Length);
            foreach (var bottle in _bottles)
            {
                while (bottle.BallCount > 0) _factory.Release(bottle.Pop());
                _factory.Release(bottle);
            }
            _bottles.Clear();
        }

        public void Select(int bottle, int count)
        {
            StopContacts();
            _liftDelays.Clear();
            _bottles[bottle].Select(count, _animation, _liftDelays);
            if (_liftDelays.Count == 0) return;
            var plan = OrbContacts.Plan(ContactKind.Select, Mathf.Min(_liftDelays.Count, OrbContacts.MaxBalls));
            for (var i = 0; i < plan.Count; i++)
            {
                var contact = plan[i];
                if (_liftDelays[i] <= 0f) PlayContact(contact, false);
                else PrimeTween.Tween.Delay(_contactClock, _liftDelays[i], () => PlayContact(contact, false));
            }
        }

        // Drops the lifted group back into its own tube; not a move.
        public void Deselect(int bottle)
        {
            StopContacts();
            var tube = _bottles[bottle];
            var lifted = tube.LiftedCount;
            if (lifted == 0)
            {
                tube.Deselect(_animation);
                return;
            }
            var plan = OrbContacts.Plan(ContactKind.Return, Mathf.Min(lifted, OrbContacts.MaxBalls));
            tube.Deselect(_animation, (index, count) =>
            {
                if (index < plan.Count) PlayContact(plan[index], index == count - 1);
            });
        }

        // Tapped as a target that can't take the selected balls.
        public void Reject(int bottle) => _bottles[bottle].Reactions.Reject(_animation);

        // Tapped while it can't be picked.
        public void Refuse(int bottle)
        {
            _bottles[bottle].Reactions.Locked(_animation);
            _markers.Rattle(bottle);
        }

        // celebrate: a move just completed it, so the charge, cap pop and side effect play.
        public void SetCompleted(int bottle, bool completed, bool celebrate = true)
        {
            if (_capped[bottle] == completed) return;
            _capped[bottle] = completed;
            if (completed) _lastCompleted = bottle;
            _bottles[bottle].SetCompleted(completed, celebrate);
            StopFx(bottle);
            if (completed && celebrate) PlayFx(bottle);
        }

        private void PlayFx(int bottle)
        {
            var fx = _factory.Get<CompleteFx>(ItemType.CompleteFx, transform);
            _fx[bottle] = fx;
            fx.Play(_bottles[bottle], _animation, done =>
            {
                if (_fx[bottle] == done) _fx[bottle] = null;
                _factory.Release(done);
            });
        }

        // The beat before the result panel, centred on the tubes and sized to their spread.
        public void PlayResult(bool won)
        {
            StopResult();
            if (_bottles.Count == 0) return;
            float left = float.MaxValue, right = float.MinValue, low = float.MaxValue, high = float.MinValue;
            foreach (var bottle in _bottles)
            {
                var p = bottle.transform.localPosition;
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
                low = Mathf.Min(low, p.y);
                high = Mathf.Max(high, p.y + bottle.MouthLocalY);
            }
            var centre = new Vector3((left + right) * 0.5f, (low + high) * 0.5f, 0f);
            var width = right - left + TubeArt.Size.x;
            if (won)
            {
                var fx = Seat(_factory.Get<WinFx>(ItemType.WinFx, transform), centre);
                fx.Play(_bottles, _lastCompleted, width, WinPopupDelay, _animation, done => Released(done));
            }
            else
            {
                var fx = Seat(_factory.Get<ResultFx>(ItemType.ResultFx, transform), centre);
                fx.Play(width, _animation, done => Released(done));
            }
        }

        private T Seat<T>(T fx, Vector3 centre) where T : Item
        {
            fx.transform.localPosition = centre;
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one;
            _result = fx;
            return fx;
        }

        private void Released(Item done)
        {
            if (_result == done) _result = null;
            _factory.Release(done);
        }

        private void StopResult()
        {
            if (_result == null) return;
            var fx = _result;
            _result = null;
            _factory.Release(fx);
        }

        private void StopFx(int bottle)
        {
            var fx = _fx[bottle];
            if (fx == null) return;
            _fx[bottle] = null;
            _factory.Release(fx);
        }

        // Syncs all but ball positions; obstacles a move removes stay until PlayObstacles (null move: show at once).
        public void Sync(SortBoard board, MoveResult move = null)
        {
            _model = board;
            _markers.Refresh(board, _bottles, move);
            for (var b = 0; b < _bottles.Count; b++)
            {
                var balls = _bottles[b].Balls;
                // A ball under a cover being removed is revealed once the cover is gone.
                var covered = _markers.IsCovered(b);
                for (var s = 0; s < balls.Count && s < board.Count(b); s++)
                {
                    if (balls[s].IsHidden && board.IsKnown(b, s) && !covered) Reveal(b, s);
                    else if (!balls[s].IsHidden && !board.IsKnown(b, s)) balls[s].Hide();
                }
                if (_capped[b] && !board.IsCompleted(b)) SetCompleted(b, false);
            }
        }

        // The move's flights have landed: its melts, shutter openings, used keys and defuses play.
        public void PlayObstacles(MoveResult move) => _markers.Play(move);

        // A fuse ran out.
        public void Explode() => _markers.Explode();

        public void Reveal(int bottle, int slot)
        {
            var ball = _bottles[bottle].Balls[slot];
            ball.Reveal(_palette.GetOrb(ball.ColorIndex), _palette.Get(ball.ColorIndex), _animation);
        }

        private void RevealUncovered(int bottle)
        {
            if (_model == null || bottle >= _bottles.Count) return;
            var balls = _bottles[bottle].Balls;
            for (var s = 0; s < balls.Count && s < _model.Count(bottle); s++)
                if (balls[s].IsHidden && _model.IsKnown(bottle, s)) Reveal(bottle, s);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void AssertMatches(SortBoard board)
        {
            for (var b = 0; b < _bottles.Count; b++)
            {
                var balls = _bottles[b].Balls;
                if (balls.Count != board.Count(b))
                {
                    Debug.LogError($"[BoardView] Bottle {b} shows {balls.Count} balls, the model has {board.Count(b)}.");
                    continue;
                }
                // A ball under a cover being removed waits for it to go before it is revealed.
                var covered = _markers.IsCovered(b);
                for (var s = 0; s < balls.Count; s++)
                    if (balls[s].ColorIndex != board.ColorAt(b, s) ||
                        balls[s].IsHidden == board.IsKnown(b, s) && !(covered && balls[s].IsHidden))
                        Debug.LogError($"[BoardView] Bottle {b} slot {s} does not match the model.");
            }
        }
    }
}
