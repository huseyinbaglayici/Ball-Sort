using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Disk beads speed up through the charge and are swallowed at the slam; only dust and glow play out after.
    public sealed class AccretionDisk : MonoBehaviour
    {
        private const int TrailPoints = 20;
        private const float FadeIn = 0.25f;
        // Widest at this share of the trail, narrowing under the bead so the strip has no hard end.
        private const float TrailPeak = 0.75f;
        private const float TrailHeadWidth = 0.45f;
        // The dust and glow materials double their colour, so they get about half of it.
        private const float DoubledShare = 0.55f;
        // Three rings at these shares of the disk radius, a few degrees apart, each turning at its own speed.
        private const float RingInner = 0.8f;
        private const float RingStep = 0.12f;
        private const float RingTiltStep = 4f;
        private static readonly float[] RingSpeed = { 1f, 1.22f, 0.86f };
        private static readonly float[] RingPhase = { 0f, 2.1f, 4f };

        [SerializeField] private Transform _back;
        [SerializeField] private Transform _front;
        [SerializeField] private SpriteRenderer[] _cores;
        [SerializeField] private SpriteRenderer[] _glows;
        [SerializeField] private LineRenderer[] _trailsFront;
        [SerializeField] private LineRenderer[] _trailsBack;
        [SerializeField] private ParticleSystem _dustFront;
        [SerializeField] private ParticleSystem _dustBack;
        [SerializeField] private ParticleSystem _glow;

        private readonly float[] _turn = new float[3];
        private readonly float[] _tilts = new float[3];
        private Vector3 _center;
        private Vector3[] _last;
        private float[] _travel;
        private readonly Vector3[] _points = new Vector3[TrailPoints];
        private readonly bool[] _near = new bool[TrailPoints];
        private readonly AnimationCurve _trailShape = new AnimationCurve();
        private readonly Keyframe[] _twoKeys = new Keyframe[2];
        private readonly Keyframe[] _threeKeys = new Keyframe[3];
        private FinishFxSettings _s;
        private float _slam;
        private float _previous;

        public bool IsAlive => _dustFront.IsAlive() || _dustBack.IsAlive() || _glow.IsAlive();

        public void Play(FinishFxSettings settings, float slam, float mouth)
        {
            Stop();
            _s = settings;
            _slam = slam;
            _previous = 0f;
            for (var r = 0; r < _turn.Length; r++)
            {
                _turn[r] = RingPhase[r];
                _tilts[r] = settings.DiskTilt + (r - 1) * RingTiltStep;
            }
            if (_travel == null || _travel.Length != _cores.Length)
            {
                _travel = new float[_cores.Length];
                _last = new Vector3[_cores.Length];
            }
            for (var i = 0; i < _travel.Length; i++)
            {
                _travel[i] = 0f;
                _last[i] = new Vector3(float.NaN, 0f, 0f);
            }
            var glow = _glow.main;
            glow.startDelay = Mathf.Max(0f, slam - settings.GlowLead);
            glow.startColor = Color.Lerp(settings.Deep, settings.Violet, 0.5f) * DoubledShare;
            _glow.transform.localPosition = new Vector3(0f, mouth * 0.5f, 0f);
            _glow.Play();
        }

        // The disk sits at center with this radius; after the slam the beads are gone.
        public void Tick(float t, Vector3 center, float radius)
        {
            var dt = Mathf.Max(0f, t - _previous);
            _previous = t;
            _center = center;
            var swallowed = t >= _slam || radius < 0.01f;
            var speed = Mathf.Lerp(_s.ChargeSpeedFrom, _s.ChargeSpeedTo, EaseInQuad(Mathf.Clamp01(t / _slam))) * _s.AccretionSpin;
            var alpha = Mathf.Clamp01(t / FadeIn);
            var perRing = _cores.Length / _turn.Length;
            for (var r = 0; r < _turn.Length; r++)
            {
                _turn[r] += speed * RingSpeed[r] * dt;
                var ringRadius = radius * (RingInner + RingStep * r);
                for (var k = 0; k < perRing; k++)
                {
                    var i = r * perRing + k;
                    if (swallowed)
                    {
                        _cores[i].enabled = _glows[i].enabled = _trailsFront[i].enabled = _trailsBack[i].enabled = false;
                        continue;
                    }
                    var angle = _turn[r] + Mathf.PI * 2f * k / perRing;
                    var position = Point(r, angle, ringRadius);
                    var near = Mathf.Sin(angle) < 0f;
                    PlaceBead(i, position, near, alpha);
                    DrawTrail(i, r, angle, ringRadius, speed * RingSpeed[r], alpha);
                    Dust(i, position, near, alpha);
                }
            }
        }

        public void Stop()
        {
            foreach (var core in _cores) core.enabled = false;
            foreach (var glow in _glows) glow.enabled = false;
            foreach (var line in _trailsFront) line.enabled = false;
            foreach (var line in _trailsBack) line.enabled = false;
            _dustFront.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _dustBack.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Ellipse in perspective; the near half (lower) is drawn in front, the far half behind.
        private Vector3 Point(int ring, float angle, float radius)
        {
            var flat = new Vector3(radius * Mathf.Cos(angle), radius * _s.DiskAspect * Mathf.Sin(angle), 0f);
            return _center + Quaternion.Euler(0f, 0f, _tilts[ring]) * flat;
        }

        private void PlaceBead(int i, Vector3 position, bool near, float alpha)
        {
            var core = _cores[i];
            var glow = _glows[i];
            core.enabled = glow.enabled = true;
            var group = near ? _front : _back;
            if (core.transform.parent != group) core.transform.SetParent(group, false);
            if (glow.transform.parent != group) glow.transform.SetParent(group, false);
            // A touch smaller on the far side, for depth.
            var depth = (near ? 1f : 0.82f) * _s.DiskBeadScale;
            core.transform.localPosition = glow.transform.localPosition = position;
            core.transform.localScale = Vector3.one * (_s.BeadSize * depth);
            glow.transform.localScale = Vector3.one * (_s.BeadGlowSize * depth);
            core.color = new Color(_s.Core.r, _s.Core.g, _s.Core.b, alpha);
            glow.color = new Color(_s.Violet.r, _s.Violet.g, _s.Violet.b, alpha * _s.DiskAuraAlpha);
        }

        // Past positions along the same ring, split where it passes behind the sphere.
        private void DrawTrail(int i, int ring, float angle, float radius, float speed, float alpha)
        {
            var front = _trailsFront[i];
            var back = _trailsBack[i];
            var arc = Mathf.Min(speed * _s.TrailTime, _s.TrailMaxArc);
            if (alpha <= 0f || arc < 0.05f || radius < 0.02f)
            {
                front.enabled = back.enabled = false;
                return;
            }
            for (var j = 0; j < TrailPoints; j++)
            {
                var a = angle - arc * (1f - (float)j / (TrailPoints - 1));
                _points[j] = Point(ring, a, radius);
                _near[j] = Mathf.Sin(a) < 0f;
            }
            // The arc is shorter than half a turn, so it crosses the edge at most once.
            var split = 1;
            while (split < TrailPoints && _near[split] == _near[0]) split++;
            var tailLine = _near[0] ? front : back;
            var headLine = _near[0] ? back : front;
            if (split >= TrailPoints)
            {
                headLine.enabled = false;
                Segment(tailLine, 0, TrailPoints - 1, alpha, radius);
                return;
            }
            Segment(tailLine, 0, split, alpha, radius);
            Segment(headLine, split, TrailPoints - 1, alpha, radius);
        }

        // Tail is deep indigo and thin; the head is hot white-cyan. Small rings get narrower trails.
        private void Segment(LineRenderer line, int from, int to, float alpha, float radius)
        {
            var count = to - from + 1;
            line.enabled = count >= 2;
            if (!line.enabled) return;
            line.positionCount = count;
            for (var j = 0; j < count; j++) line.SetPosition(j, transform.TransformPoint(_points[from + j]));
            float f0 = (float)from / (TrailPoints - 1), f1 = (float)to / (TrailPoints - 1);
            if (f0 < TrailPeak && f1 > TrailPeak)
            {
                _threeKeys[0] = new Keyframe(0f, TrailShape(f0));
                _threeKeys[1] = new Keyframe((TrailPeak - f0) / (f1 - f0), 1f);
                _threeKeys[2] = new Keyframe(1f, TrailShape(f1));
                _trailShape.keys = _threeKeys;
            }
            else
            {
                _twoKeys[0] = new Keyframe(0f, TrailShape(f0));
                _twoKeys[1] = new Keyframe(1f, TrailShape(f1));
                _trailShape.keys = _twoKeys;
            }
            for (var k = 0; k < _trailShape.length; k++) _trailShape.SmoothTangents(k, 0f);
            line.widthCurve = _trailShape;
            line.widthMultiplier = _s.TrailWidth * transform.lossyScale.x * Mathf.Clamp01(radius / 0.6f + 0.35f);
            line.startColor = TrailColor(f0, alpha);
            line.endColor = TrailColor(f1, alpha);
        }

        private static float TrailShape(float f) =>
            f < TrailPeak ? f / TrailPeak : 1f - (1f - TrailHeadWidth) * (f - TrailPeak) / (1f - TrailPeak);

        private Color TrailColor(float f, float alpha)
        {
            var color = f < 0.5f ? Color.Lerp(_s.Deep, _s.Violet, f * 2f) : Color.Lerp(_s.Violet, _s.Core, (f - 0.5f) * 2f);
            color.a = alpha * Mathf.Sqrt(f);
            return color;
        }

        // Soft star dust shed along the rings, rising as it fades; a few grains are gold.
        private void Dust(int i, Vector3 position, bool near, float alpha)
        {
            if (float.IsNaN(_last[i].x))
            {
                _last[i] = position;
                return;
            }
            _travel[i] += (position - _last[i]).magnitude;
            _last[i] = position;
            var system = near ? _dustFront : _dustBack;
            var spacing = _s.DiskDustSpacing;
            for (var n = 0; n < 2 && _travel[i] >= spacing; n++)
            {
                _travel[i] -= spacing;
                var outward = new Vector3(position.x - _center.x, 0f, 0f).normalized;
                var pick = Random.value;
                var color = pick < _s.GoldShare ? _s.Gold : pick < _s.GoldShare + 0.25f ? _s.Core : pick < 0.8f ? _s.Violet : Color.Lerp(_s.Deep, _s.Violet, 0.5f);
                color *= DoubledShare;
                color.a = alpha;
                var emit = new ParticleSystem.EmitParams
                {
                    position = position + (Vector3)(Random.insideUnitCircle * 0.06f),
                    velocity = outward * Random.Range(0.03f, 0.12f) + Vector3.up * Random.Range(0.5f, 1.1f),
                    startColor = color,
                    startSize = Random.Range(0.1f, 0.2f),
                    startLifetime = Random.Range(0.55f, 0.9f),
                    applyShapeToPosition = false,
                };
                system.Emit(emit, 1);
            }
            if (_travel[i] > spacing) _travel[i] = spacing;
        }

        private static float EaseInQuad(float u) => u * u;
    }
}
