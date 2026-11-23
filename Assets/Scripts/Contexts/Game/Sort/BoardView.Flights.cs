using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Core.Audio;
using Project.Core.Haptics;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Ball flights between tubes: through the warp portals, landing contacts and cancels.
    public sealed partial class BoardView
    {
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
                    tower: target.Capacity >= LevelMechanics.TowerCapacity);
            }
            if (count <= 0)
            {
                source.ReturnLifted(_animation);
                onDone?.Invoke();
                return;
            }

            _animation.EntryTiming(count, out _, out var stagger);
            var lastStart = (count - 1) * stagger;
            // Both portals share one shape; the target closes PortalCloseLag later so the eye ends on it.
            var sourcePortal = PortalOver(source, facesDown: true);
            var targetPortal = PortalOver(target, facesDown: false);
            var allThrough = lastStart + _animation.WarpCross;
            var sourceView = NewPortal(sourcePortal, SourcePortalOrder, facesDown: true);
            var targetView = NewPortal(targetPortal, TargetPortalOrder, facesDown: false);
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
            // Hidden above the cut: the source cuts at its void, the target at its lowest edge, so in matches out.
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
