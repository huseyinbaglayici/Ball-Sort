using System;
using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// The tube's reactions to what happens to it (selected, refused, a ball leaving, entering, landing, completed):
    /// short poses that add up instead of cutting each other. They move the art and the resting balls together,
    /// squashing from the tube's bottom; the tube root never moves, so a ball in flight under it is not pulled around.
    /// A pose is an offset from rest: (scale x, scale y, drop, sideways shift).
    /// </summary>
    public sealed class TubeReactions
    {
        // Share of the locked press spent pressing; the rest is the release.
        private const float LockedPressShare = 0.4f;
        // After the launch kick the tube springs up by this share of the kick's drop.
        private const float RecoilSpringBack = 0.3f;

        private readonly List<Reaction> _running = new();
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

        /// <summary>Tapped to pick its balls: a quick press down, as if pushing them up, and a release.</summary>
        public void Select(BallAnimationConfig config) =>
            Add(0f, 1f, Vector4.zero,
                (Pose(config.SelectPress - Vector2.one, config.SelectDrop), config.SelectPressDuration, config.SquashCurve),
                (Vector4.zero, config.SelectReleaseDuration, config.RecoverCurve));

        /// <summary>Its lifted balls settle back in after <paramref name="delay"/>: a small catch.</summary>
        public void Catch(BallAnimationConfig config, float delay) =>
            Add(delay, 1f, Vector4.zero,
                (Pose(config.CatchSquash - Vector2.one, 0f), config.CatchInDuration, config.SquashCurve),
                (Vector4.zero, config.CatchOutDuration, config.RecoverCurve));

        /// <summary>Tapped as a target that cannot take the selected balls: a short sideways "no".</summary>
        public void Reject(BallAnimationConfig config)
        {
            var amount = config.RejectShake;
            var cycles = config.RejectCycles;
            Add(0f, config.RejectDuration,
                u => new Vector4(0f, 0f, 0f, amount * Mathf.Sin(Mathf.PI * 2f * cycles * u) * (1f - u)));
        }

        /// <summary>Tapped while it cannot be picked (empty, completed or busy): a small press, nothing more.</summary>
        public void Locked(BallAnimationConfig config) =>
            Add(0f, 1f, Vector4.zero,
                (Pose(config.LockedPress - Vector2.one, 0f), config.LockedDuration * LockedPressShare, config.SquashCurve),
                (Vector4.zero, config.LockedDuration * (1f - LockedPressShare), config.RecoverCurve));

        /// <summary>
        /// A ball shoots out of the mouth: the tube kicks back down and wide, springs up and settles. Later balls of the
        /// same move kick weaker (<paramref name="strength"/>).
        /// </summary>
        public void Launch(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (Pose(config.RecoilSquash - Vector2.one, config.RecoilDrop), config.RecoilHit, config.SquashCurve),
                (Pose(config.RecoilStretch - Vector2.one, -config.RecoilDrop * RecoilSpringBack), config.RecoilStretchDuration,
                    config.StretchCurve),
                (Vector4.zero, config.RecoilRecover, config.RecoverCurve));

        /// <summary>A ball goes into the mouth: the tube widens for a moment, like a gulp.</summary>
        public void Swallow(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (new Vector4(config.SwallowWiden, 0f, 0f, 0f), config.SwallowIn, config.LiftCurve),
                (Vector4.zero, config.SwallowOut, config.RecoverCurve));

        /// <summary>A ball lands in its slot: a small impact squash. Later balls of the same move land softer.</summary>
        public void Impact(BallAnimationConfig config, float strength) =>
            Add(0f, strength, Vector4.zero,
                (Pose(config.ImpactSquash - Vector2.one, 0f), config.ImpactIn, config.SquashCurve),
                (Vector4.zero, config.ImpactOut, config.RecoverCurve));

        /// <summary>Completed: after the last impact settles, a small victory hop, landing with a squash.</summary>
        public void Complete(BallAnimationConfig config) =>
            Add(config.CompleteDelay, 1f, Vector4.zero,
                (Pose(Vector2.zero, -config.CompleteHop), config.CompleteRise, config.LiftCurve),
                (Vector4.zero, config.CompleteFall, config.SquashCurve),
                (Pose(config.CompleteSquash - Vector2.one, 0f), config.CompleteSquashIn, config.SquashCurve),
                (Vector4.zero, config.CompleteSquashOut, config.RecoverCurve));

        /// <summary>Applies the sum of the running reactions; finished ones drop out. Call once per frame.</summary>
        public void Tick()
        {
            if (_running.Count == 0 && !_deformed) return;
            var now = Time.time;
            var pose = Vector4.zero;
            for (var i = _running.Count - 1; i >= 0; i--)
            {
                var reaction = _running[i];
                var u = (now - reaction.Start) / reaction.Length;
                if (u >= 1f)
                {
                    _running.RemoveAt(i);
                    continue;
                }
                if (u > 0f) pose += reaction.At(u);
            }
            Apply(pose);
        }

        /// <summary>Stops every reaction and puts the tube back at rest.</summary>
        public void Clear()
        {
            _running.Clear();
            Apply(Vector4.zero);
        }

        private static Vector4 Pose(Vector2 scaleOffset, float drop) => new(scaleOffset.x, scaleOffset.y, drop, 0f);

        /// <summary>
        /// One reaction, starting after <paramref name="delay"/>: from <paramref name="from"/> through each pose in turn,
        /// every step with its own time and curve; the last pose should be rest. Scaled by <paramref name="strength"/>.
        /// </summary>
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

        /// <summary>
        /// Deforms the art and the resting balls from the tube's bottom: scaled, pushed down by the drop and shifted
        /// sideways.
        /// </summary>
        private void Apply(Vector4 pose)
        {
            var scale = new Vector3(1f + pose.x, 1f + pose.y, 1f);
            _art.localScale = scale;
            _art.localPosition = new Vector3(_artHome.x + pose.w, _artHome.y * scale.y - pose.z, _artHome.z);
            _content.localScale = scale;
            _content.localPosition = new Vector3(pose.w, -pose.z, 0f);
            _deformed = pose != Vector4.zero;
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
