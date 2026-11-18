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
    /// <summary>
    /// Shows a <see cref="SortBoard"/>: spawns the bottles and ball units from the pool, scales the layout
    /// to fit the screen and animates moves. It never decides what is allowed, SortBoard does.
    /// The bottle stacks follow the model at once; only the ball positions catch up through tweens.
    /// A bottle is locked while a ball flies into or out of it; the rest of the board stays playable.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        // The portal's layers sort above every tube group (idle 0, raised 10, receiving 11), so balls pass under them.
        private const int PortalBackOrder = 20;
        // How fast the charging ball shivers sideways, in radians per second.
        private const float ChargeShiverRate = 180f;
        // Share of its frame step a ball smears by while it passes the target mouth.
        private const float MouthSmear = 0.35f;

        [SerializeField] private BallAnimationConfig _animation;
        // The camera is fixed, so the board is scaled to the size the layout asks of it.
        [SerializeField] private BoardLayout _layout = new();

        private readonly List<BottleItem> _bottles = new();
        // The bottles each flying ball locks: (from, to).
        private readonly Dictionary<BallItem, Vector2Int> _flights = new();
        private int[] _locks = Array.Empty<int>();
        // Balls flying into each bottle; a receiving bottle draws above the one they come from.
        private int[] _incoming = Array.Empty<int>();
        private ItemFactory _factory;
        private LevelPalette _palette;
        private AudioService _audio;
        private HapticService _haptics;
        // Portals still open or closing over source and target tubes; cleared with the board.
        private readonly List<PortalView> _portals = new();

        public BottleItem Bottle(int index) => _bottles[index];

        public bool IsLocked(int bottle) => _locks[bottle] > 0;

        public bool IsAnimating => _flights.Count > 0;

        public void Build(LevelData level, SortBoard board, ItemFactory factory, LevelPalette palette)
        {
            Clear();
            _factory = factory;
            _palette = palette;
            _audio ??= Services.Get<AudioService>();
            if (_haptics == null) Services.TryGet(out _haptics);

            var camera = Camera.main;
            var origins = _layout.Place(level, camera != null ? camera.aspect : 9f / 16f, out var orthoSize);
            var scale = camera != null ? camera.orthographicSize / orthoSize : 1f;
            var cameraCenter = camera != null ? camera.transform.position : Vector3.zero;
            transform.localScale = Vector3.one * scale;
            transform.position = new Vector3(cameraCenter.x, cameraCenter.y, 0f);

            _locks = new int[level.Bottles.Count];
            _incoming = new int[level.Bottles.Count];
            for (var b = 0; b < level.Bottles.Count; b++)
            {
                var bottle = _factory.Get<BottleItem>(ItemType.Bottle, transform);
                bottle.Setup(b, level.Capacity, origins[b] + Vector2.up * TubeArt.BottomFromOrigin);
                _bottles.Add(bottle);

                for (var s = 0; s < board.Count(b); s++)
                {
                    var color = board.ColorAt(b, s);
                    var type = board.IsSpecial(b, s) ? ItemType.QuestionMark : ItemType.Ball;
                    var ball = _factory.Get<BallItem>(type, bottle.transform);
                    ball.Setup(color, _palette.GetOrb(color), hidden: !board.IsKnown(b, s));
                    bottle.Push(ball, snap: true);
                }
            }
        }

        /// <summary>Stops every motion and returns every spawned item to the pool.</summary>
        public void Clear()
        {
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

        public void Select(int bottle, int count) => _bottles[bottle].Select(count, _animation);

        public void Deselect(int bottle) => _bottles[bottle].Deselect(_animation);

        /// <summary>The tube was tapped as a target that cannot take the selected balls.</summary>
        public void Reject(int bottle) => _bottles[bottle].Reactions.Reject(_animation);

        /// <summary>The tube was tapped while it cannot be picked.</summary>
        public void Refuse(int bottle) => _bottles[bottle].Reactions.Locked(_animation);

        /// <summary>
        /// Moves the top <paramref name="count"/> ball units of one bottle onto another. The top ball leaves first and
        /// goes to the first free slot; the next ones leave while it is still flying. Balls still lifted in the origin
        /// (a partial transfer) return to their slots. <paramref name="onDone"/> runs when the last ball lands,
        /// and never if that flight is cancelled (Undo, Restart).
        /// </summary>
        public void Transfer(int from, int to, int count, Action onDone)
        {
            var source = _bottles[from];
            var target = _bottles[to];
            if (count <= 0)
            {
                source.ReturnLifted(_animation);
                onDone?.Invoke();
                return;
            }

            // One entry window for the whole group, whatever its size: each ball's entry and the delay to the next one.
            _animation.EntryTiming(count, out _, out var stagger);
            var lastStart = (count - 1) * stagger;
            // Two portals per move, joined, one over each mouth. Both open on this tap, together, while the balls charge
            // (the selection opens none), and close once the last ball of the move is through. They open with the same
            // shape at the same time, and each one hides the balls only where it is drawn, so what goes into one comes out
            // of the other even while they are still opening.
            var sourcePortal = PortalOver(source);
            var targetPortal = PortalOver(target);
            var allThrough = lastStart + _animation.WarpCharge + _animation.WarpCross;
            var sourceView = NewPortal(sourcePortal);
            var targetView = NewPortal(targetPortal);
            sourceView.Close(allThrough);
            targetView.Close(allThrough);
            for (var i = 0; i < count; i++)
            {
                var ball = source.Pop();
                target.Push(ball, snap: false);
                Fly(ball, from, to, sourcePortal, targetPortal, sourceView, targetView, i * stagger, i, count,
                    i == count - 1 ? onDone : null);
            }
            source.ReturnLifted(_animation);
        }

        public void SetCompleted(int bottle, bool completed) => _bottles[bottle].SetCompleted(completed, _animation);

        public void Reveal(int bottle, int slot)
        {
            var ball = _bottles[bottle].Balls[slot];
            ball.Reveal(_palette.GetOrb(ball.ColorIndex));
        }

        /// <summary>Editor check: the bottle stacks hold exactly the model's units.</summary>
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
                for (var s = 0; s < balls.Count; s++)
                    if (balls[s].ColorIndex != board.ColorAt(b, s) || balls[s].IsHidden == board.IsKnown(b, s))
                        Debug.LogError($"[BoardView] Bottle {b} slot {s} does not match the model.");
            }
        }

        /// <summary>
        /// One ball's move: a short charge, then a warp jump up into the source portal and down out of the target portal
        /// in one motion (see <see cref="WarpPath"/>). The two portals are one hole, so whatever share of the ball has
        /// gone into the first is already out of the second, and the two slices always add up to one ball. Out of the
        /// target portal it keeps falling through the narrow mouth, long and thin, straight into its slot, and stops hard
        /// on the balls below. Planned from where the ball is now, so a half-finished lift or a reversed flight never
        /// snaps. The portals are board-space ring centers (see <see cref="PortalOver"/>).
        /// </summary>
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
            // The target already lists the ball, but until it is through the portals it is still in the source tube, so
            // it draws there, under that tube's glass, and only shows over it once it is out of the mouth. It moves to
            // the target tube at the hand-off; its twin out of the target portal draws in the target tube meanwhile.
            ball.transform.SetParent(source.transform, true);
            // A ball lifted into the mouth is squeezed there; the charge starts from that shape, not from round.
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
            var chargeTime = config.WarpCharge;
            // Halfway through the hole (its center on the portal planes) the ball is this tall and thin (area kept).
            var deep = new Vector3(1f / config.VanishStretch, config.VanishStretch, 1f);
            var charged = new Vector3(config.ChargeSquash.x, config.ChargeSquash.y, 1f);
            // Each portal splits the ball at its ring's lowest edge (the bottom of the side wall), the ring's silhouette:
            // the ring draws over the ball, so whatever is above that edge is hidden by it. Splitting there keeps the two
            // parts adding up to one ball on screen: the share the source ring hides is the share the target ring shows.
            var sourceCut = sourcePortal.y - PortalView.Drop(config);
            var targetCut = targetPortal.y - PortalView.Drop(config);
            var path = new WarpPath(start.y - sourceCut, radius, targetCut - radius - end.y, entry, config);
            var total = chargeTime + path.Duration;
            var through = false;
            var previous = start;
            // The tubes react to this ball as it leaves one mouth and enters the other; later balls of a move weaker.
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
                        // How far the center is past the source cut line (negative before it); the twin's center is as
                        // far below the target cut line.
                        var split = false;
                        var past = 0f;
                        if (u < chargeTime)
                        {
                            // Anticipation: the ball gathers (grows a little) and shivers under the opening portal.
                            var c = u / Mathf.Max(chargeTime, 0.0001f);
                            point = start + new Vector3(Mathf.Sin(u * ChargeShiverRate) * config.ChargeShiver * c, 0f, 0f);
                            size = Vector3.LerpUnclamped(startScale, charged, c);
                        }
                        else
                        {
                            past = path.PastAt(u - chargeTime);
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
                                // Going in: it stretches toward the plane, squeezed while it leaves its own mouth.
                                var w = Mathf.Clamp01(1f + past / radius);
                                size = Vector3.Scale(Vector3.LerpUnclamped(charged, deep, w * w),
                                    BallItem.NeckScale(sourceCut + past, sourceMouthY, config));
                            }
                            else
                            {
                                // Coming out: round again (with a small squash past round), squeezed in the target mouth.
                                var leave = Mathf.Clamp01(past / (radius * config.VanishStretch));
                                var round = PrimeTween.Easing.Evaluate(leave, PrimeTween.Ease.OutBack);
                                size = Vector3.Scale(Vector3.LerpUnclamped(deep, Vector3.one, round),
                                    BallItem.NeckScale(twinY, mouthY, config));
                            }
                            // Until the twin is all out, the ball's tail is still going into the source portal.
                            split = past < radius * size.y;
                            if (split) point = new Vector3(start.x, sourceCut + past, start.z);
                            else
                            {
                                // Hand-off: the ball is through the portals and goes on in the target tube.
                                if (root.parent != target.transform) root.SetParent(target.transform, true);
                                point = new Vector3(end.x, twinY, end.z);
                                if (!through && twinY < mouthY - radius)
                                {
                                    through = true;
                                    _haptics?.Play(HapticStrength.Light);
                                }
                                // It smears while it passes the mouth, where it is fastest and squeezed.
                                var nearMouth = Mathf.Abs(twinY - mouthY) < TubeArt.BallDiameter;
                                ball.SetMotionSmear(nearMouth ? (point - previous) * MouthSmear : Vector3.zero, config);
                            }
                        }
                        t.localScale = size;
                        t.position = board.TransformPoint(point);
                        // While split, the ball that goes on is the twin out of the target portal, so the next step
                        // (and its smear) is measured from there, not from the root left at the source portal.
                        var twin = new Vector3(end.x, targetCut - past, end.z);
                        previous = split ? twin : point;
                        if (!split) ball.ClearPortalCut();
                        else
                        {
                            // Each ring hides the ball only where it is drawn now: its edge follows its opening.
                            ball.SetPortalSplit(sourceView.Edge, board.TransformPoint(twin), targetView.Edge,
                                target.transform);
                        }
                    })
                .OnComplete(() =>
                {
                    root.localScale = Vector3.one;
                    Land(ball, target, order, onLanded);
                });
        }

        /// <summary>Board-space y of a tube's mouth.</summary>
        private float MouthY(BottleItem bottle) =>
            transform.InverseTransformPoint(bottle.transform.TransformPoint(new Vector3(0f, TubeArt.MouthLocalY, 0f))).y;

        /// <summary>
        /// Board-space center of the portal ring over a tube (the center of the face the balls cross), the same for both
        /// ends of a move: it sits just over the mouth, its lowest point <see cref="BallAnimationConfig.PortalHeight"/>
        /// above it, like a cap. A leaving ball rises out of its tube up into the ring's opening; an arriving one comes
        /// down out of it straight into the mouth.
        /// </summary>
        private Vector3 PortalOver(BottleItem bottle)
        {
            var x = transform.InverseTransformPoint(bottle.transform.position).x;
            return new Vector3(x, MouthY(bottle) + _animation.PortalHeight + PortalView.Drop(_animation), 0f);
        }

        private void Land(BallItem ball, BottleItem target, int order, Action onLanded)
        {
            CancelFlight(ball);
            var slot = target.IndexOf(ball);
            if (slot >= 0) ball.transform.localPosition = TubeArt.SlotLocalPosition(slot);
            ball.Snap(_animation);
            _haptics?.Play(HapticStrength.Medium);
            ImpactWave(target, slot);
            // The tube takes the hit: the first ball of a move lands hardest.
            target.Reactions.Impact(_animation, Mathf.Pow(Mathf.Clamp01(_animation.ReactionFalloff), order));
            _audio?.PlaySfx(_animation.LandSound, 1f + order * _animation.LandPitchStep);
            onLanded?.Invoke();
        }

        /// <summary>
        /// The new ball sits on the ones below: each of them plays a small copy of the tube squash, one after another
        /// from top to bottom, weaker further down. A wave that meets a running one continues from its pose.
        /// </summary>
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

        /// <summary>Releases the locks of a ball's running flight. Its tween is stopped by the caller.</summary>
        private void CancelFlight(BallItem ball)
        {
            ball.SetFlying(false);
            ball.ClearPortalCut();
            if (!_flights.Remove(ball, out var flight)) return;
            // A flight stopped before its hand-off leaves the ball in the source tube; it belongs to the target.
            _bottles[flight.y].Adopt(ball);
            _locks[flight.x]--;
            _locks[flight.y]--;
            _incoming[flight.y]--;
            _bottles[flight.x].SetInMotion(_locks[flight.x] > 0, _incoming[flight.x] > 0);
            _bottles[flight.y].SetInMotion(_locks[flight.y] > 0, _incoming[flight.y] > 0);
        }

        /// <summary>
        /// Opens a portal centered on <paramref name="center"/> (board space), on the plane that slices the balls. It
        /// draws over every tube and ball, so a ball rising up into it disappears behind its lower edge.
        /// </summary>
        private PortalView NewPortal(Vector3 center)
        {
            _portals.RemoveAll(portal => portal.IsGone);
            var view = new PortalView(transform, center, _animation, PortalBackOrder);
            view.Open();
            _portals.Add(view);
            return view;
        }
    }
}
