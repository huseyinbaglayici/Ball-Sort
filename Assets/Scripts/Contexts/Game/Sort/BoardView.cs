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
    public sealed class BoardView : MonoBehaviour
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
        private const int TowerCapacity = 8;
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
        private readonly Dictionary<int, PortalView> _hintPortals = new();
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

        // An obstacle still showing over a tube that the model already opened locks it too.
        public bool IsLocked(int bottle) => _locks[bottle] > 0 || Time.time < _settledAt || _markers.IsCovered(bottle);

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
            _hintPortals.Clear();
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

        // Leftover lifted balls return silently; onDone is skipped if the last flight is cancelled (Undo, Restart).
        public void Transfer(int from, int to, int count, Action onDone, bool silent = false)
        {
            var source = _bottles[from];
            var target = _bottles[to];
            StopContacts();
            List<OrbContact> plan = null;
            if (!silent && count > 0)
            {
                // Must be planned before the push so the target is read as it was.
                var selected = Mathf.Clamp(Mathf.Max(source.LiftedCount, count), 1, OrbContacts.MaxBalls);
                var moved = Mathf.Min(count, selected);
                plan = OrbContacts.Plan(ContactKind.Transfer, selected, moved, empty: target.BallCount == 0,
                    tower: target.Capacity >= TowerCapacity);
            }
            if (count <= 0)
            {
                source.ReturnLifted(_animation);
                onDone?.Invoke();
                return;
            }

            _animation.EntryTiming(count, out _, out var stagger);
            var lastStart = (count - 1) * stagger;
            // Both portals share one shape so in matches out; the target closes PortalCloseLag later so the eye ends on it.
            var sourcePortal = PortalOver(source, facesDown: true);
            var targetPortal = PortalOver(target, facesDown: false);
            var allThrough = lastStart + _animation.WarpCross;
            var sourceView = NewPortal(sourcePortal, SourcePortalOrder, facesDown: true);
            // A Free Move hint portal already open over the target becomes the move's own.
            var targetView = _hintPortals.Remove(to, out var held) && !held.IsGone
                ? held
                : NewPortal(targetPortal, TargetPortalOrder, facesDown: false);
            HideTargets();
            sourceView.Close(allThrough);
            targetView.Close(allThrough + Mathf.Max(_animation.PortalCloseLag, 0f));
            for (var i = 0; i < count; i++)
            {
                var ball = source.Pop();
                target.Push(ball, snap: false);
                // A cancelled earlier flight may have left a stale contact for this ball.
                if (plan != null && i < plan.Count) _landings[ball] = (plan[i], i == plan.Count - 1);
                else _landings.Remove(ball);
                Fly(ball, from, to, sourcePortal, targetPortal, sourceView, targetView, i * stagger, i, count,
                    i == count - 1 ? onDone : null);
            }
            source.ReturnLifted(_animation);
        }

        // Free Move armed and balls lifted from: target tubes breathe in a tap-me idle with either portals or landing-slot rims.
        public void ShowTargets(int from, IReadOnlyList<int> targets)
        {
            HideTargets();
            var b = _animation.Boosters;
            foreach (var t in targets)
            {
                if (t < 0 || t >= _bottles.Count) continue;
                _idleTubes.Add(_bottles[t]);
                if (b.Hint == FreeMoveHint.Portals)
                {
                    _hintPortals[t] = NewPortal(PortalOver(_bottles[t], facesDown: false), TargetPortalOrder, facesDown: false);
                    continue;
                }
                var landing = _model != null ? _model.MoveAmount(from, t) : 0;
                var first = _bottles[t].BallCount;
                for (var slot = first; slot < first + landing && slot < _bottles[t].Capacity; slot++) Outline(_bottles[t], slot);
            }
            if (_idleTubes.Count > 0) StartIdle();
            Focus(from, targets);
        }

        // Targets' glass eases bright, the rest but the source eases dim, so the eye goes to where the balls can go.
        private void Focus(int from, IReadOnlyList<int> targets)
        {
            var b = _animation.Boosters;
            _focused = true;
            for (var t = 0; t < _bottles.Count; t++)
            {
                if (t == from) continue;
                var target = false;
                foreach (var candidate in targets) target |= candidate == t;
                _bottles[t].SetTint(target ? b.FocusTargetTint : b.FocusDimTint, b.FocusTime);
            }
        }

        public void HideTargets()
        {
            foreach (var portal in _hintPortals.Values) portal.Close();
            _hintPortals.Clear();
            StopIdle();
            FadeOutlines();
            if (!_focused) return;
            _focused = false;
            foreach (var bottle in _bottles) bottle.ResetTint(_animation.Boosters.FocusTime);
        }

        // Tap me: each target tube breathes very slightly; its balls a little more, bottom first, and the rims fade in and breathe on with them.
        private void StartIdle()
        {
            var b = _animation.Boosters;
            var outlines = _slotOutlines.ToArray();
            var slots = _slotOf.ToArray();
            var shown = 0f;
            PrimeTween.Tween.Custom(_idleClock, 0f, 1f, Mathf.Max(b.IdlePeriod, 0.05f), (_, k) =>
            {
                shown = Mathf.Min(1f, shown + Time.deltaTime / Mathf.Max(b.HintFadeTime, 0.0001f));
                var eased = Mathf.SmoothStep(0f, 1f, shown);
                for (var i = 0; i < outlines.Length; i++)
                {
                    if (outlines[i] == null) continue;
                    var breath = Breath(k - b.IdleBallPhase * slots[i]);
                    ApplyOutline(outlines[i], eased * (1f - b.SlotBreathAlpha * (1f - breath)), 1f + b.SlotBreathScale * breath);
                }
                foreach (var tube in _idleTubes)
                {
                    tube.transform.localScale = Vector3.one * (1f + b.IdleTubeBreath * Breath(k));
                    var balls = tube.Balls;
                    for (var i = 0; i < balls.Count; i++)
                        if (!_flights.ContainsKey(balls[i]))
                            balls[i].transform.localScale = Vector3.one * (1f + b.IdleBallBreath * Breath(k - b.IdleBallPhase * i));
                }
            }, PrimeTween.Ease.Linear, cycles: -1);
        }

        // 0 to 1 and back, eased at both ends.
        private static float Breath(float k) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * 2f * k);

        private void StopIdle()
        {
            PrimeTween.Tween.StopAll(_idleClock);
            foreach (var tube in _idleTubes)
            {
                if (tube == null) continue;
                tube.transform.localScale = Vector3.one;
                foreach (var ball in tube.Balls)
                    if (!_flights.ContainsKey(ball)) ball.transform.localScale = Vector3.one;
            }
            _idleTubes.Clear();
        }

        // An empty slot drawn like an orb waiting to land: violet inner glow lit from the lower right, crisp rim, gloss upper left.
        private void Outline(BottleItem tube, int slot)
        {
            var b = _animation.Boosters;
            var root = new GameObject("SlotRim");
            root.transform.SetParent(tube.transform, false);
            root.transform.localPosition = TubeArt.SlotLocalPosition(slot);
            OutlineLayer(root.transform, b.SlotInner, SlotInnerOrder, TubeArt.BallDiameter / SlotInnerEdge);
            OutlineLayer(root.transform, b.SlotRim, SlotRimOrder, TubeArt.BallDiameter / SlotRimEdge);
            OutlineLayer(root.transform, b.SlotGloss, SlotGlossOrder, TubeArt.BallDiameter / SlotInnerEdge);
            _slotOutlines.Add(root);
            _slotOf.Add(slot);
        }

        private static void OutlineLayer(Transform parent, Sprite sprite, int order, float size)
        {
            var go = new GameObject(sprite != null ? sprite.name : "SlotRimLayer");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<SpriteRenderer>();
            layer.sprite = sprite;
            layer.sortingOrder = order;
            layer.color = Color.clear;
            if (sprite != null) go.transform.localScale = Vector3.one * (size / sprite.bounds.size.x);
        }

        private void FadeOutlines()
        {
            if (_slotOutlines.Count == 0) return;
            var outlines = _slotOutlines.ToArray();
            _slotOutlines.Clear();
            _slotOf.Clear();
            foreach (var outline in outlines)
            {
                if (outline == null) continue;
                var from = outline.transform.localScale.x;
                PrimeTween.Tween.Custom(outline.transform, 1f, 0f, Mathf.Max(_animation.Boosters.HintFadeTime, 0.0001f),
                        (t, a) => ApplyOutline(t.gameObject, a, from), PrimeTween.Ease.InQuad)
                    .OnComplete(outline, gone => Destroy(gone));
            }
        }

        private void ClearOutlines()
        {
            foreach (var outline in _slotOutlines)
                if (outline != null) Destroy(outline);
            _slotOutlines.Clear();
            _slotOf.Clear();
        }

        private void ApplyOutline(GameObject outline, float alpha, float scale)
        {
            var b = _animation.Boosters;
            outline.transform.localScale = Vector3.one * scale;
            var layers = outline.GetComponentsInChildren<SpriteRenderer>();
            if (layers.Length < 3) return;
            layers[0].color = Fade(b.SlotInnerColor, alpha);
            layers[1].color = Fade(b.SlotRimColor, alpha);
            layers[2].color = Fade(b.SlotGlossColor, alpha);
        }

        private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

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

        // Portals act as one hole: both slices add up to one ball. Planned from the current pose so it never snaps.
        private void Fly(BallItem ball, int from, int to, Vector3 sourcePortal, Vector3 targetPortal, PortalView sourceView,
            PortalView targetView, float delay, int order, int count, Action onLanded)
        {
            CancelFlight(ball);
            _flights[ball] = new Vector2Int(from, to);
            _locks[from]++;
            _locks[to]++;
            _incoming[to]++;

            var source = _bottles[from];
            var target = _bottles[to];
            source.SetInMotion(true, _incoming[from] > 0);
            target.SetInMotion(true, receiving: true);
            var slot = target.IndexOf(ball);
            // Stays parented to the source tube until the hand-off so it draws under that tube's glass.
            ball.transform.SetParent(source.transform, true);
            // Start from the squeezed lift shape, not from round.
            var startScale = ball.transform.localScale;
            ball.StopMotion();
            ball.SetFlying(true);
            ball.SetHighlight(false, _animation);

            var config = _animation;
            var board = transform;
            var root = ball.transform;
            var radius = TubeArt.BallDiameter * 0.5f;
            var start = board.InverseTransformPoint(root.position);
            var end = board.InverseTransformPoint(target.SlotWorldPosition(slot));
            var mouthY = MouthY(target);
            var sourceMouthY = MouthY(source);
            config.EntryTiming(count, out var entry, out _);
            // Shape with the center on the portal planes; area kept.
            var deep = new Vector3(1f / config.VanishStretch, config.VanishStretch, 1f);
            // Above a cut line is hidden: source cuts at its void, target at its lowest edge, so what goes in comes out.
            var sourceCut = sourcePortal.y - PortalView.Cut(config, facesDown: true);
            var targetCut = targetPortal.y - PortalView.Cut(config, facesDown: false);
            var path = new WarpPath(start.y - sourceCut, radius, targetCut - radius - end.y, entry, config);
            var total = path.Duration;
            var previous = start;
            // Later balls of a move get weaker tube reactions.
            var strength = Mathf.Pow(Mathf.Clamp01(config.ReactionFalloff), order);
            var launched = false;
            var swallowed = false;

            PrimeTween.Tween.Custom(root,
                    new PrimeTween.TweenSettings<float>(0f, 1f,
                        new PrimeTween.TweenSettings(total, PrimeTween.Ease.Linear, startDelay: delay)),
                    (t, k) =>
                    {
                        var u = k * total;
                        Vector3 point;
                        var size = Vector3.one;
                        // Shape of the twin coming out of the target portal.
                        var outSize = Vector3.one;
                        var past = path.PastAt(u);
                        var twinY = targetCut - past;
                        if (!launched && sourceCut + past >= sourceMouthY)
                        {
                            launched = true;
                            source.Reactions.Launch(config, strength);
                        }
                        if (!swallowed && twinY <= mouthY)
                        {
                            swallowed = true;
                            target.Reactions.Swallow(config, strength);
                        }
                        if (past < 0f)
                        {
                            var w = Mathf.Clamp01(1f + past / radius);
                            size = Vector3.Scale(Vector3.LerpUnclamped(startScale, deep, w * w),
                                BallItem.NeckScale(sourceCut + past, sourceMouthY, config));
                            outSize = size;
                        }
                        else
                        {
                            var leave = Mathf.Clamp01(past / (radius * config.VanishStretch));
                            var round = PrimeTween.Easing.Evaluate(leave, PrimeTween.Ease.OutBack);
                            outSize = Vector3.Scale(Vector3.LerpUnclamped(deep, Vector3.one, round),
                                BallItem.NeckScale(twinY, mouthY, config));
                            // The tail keeps its entry shape: the hole pulls the ball, it doesn't stretch it.
                            size = deep;
                        }
                        var split = past < radius * outSize.y;
                        // Keep the tail's bottom at the twin's top so the two still add up to one ball.
                        if (split)
                            point = new Vector3(start.x, sourceCut + past + radius * (size.y - outSize.y), start.z);
                        else
                        {
                            // Hand-off to the target tube.
                            if (root.parent != target.transform) root.SetParent(target.transform, true);
                            point = new Vector3(end.x, twinY, end.z);
                            size = outSize;
                            // Smear only near the mouth, where the ball is fastest.
                            var nearMouth = Mathf.Abs(twinY - mouthY) < TubeArt.BallDiameter;
                            ball.SetMotionSmear(nearMouth ? (point - previous) * MouthSmear : Vector3.zero, config);
                        }
                        t.localScale = size;
                        t.position = board.TransformPoint(point);
                        // Measure the next step's smear from the twin, not from the root left at the source portal.
                        var twin = new Vector3(end.x, targetCut - past, end.z);
                        previous = split ? twin : point;
                        if (!split) ball.SetPortalTrail(targetView.OpenEdge);
                        else
                        {
                            var exitShape = new Vector3(outSize.x / size.x, outSize.y / size.y, 1f);
                            ball.SetPortalSplit(sourceView, board.TransformPoint(twin), exitShape, targetView,
                                target.transform);
                        }
                    })
                .OnComplete(() =>
                {
                    root.localScale = Vector3.one;
                    Land(ball, target, order, onLanded);
                });
        }

        private float MouthY(BottleItem bottle) =>
            transform.InverseTransformPoint(bottle.transform.TransformPoint(new Vector3(0f, bottle.MouthLocalY, 0f))).y;

        // Board-space ring center, with the ring's lowest point PortalHeight above the mouth.
        private Vector3 PortalOver(BottleItem bottle, bool facesDown)
        {
            var x = transform.InverseTransformPoint(bottle.transform.position).x;
            return new Vector3(x, MouthY(bottle) + _animation.PortalHeight + PortalView.Drop(_animation, facesDown), 0f);
        }

        private void Land(BallItem ball, BottleItem target, int order, Action onLanded)
        {
            CancelFlight(ball);
            var slot = target.IndexOf(ball);
            if (slot >= 0) ball.transform.localPosition = TubeArt.SlotLocalPosition(slot);
            ball.Snap(_animation);
            if (_landings.Remove(ball, out var landing)) PlayContact(landing.contact, landing.last);
            ImpactWave(target, slot);
            target.Reactions.Impact(_animation, Mathf.Pow(Mathf.Clamp01(_animation.ReactionFalloff), order));
            onLanded?.Invoke();
        }

        // A wave that meets a running one continues from its pose.
        private void ImpactWave(BottleItem bottle, int slot)
        {
            var strength = _animation.RippleStrength;
            for (var below = slot - 1; below >= 0 && strength > 0.005f; below--)
            {
                var ball = bottle.Balls[below];
                if (!_flights.ContainsKey(ball))
                    ball.Nudge(strength, _animation.RippleStep * (slot - 1 - below), _animation);
                strength *= _animation.RippleFalloff;
            }
        }

        private void PlayContact(OrbContact contact, bool last)
        {
            _audio?.PlayOneShotSound(contact.Sound, AudioManager.DecibelsToVolume(contact.GainDb));
            if (contact.Haptic) _haptics?.Haptic(last ? HapticModule.HapticType.Medium : HapticModule.HapticType.Light);
        }

        private void StopContacts() => PrimeTween.Tween.StopAll(_contactClock);

        // The caller stops the tween.
        private void CancelFlight(BallItem ball)
        {
            ball.SetFlying(false);
            ball.ClearPortalCut();
            if (!_flights.Remove(ball, out var flight)) return;
            // Stopped before the hand-off, the ball is still parented to the source tube.
            _bottles[flight.y].Adopt(ball);
            _locks[flight.x]--;
            _locks[flight.y]--;
            _incoming[flight.y]--;
            _bottles[flight.x].SetInMotion(_locks[flight.x] > 0, _incoming[flight.x] > 0);
            _bottles[flight.y].SetInMotion(_locks[flight.y] > 0, _incoming[flight.y] > 0);
        }

        private PortalView NewPortal(Vector3 center, int order, bool facesDown)
        {
            _portals.RemoveAll(portal => portal.IsGone);
            var view = new PortalView(transform, center, _animation, order, facesDown);
            view.Open();
            _portals.Add(view);
            return view;
        }
    }
}
