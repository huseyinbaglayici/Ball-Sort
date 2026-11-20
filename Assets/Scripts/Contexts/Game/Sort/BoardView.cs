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
        private readonly ObstacleMarkers _markers = new();
        private bool[] _capped = Array.Empty<bool>();
        // Completion effect still playing beside each bottle.
        private CompleteFx[] _fx = Array.Empty<CompleteFx>();
        // Level grid positions, then capacities of every bottle (level ones first, added ones after).
        private readonly List<Vector2> _positions = new();
        private readonly List<int> _capacities = new();
        private int _levelNumber;
        // Relayout time after a bottle is added during play.
        private const float AddDuration = 0.3f;
        private float _settledAt;

        private void Awake()
        {
            _audio = Services.Get<AudioManager>();
            _haptics = Services.Get<HapticModule>();
        }

        // An obstacle still showing over a tube that the model already opened locks it too.
        public bool IsLocked(int bottle) => _locks[bottle] > 0 || Time.time < _settledAt || _markers.IsCovered(bottle);

        public float WinPopupDelay => _animation.WinPopupDelay;

        public bool IsAnimating => _flights.Count > 0 || Time.time < _settledAt;

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

        // Call only while no ball is flying.
        public void AddBottle(SortBoard board, int index, Action onPlaced)
        {
            Debug.Assert(index == _bottles.Count, "[BoardView] Bottles are added at the end.");
            _capacities.Add(board.Capacity(index));
            var origins = Place(out var rises, out var scale);

            if (transform.localScale != Vector3.one * scale)
                PrimeTween.Tween.Scale(transform, Vector3.one * scale, AddDuration, PrimeTween.Ease.OutCubic);
            for (var b = 0; b < _bottles.Count; b++)
            {
                var target = BottleLocal(origins[b]);
                if (_bottles[b].transform.localPosition != target)
                    PrimeTween.Tween.LocalPosition(_bottles[b].transform, target, AddDuration, PrimeTween.Ease.OutCubic);
            }

            Array.Resize(ref _locks, index + 1);
            Array.Resize(ref _incoming, index + 1);
            Array.Resize(ref _capped, index + 1);
            Array.Resize(ref _fx, index + 1);
            var bottle = _factory.Get<BottleItem>(ItemType.Bottle, transform);
            bottle.Setup(index, board.Capacity(index), BottleLocal(origins[index]), rises[index], _animation);
            _bottles.Add(bottle);
            _settledAt = Time.time + AddDuration;
            PrimeTween.Tween.Scale(bottle.transform, 0f, 1f, AddDuration, PrimeTween.Ease.OutBack)
                .OnComplete(() => onPlaced?.Invoke());
            Sync(board);
        }

        private Vector2[] Place(out float[] rises, out float scale)
        {
            var camera = Camera.main;
            var origins = _layout.Place(_positions, _capacities, _levelNumber, camera != null ? camera.aspect : 9f / 16f,
                out rises, out var orthoSize);
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
            _settledAt = 0f;
            _markers.Clear();
            _model = null;
            Array.Clear(_capped, 0, _capped.Length);
            for (var b = 0; b < _fx.Length; b++) StopFx(b);
            _flights.Clear();
            foreach (var portal in _portals) portal.Destroy();
            _portals.Clear();
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
            var targetView = NewPortal(targetPortal, TargetPortalOrder, facesDown: false);
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

        // celebrate: a move just completed it, so the charge, cap pop and side effect play.
        public void SetCompleted(int bottle, bool completed, bool celebrate = true)
        {
            if (_capped[bottle] == completed) return;
            _capped[bottle] = completed;
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
            _audio.PlayOneShotSound(contact.Sound, AudioManager.DecibelsToVolume(contact.GainDb));
            if (contact.Haptic) _haptics.Haptic(last ? HapticModule.HapticType.Medium : HapticModule.HapticType.Light);
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
