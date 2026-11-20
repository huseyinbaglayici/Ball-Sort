using System;
using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Additive poses from the tube's bottom; the root never moves so flying balls under it stay put.
    public sealed class TubeReactions
    {
        private const float LockedPressShare = 0.4f;
        // Share of the launch kick's drop the tube springs back up.
        private const float RecoilSpringBack = 0.3f;

        private readonly List<Reaction> _running = new();
        // Tilt in degrees around the tube's bottom, in the x of each pose.
        private readonly List<Reaction> _tilts = new();
        private readonly Transform _art;
        private readonly Vector3 _artHome;
        private readonly Transform _content;
        private bool _deformed;

        public TubeReactions(Transform art, Transform content)
        {
            _art = art;
            _artHome = art.localPosition;
            _content = content;
        }

        public void Select(BallAnimationConfig config) =>
            Add(0f, 1f, Vector4.zero,
                (Pose(config.SelectPress - Vector2.one, config.SelectDrop), config.SelectPressDuration, config.SquashCurve),
                (Vector4.zero, config.SelectReleaseDuration, config.RecoverCurve));

        public void Catch(BallAnimationConfig config, float delay) =>
            Add(delay, 1f, Vector4.zero,
                (Pose(config.CatchSquash - Vector2.one, 0f), config.CatchInDuration, config.SquashCurve),
                (Vector4.zero, config.CatchOutDuration, config.RecoverCurve));

        // Target can't take the selected balls.
        public void Reject(BallAnimationConfig config)
        {
            var amount = config.RejectShake;
            var cycles = config.RejectCycles;
            Add(0f, config.RejectDuration,
                u => new Vector4(0f, 0f, 0f, amount * Mathf.Sin(Mathf.PI * 2f * cycles * u) * (1f - u)));
        }

        // Tapped while empty, completed or busy.
        public void Locked(BallAnimationConfig config) =>
            Add(0f, 1f, Vector4.zero,
                (Pose(config.LockedPress - Vector2.one, 0f), config.LockedDuration * LockedPressShare, config.SquashCurve),
                (Vector4.zero, config.LockedDuration * (1f - LockedPressShare), config.RecoverCurve));

        // Later balls of the same move pass a lower strength.
        public void Launch(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (Pose(config.RecoilSquash - Vector2.one, config.RecoilDrop), config.RecoilHit, config.SquashCurve),
                (Pose(config.RecoilStretch - Vector2.one, -config.RecoilDrop * RecoilSpringBack), config.RecoilStretchDuration,
                    config.StretchCurve),
                (Vector4.zero, config.RecoilRecover, config.RecoverCurve));

        public void Swallow(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (new Vector4(config.SwallowWiden, 0f, 0f, 0f), config.SwallowIn, config.LiftCurve),
                (Vector4.zero, config.SwallowOut, config.RecoverCurve));

        public void Impact(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (Pose(config.ImpactSquash - Vector2.one, 0f), config.ImpactIn, config.SquashCurve),
                (Vector4.zero, config.ImpactOut, config.RecoverCurve));

        // Twitch and creep up, hop, then land with a squash and stretch.
        public void Complete(BallAnimationConfig config)
        {
            Add(0f, 1f, Vector4.zero,
                (Pose(Vector2.zero, -config.CompleteChargeRise), config.CompleteChargeDuration, config.CompleteChargeCurve),
                (Pose(Vector2.zero, -config.CompleteHop), config.CompleteRise, config.LiftCurve),
                (Vector4.zero, config.CompleteFall, config.SquashCurve),
                (Pose(config.CompleteSquash - Vector2.one, 0f), config.CompleteSquashIn, config.SquashCurve),
                (Pose(config.CompleteStretch - Vector2.one, 0f), config.CompleteStretchDuration, config.StretchCurve),
                (Vector4.zero, config.CompleteSquashOut, config.RecoverCurve));
            var loops = Mathf.Max(config.CompleteTwitchLoops, 0);
            var angle = config.CompleteTwitchAngle;
            AddTilt(config.CompleteTwitchStep * 4f * loops, u => angle * Mathf.Sin(Mathf.PI * 2f * loops * u));
        }

        // Call once per frame.
        public void Tick()
        {
            if (_running.Count == 0 && _tilts.Count == 0 && !_deformed) return;
            var now = Time.time;
            var pose = Sum(_running, now);
            Apply(pose, Sum(_tilts, now).x);
        }

        public void Clear()
        {
            _running.Clear();
            _tilts.Clear();
            Apply(Vector4.zero, 0f);
        }

        private static Vector4 Sum(List<Reaction> reactions, float now)
        {
            var sum = Vector4.zero;
            for (var i = reactions.Count - 1; i >= 0; i--)
            {
                var reaction = reactions[i];
                var u = (now - reaction.Start) / reaction.Length;
                if (u >= 1f)
                {
                    reactions.RemoveAt(i);
                    continue;
                }
                if (u > 0f) sum += reaction.At(u);
            }
            return sum;
        }

        private void AddTilt(float length, Func<float, float> degrees)
        {
            if (length <= 0f) return;
            _tilts.Add(new Reaction(Time.time, length, u => new Vector4(degrees(u), 0f, 0f, 0f)));
        }

        private static Vector4 Pose(Vector2 scaleOffset, float drop) => new(scaleOffset.x, scaleOffset.y, drop, 0f);

        // Steps through each pose with its own duration and curve; the last pose should be rest.
        private void Add(float delay, float strength, Vector4 from,
            params (Vector4 pose, float duration, AnimationCurve curve)[] steps)
        {
            if (strength <= 0.001f) return;
            var length = 0f;
            foreach (var step in steps) length += Mathf.Max(step.duration, 0f);
            Add(delay, length, u =>
            {
                var time = u * length;
                var previous = from;
                foreach (var step in steps)
                {
                    var duration = Mathf.Max(step.duration, 0f);
                    if (time <= duration && duration > 0f)
                        return Vector4.LerpUnclamped(previous, step.pose, step.curve.Evaluate(time / duration)) * strength;
                    time -= duration;
                    previous = step.pose;
                }
                return previous * strength;
            });
        }

        private void Add(float delay, float length, Func<float, Vector4> at)
        {
            if (length <= 0f) return;
            _running.Add(new Reaction(Time.time + Mathf.Max(delay, 0f), length, at));
        }

        private void Apply(Vector4 pose, float tilt)
        {
            var scale = new Vector3(1f + pose.x, 1f + pose.y, 1f);
            var rotation = Quaternion.Euler(0f, 0f, tilt);
            _art.localScale = scale;
            _art.localRotation = rotation;
            _art.localPosition = rotation * new Vector3(_artHome.x + pose.w, _artHome.y * scale.y - pose.z, _artHome.z);
            _content.localScale = scale;
            _content.localRotation = rotation;
            _content.localPosition = rotation * new Vector3(pose.w, -pose.z, 0f);
            _deformed = pose != Vector4.zero || tilt != 0f;
        }

        private readonly struct Reaction
        {
            public readonly float Start;
            public readonly float Length;
            public readonly Func<float, Vector4> At;

            public Reaction(float start, float length, Func<float, Vector4> at)
            {
                Start = start;
                Length = length;
                At = at;
            }
        }
    }
}
