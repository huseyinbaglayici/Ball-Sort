using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Singularity above the mouth that draws each orb's essence, then drops into the gem when the cap seats.
    public sealed class CompleteFx : Item
    {
        private const int HalfSegments = 24;
        private const int WispPoints = 14;
        private const float PulseTime = 0.45f;
        private const float GemSquashTime = 0.28f;
        // Long enough for the ripple, glint and dust to play out after the slam.
        private const float AfterSlam = 1.2f;
        private const float MinLength = 2.2f;

        [SerializeField] private Transform _front;
        [SerializeField] private AccretionDisk _disk;
        // Sphere layers: violet halo, hot rim, dark body, fresnel rim, gloss and photon ring.
        [SerializeField] private SpriteRenderer _lens;
        [SerializeField] private SpriteRenderer _heat;
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _rim;
        [SerializeField] private SpriteRenderer _gloss;
        [SerializeField] private SpriteRenderer _photon;
        [SerializeField] private LineRenderer _diskBack;
        [SerializeField] private LineRenderer _diskFront;
        // One essence per orb: its glow copy, its bead core and its short trail.
        [SerializeField] private SpriteRenderer[] _soulGlows;
        [SerializeField] private SpriteRenderer[] _soulCores;
        [SerializeField] private LineRenderer[] _wisps;
        // Closing beat: event horizon ring, ripple down the glass, residual glow and glint on the gem.
        [SerializeField] private LineRenderer _ringBack;
        [SerializeField] private LineRenderer _ringFront;
        [SerializeField] private LineRenderer _rippleBack;
        [SerializeField] private LineRenderer _rippleFront;
        [SerializeField] private SpriteRenderer _halo;
        [SerializeField] private SpriteRenderer _glint;

        private static readonly AnimationCurve EvenWidth = AnimationCurve.Constant(0f, 1f, 1f);
        // Tail at nothing, swelling to full width at the head.
        private static readonly AnimationCurve TaperWidth = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private static readonly AnimationCurve ArcWidth = new(new Keyframe(0f, 0.25f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.25f));

        private readonly Gradient _arcGradient = new();
        private readonly GradientColorKey[] _arcColors = new GradientColorKey[3];
        private readonly GradientAlphaKey[] _arcAlphas = new GradientAlphaKey[3];
        private BottleItem _tube;
        private FinishFxSettings _s;
        private Action<CompleteFx> _onFinished;
        private bool _playing;
        private bool _slammed;
        private bool _gemTouched;
        private float _start;
        private float _slam;
        private float _settled;
        private float _end;
        private float _mouth;
        private Vector3 _gemAtSlam;

        // Works in tube coordinates (the tube's spot and scale); onFinished runs once nothing is left.
        public void Play(BottleItem tube, BallAnimationConfig config, Action<CompleteFx> onFinished)
        {
            HideAll();
            transform.SetPositionAndRotation(tube.transform.position, tube.transform.rotation);
            transform.localScale = tube.transform.localScale;
            _tube = tube;
            _s = config.Finish;
            _onFinished = onFinished;
            _slam = config.CompleteChargeDuration + config.CapPopDuration + config.CapSlamDuration;
            _settled = _slam + config.CapSquashIn + config.CapSettle;
            _end = Mathf.Max(_slam + AfterSlam, MinLength);
            _mouth = tube.MouthLocalY;
            _disk.Play(_s, _slam, _mouth);
            _slammed = false;
            _start = Time.time;
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            var t = Time.time - _start;
            if (_tube == null || (t >= _end && !_disk.IsAlive))
            {
                Finish();
                return;
            }
            if (t >= _end) return;
            if (t < _slam)
            {
                Charge(t);
                return;
            }
            if (!_slammed)
            {
                _slammed = true;
                _gemAtSlam = new Vector3(0f, GemLocal().y, 0f);
                HideSphere();
            }
            _disk.Tick(t, _gemAtSlam, 0f);
            Close(t - _slam, t);
        }

        // The singularity forms with a slight overshoot, inhales each essence and drops into the gem with a squash.
        private void Charge(float t)
        {
            var s = _s;
            var collapse = Mathf.Clamp01((t - (_slam - s.CollapseTime)) / Mathf.Max(s.CollapseTime, 0.01f));
            var grow = Mathf.Clamp01((t - s.FormStart) / Mathf.Max(s.FormTime, 0.01f));
            var form = Mathf.Max(0f, EaseOutBack(grow));
            var swallow = EaseInCubic(collapse);
            var center = Vector3.Lerp(new Vector3(0f, _mouth + s.SingularityHeight, 0f), GemLocal(), swallow);
            // Each essence arrival: a quick contraction, then a pulse, a small growth step and a tighter ring.
            var taken = Swallowed(t, out var pulse, out var contract);
            var share = (float)taken / Mathf.Max(1, _tube.Balls.Count);
            var fed = 1f + s.SwallowGrowth * share + s.SwallowPulse * pulse - s.SwallowContract * contract;
            var size = s.SingularitySize * form * (1f - swallow) * fed;
            var shown = Mathf.Clamp01(grow * 3f);
            // Wider and flatter as it drops in.
            var squash = new Vector3(1f + 0.5f * collapse, 1f - 0.45f * collapse, 1f);
            var heat = EaseInQuad(Mathf.Clamp01(t / _slam));
            Show(_lens, center, Vector3.one * (size * s.LensSize), WithAlpha(s.Violet, s.LensAlpha * shown));
            Show(_heat, center, squash * (size * s.HeatSize), WithAlpha(Color.Lerp(s.Heat, s.HeatCore, 0.3f * heat), s.HeatAlpha * shown));
            Show(_core, center, squash * size, WithAlpha(s.Body, 0.96f * shown));
            Show(_rim, center, squash * size, WithAlpha(Color.Lerp(s.Violet, s.Core, 0.45f), s.RimAlpha * shown));
            Show(_gloss, center, squash * size, WithAlpha(Color.white, s.GlossAlpha * shown));
            var photon = Color.Lerp(Color.Lerp(s.HeatCore, s.Core, 0.5f), Color.white, Mathf.Clamp01(pulse));
            Show(_photon, center, squash * (size * s.PhotonSize * (1f - s.PhotonTighten * share)),
                WithAlpha(photon, Mathf.Min(1f, s.PhotonAlpha * shown + 0.5f * pulse)));
            _photon.transform.localRotation = Quaternion.Euler(0f, 0f, -s.PhotonSpin * t);
            var radius = size * 0.5f * s.DiskSize;
            DrawDisk(center, radius, squash, shown, heat);
            Souls(t, center);
            _disk.Tick(t, center, radius);
        }

        // One thin ring from the gem, a faint ripple down the glass, a soft glow and a glint once the cap settles.
        private void Close(float since, float t)
        {
            var s = _s;
            var u = since / s.HorizonDuration;
            _ringBack.enabled = _ringFront.enabled = u < 1f;
            if (u < 1f)
            {
                var spread = EaseOutQuart(u);
                var radius = Mathf.Lerp(s.HorizonFrom, s.HorizonTo, spread);
                var line = Mathf.Lerp(s.HorizonLineFrom, s.HorizonLineTo, spread);
                var color = WithAlpha(Color.Lerp(s.Core, s.Violet, u), (1f - u) * (1f - u));
                DrawEllipse(_ringBack, _gemAtSlam, radius, radius * s.DiskAspect, 0f, Mathf.PI, line, color);
                DrawEllipse(_ringFront, _gemAtSlam, radius, radius * s.DiskAspect, Mathf.PI, Mathf.PI * 2f, line, color);
            }

            var r = (since - s.RippleDelay) / Mathf.Max(s.RippleDuration, 0.01f);
            _rippleBack.enabled = _rippleFront.enabled = r > 0f && r < 1f;
            if (r > 0f && r < 1f)
            {
                var y = Mathf.Lerp(_mouth - 0.25f, TubeArt.SlotLocalPosition(0).y, EaseInOutSine(r));
                var center = new Vector3(0f, y, 0f);
                var color = WithAlpha(Color.Lerp(s.Violet, s.Core, 0.35f), s.RippleAlpha * Mathf.Sin(Mathf.PI * r));
                DrawEllipse(_rippleBack, center, s.RippleRadius, s.RippleRadius * 0.25f, 0f, Mathf.PI, s.RippleLine, color);
                DrawEllipse(_rippleFront, center, s.RippleRadius, s.RippleRadius * 0.25f, Mathf.PI, Mathf.PI * 2f, s.RippleLine, color);
            }

            GemPulse(t - _settled, Color.Lerp(s.Core, s.Gold, 0.35f));
            var g = (t - _settled) / Mathf.Max(s.GlintTime, 0.01f);
            _glint.enabled = g > 0f && g < 1f;
            if (_glint.enabled)
            {
                var pop = Mathf.Sin(Mathf.PI * EaseOutCubic(g));
                Show(_glint, GemLocal(), Vector3.one * (s.GlintSize * pop), WithAlpha(s.Core, pop));
            }
        }

        // Far half sits behind the sphere, thinner and dimmer; the near half crosses in front, widest at the front.
        private void DrawDisk(Vector3 center, float radius, Vector3 squash, float alpha, float heat)
        {
            var s = _s;
            var rx = radius * squash.x;
            var ry = radius * s.DiskAspect * squash.y;
            var near = Color.Lerp(s.Heat, s.HeatCore, 0.3f + 0.6f * heat);
            DrawArc(_diskBack, center, rx, ry, 0f, Mathf.PI, s.DiskWidth * 0.55f,
                WithAlpha(s.Deep, 0f), WithAlpha(Color.Lerp(s.Violet, s.Heat, 0.4f), 0.5f * alpha));
            DrawArc(_diskFront, center, rx, ry, Mathf.PI, Mathf.PI * 2f, s.DiskWidth,
                WithAlpha(s.Violet, 0.15f * alpha), WithAlpha(near, alpha));
        }

        // Half a tilted ellipse, thin and in the edge colour at its ends, wide and in the middle colour at its middle.
        private void DrawArc(LineRenderer line, Vector3 center, float rx, float ry, float from, float to, float width,
            Color edge, Color middle)
        {
            line.enabled = rx > 0.005f && middle.a > 0.002f;
            if (!line.enabled) return;
            var rotation = Quaternion.Euler(0f, 0f, _s.DiskTilt);
            line.positionCount = HalfSegments + 1;
            for (var i = 0; i <= HalfSegments; i++)
            {
                var angle = Mathf.Lerp(from, to, (float)i / HalfSegments);
                line.SetPosition(i, transform.TransformPoint(center + rotation * new Vector3(rx * Mathf.Cos(angle), ry * Mathf.Sin(angle), 0f)));
            }
            line.widthCurve = ArcWidth;
            line.widthMultiplier = width * transform.lossyScale.x;
            _arcColors[0] = new GradientColorKey(edge, 0f);
            _arcColors[1] = new GradientColorKey(middle, 0.5f);
            _arcColors[2] = new GradientColorKey(edge, 1f);
            _arcAlphas[0] = new GradientAlphaKey(edge.a, 0f);
            _arcAlphas[1] = new GradientAlphaKey(middle.a, 0.5f);
            _arcAlphas[2] = new GradientAlphaKey(edge.a, 1f);
            _arcGradient.SetKeys(_arcColors, _arcAlphas);
            line.colorGradient = _arcGradient;
        }

        // Bottom first, a ghost of each orb's glow rises from its core and arcs into the sphere.
        private void Souls(float t, Vector3 target)
        {
            var s = _s;
            var balls = _tube.Balls;
            for (var i = 0; i < _wisps.Length; i++)
            {
                var line = _wisps[i];
                var glow = _soulGlows[i];
                var core = _soulCores[i];
                var u = i < balls.Count ? (t - s.DrainStart(i, balls.Count)) / Mathf.Max(s.WispTime, 0.01f) : -1f;
                if (u <= 0f || u >= 1f + s.WispLag)
                {
                    line.enabled = glow.enabled = core.enabled = false;
                    continue;
                }
                var orb = balls[i];
                var side = i % 2 == 0 ? 1f : -1f;
                var from = transform.InverseTransformPoint(orb.transform.position);
                from.z = 0f;
                var bend = new Vector3(side * s.WispSide, Mathf.Lerp(from.y, target.y, 0.6f), 0f);
                var light = Color.Lerp(orb.Color, Color.white, s.SoulLift);

                var peel = Mathf.Clamp01(u / s.SoulPeel);
                var fly = Mathf.Clamp01((u - s.SoulPeel) / (1f - s.SoulPeel));
                var k = EaseInCubic(fly);
                var lift = Vector3.up * (s.SoulRise * EaseOutCubic(peel) * (1f - fly));
                var head = Bezier(from, bend, target, k) + lift;
                var ahead = Bezier(from, bend, target, Mathf.Min(1f, k + 0.05f)) - Bezier(from, bend, target, k);
                var size = TubeArt.BallDiameter * Mathf.Lerp(Mathf.Lerp(s.SoulStartSize, s.SoulSize, EaseOutCubic(peel)), s.SoulEndSize, k);
                var stretch = 1f + s.SoulStretch * k * (1f - k * 0.3f);
                var alive = u < 1f;
                glow.enabled = core.enabled = alive;
                if (alive)
                {
                    var angle = fly > 0f ? Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg : 90f;
                    var shape = new Vector3(size * stretch, size / Mathf.Sqrt(stretch), 1f);
                    glow.sprite = orb.GlowSprite;
                    // The glow copy is sized to the orb's own glow art; the bead core is a unit sprite.
                    var art = glow.sprite != null ? glow.sprite.bounds.size.x : 1f;
                    glow.transform.localPosition = core.transform.localPosition = head;
                    glow.transform.localRotation = core.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    glow.transform.localScale = shape * (1f / Mathf.Max(art, 0.01f));
                    core.transform.localScale = shape * s.SoulCore;
                    var fade = Mathf.Clamp01(EaseOutCubic(peel) * 1.5f) * (1f - Mathf.Clamp01((u - 0.9f) / 0.1f))
                        * s.SoulAlpha * Mathf.Lerp(1f, s.SoulEndAlpha, k);
                    glow.color = WithAlpha(light, fade);
                    core.color = WithAlpha(Color.Lerp(light, Color.white, 0.5f), fade * 0.8f);
                }

                // The trail starts when the essence leaves the orb.
                var tail = EaseInQuad(Mathf.Clamp01(fly - s.WispLag));
                line.enabled = fly > 0f && k - tail > 0.01f;
                if (!line.enabled) continue;
                line.positionCount = WispPoints;
                for (var j = 0; j < WispPoints; j++)
                    line.SetPosition(j, transform.TransformPoint(Bezier(from, bend, target, Mathf.Lerp(tail, k, (float)j / (WispPoints - 1)))));
                line.widthCurve = TaperWidth;
                line.widthMultiplier = s.WispWidth * transform.lossyScale.x * Mathf.Lerp(1f, 0.55f, k);
                line.startColor = WithAlpha(light, 0f);
                line.endColor = WithAlpha(Color.Lerp(light, s.Core, 0.4f * k),
                    s.SoulAlpha * Mathf.Lerp(1f, s.SoulEndAlpha, k) * (1f - Mathf.Clamp01((u - 0.9f) / (0.1f + s.WispLag))));
            }
        }

        // Essences absorbed so far, the lingering pulse of recent ones and the pull toward the next.
        private int Swallowed(float t, out float pulse, out float contract)
        {
            var s = _s;
            var count = _tube.Balls.Count;
            var taken = 0;
            pulse = contract = 0f;
            for (var i = 0; i < count; i++)
            {
                var since = t - (s.DrainStart(i, count) + s.WispTime);
                if (since < 0f)
                {
                    if (since > -s.SwallowLead) contract = Mathf.Max(contract, Mathf.Sin(Mathf.PI * 0.5f * (1f + since / s.SwallowLead)));
                    continue;
                }
                taken++;
                pulse += Mathf.Exp(-since / Mathf.Max(s.SwallowTime, 0.01f));
            }
            pulse = Mathf.Min(pulse, 1.5f);
            return taken;
        }

        // Soft glow swells from the gem and fades while the gem gives a small squash and recovers.
        private void GemPulse(float since, Color color)
        {
            var u = since / PulseTime;
            _halo.enabled = u > 0f && u < 1f;
            if (_halo.enabled)
            {
                _halo.transform.localPosition = GemLocal();
                _halo.transform.localScale = Vector3.one * (_s.ResidualSize * TubeArt.Size.x * (0.5f + 0.5f * EaseOutCubic(u)));
                color.a = _s.ResidualStrength * Mathf.Min(1f, u * 8f) * (1f - u) * (1f - u);
                _halo.color = color;
            }
            var g = since / GemSquashTime;
            if (g <= 0f) return;
            var gem = _tube.Gem;
            if (g >= 1f)
            {
                if (_gemTouched) gem.localScale = Vector3.one;
                _gemTouched = false;
                return;
            }
            var squash = Mathf.Sin(Mathf.PI * g) * (1f - g);
            gem.localScale = new Vector3(1f + 0.14f * squash, 1f - 0.12f * squash, 1f);
            _gemTouched = true;
        }

        // Points in this effect's local space, drawn in world space.
        private void DrawEllipse(LineRenderer line, Vector3 center, float rx, float ry, float from, float to, float width, Color color)
        {
            var segments = Mathf.RoundToInt(HalfSegments * (to - from) / Mathf.PI);
            line.positionCount = segments + 1;
            for (var i = 0; i <= segments; i++)
            {
                var angle = Mathf.Lerp(from, to, (float)i / segments);
                line.SetPosition(i, transform.TransformPoint(center + new Vector3(rx * Mathf.Cos(angle), ry * Mathf.Sin(angle), 0f)));
            }
            line.widthCurve = EvenWidth;
            line.widthMultiplier = width * transform.lossyScale.x;
            line.startColor = line.endColor = color;
        }

        private Vector3 GemLocal()
        {
            var local = transform.InverseTransformPoint(_tube.Gem.position);
            local.z = 0f;
            return local;
        }

        private static void Show(SpriteRenderer renderer, Vector3 position, Vector3 scale, Color color)
        {
            renderer.enabled = true;
            renderer.transform.localPosition = position;
            renderer.transform.localScale = scale;
            renderer.color = color;
        }

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float k) =>
            Vector3.Lerp(Vector3.Lerp(a, b, k), Vector3.Lerp(b, c, k), k);

        private void HideSphere()
        {
            _lens.enabled = _heat.enabled = _core.enabled = _rim.enabled = _gloss.enabled = _photon.enabled = false;
            _diskBack.enabled = _diskFront.enabled = false;
            foreach (var wisp in _wisps) wisp.enabled = false;
            foreach (var glow in _soulGlows) glow.enabled = false;
            foreach (var core in _soulCores) core.enabled = false;
        }

        private void HideAll()
        {
            if (_gemTouched && _tube != null) _tube.Gem.localScale = Vector3.one;
            _gemTouched = false;
            HideSphere();
            _ringBack.enabled = _ringFront.enabled = _rippleBack.enabled = _rippleFront.enabled = false;
            _halo.enabled = _glint.enabled = false;
            _disk.Stop();
        }

        private void Finish()
        {
            _playing = false;
            HideAll();
            var done = _onFinished;
            _onFinished = null;
            done?.Invoke(this);
        }

        public override void OnDespawn()
        {
            _playing = false;
            _onFinished = null;
            HideAll();
            _tube = null;
        }

        private static float EaseOutCubic(float u) => 1f - Mathf.Pow(1f - u, 3f);
        private static float EaseOutQuart(float u) => 1f - Mathf.Pow(1f - u, 4f);
        private static float EaseInQuad(float u) => u * u;
        private static float EaseInCubic(float u) => u * u * u;
        private static float EaseInOutSine(float u) => -(Mathf.Cos(Mathf.PI * u) - 1f) / 2f;

        private static float EaseOutBack(float u)
        {
            const float c1 = 1.2f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
        }
    }
}
