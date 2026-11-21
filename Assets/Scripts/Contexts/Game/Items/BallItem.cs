using System.Collections.Generic;
using Project.Contexts.Game.Sort;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Serves Ball and QuestionMark prefabs; root moves and the landing settle tween apart so each can stop alone.
    public sealed class BallItem : Item
    {
        private const int FlyingOrderBoost = 20;
        // Frames longer than 30 fps are treated as hitches by the motion smear.
        private const float SmearFrameLimit = 1f / 30f;
        // A smeared ball never gets thinner than this share of its width.
        private const float MinSmearWidth = 0.75f;
        // In ball diameters from the mouth line.
        private const float NeckReach = 0.6f;
        // Full swings of the shell and bomb trembles.
        private const float TrembleCycles = 7f;

        private static readonly AnimationCurve SnapHitCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private static readonly AnimationCurve SnapReleaseCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        // Scaled by squash and smear; at scale 1 the sphere is TubeArt.BallDiameter wide.
        [SerializeField] private Transform _visual;
        // All orb layers, bottom to top.
        [SerializeField] private SpriteRenderer[] _layers;
        [SerializeField] private SpriteRenderer _outerGlow;
        // Its color is the orb's palette color; the ring shader darkens it.
        [SerializeField] private SpriteRenderer _ring;
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _innerGlow;
        // Hidden balls only: the shell drawn instead of the orb, and its break art.
        [SerializeField] private SpriteRenderer _shell;
        [SerializeField] private SpriteRenderer _break;
        // Scaled as one by the key's use.
        [SerializeField] private Transform _keyRoot;
        [SerializeField] private SpriteRenderer _keyGlow;
        [SerializeField] private SpriteRenderer _key;
        // The key reads on any orb: lightened toward white over a dark soft shadow.
        private const float KeyLighten = 0.45f;
        private static readonly Color KeyShade = new(0.04f, 0.02f, 0.16f, 0.85f);
        [SerializeField] private SpriteRenderer _bombBase;
        [SerializeField] private SpriteRenderer _bombRing;
        [SerializeField] private SpriteRenderer _bombFx;
        [SerializeField] private SpriteRenderer _defuse;
        [SerializeField] private TextMeshPro _bombCount;
        // Shown and hidden through its renderer: a renderer on an inactive object in a sorting group can't take orders.
        private Renderer _bombCountRenderer;

        public enum BombLook
        {
            None,
            Passive,
            Lit,
        }

        private int[] _baseOrders;
        private Renderer[] _overlays;
        private int[] _overlayOrders;
        // Orb, key and bomb parts that pop in together.
        private Transform[] _popParts;
        // Stack place sorting offset, so a ball draws whole over the one under it.
        private int _stackOffset;
        private bool _flying;
        private PrimeTween.Sequence _settle;
        // Kept so a new squish can start from the current shape.
        private Vector2 _squash = Vector2.one;
        private BallHighlight _highlight;
        private BallPortalMask _portal;
        private PrimeTween.Sequence _reveal;
        private PrimeTween.Sequence _keyUse;
        private PrimeTween.Sequence _bombPlay;
        private PrimeTween.Tween _pulse;
        // The shell shows until the break, a while after the ball is revealed.
        private bool _shellShown;
        private bool _defusing;
        private bool _exploded;
        // The counter hides while the ball is still above the source portal's edge.
        private bool _countCut;
        private bool _pulseFast;
        private float _litAt;

        public enum Arrival
        {
            None,
            Land,
            // Selected ball floats up and down until moved.
            Hover,
        }

        // The real color, also while hidden.
        public int ColorIndex { get; private set; }

        private float _haloAlpha = 1f;
        private PrimeTween.Sequence _drain;
        private PrimeTween.Sequence _pull;

        // The inner glow sprite, so an effect can lift a glowing copy off the orb.
        public Sprite GlowSprite => _innerGlow.sprite;

        // The palette color of ColorIndex, for effects drawn in the ball's own light.
        public Color Color { get; private set; }

        public bool IsHidden { get; private set; }

        // Lockbox id of the key this ball shows, or null.
        public string KeyId { get; private set; }

        public BombLook Bomb { get; private set; }

        private void Awake()
        {
            _baseOrders = new int[_layers.Length];
            int bottom = int.MaxValue, top = int.MinValue;
            var masked = new List<SpriteRenderer>(_layers);
            var pop = new List<Transform>();
            for (var i = 0; i < _layers.Length; i++)
            {
                _baseOrders[i] = _layers[i].sortingOrder;
                bottom = Mathf.Min(bottom, _baseOrders[i]);
                top = Mathf.Max(top, _baseOrders[i]);
                pop.Add(_layers[i].transform);
            }
            // Only overlays on the visual are cut by the portals; the break and defuse never fly.
            foreach (var part in new[] { _shell, _keyGlow, _key, _bombBase, _bombRing, _bombFx })
                if (part != null) masked.Add(part);
            foreach (var part in new[] { _keyGlow, _key, _bombBase, _bombRing, _bombFx })
                if (part != null) pop.Add(part.transform);
            _popParts = pop.ToArray();

            var overlays = new List<Renderer>();
            foreach (Renderer part in new Renderer[] { _shell, _break, _keyGlow, _key, _bombBase, _bombRing, _bombFx, _defuse })
                if (part != null) overlays.Add(part);
            if (_bombCount != null) overlays.Add(_bombCountRenderer = _bombCount.GetComponent<Renderer>());
            _overlays = overlays.ToArray();
            _overlayOrders = new int[_overlays.Length];
            for (var i = 0; i < _overlays.Length; i++) _overlayOrders[i] = _overlays[i].sortingOrder;

            _highlight = new BallHighlight(_visual, _layers[0].sortingLayerID, bottom, top);
            _portal = new BallPortalMask(transform, _visual, masked.ToArray());
        }

        public void Setup(int colorIndex, OrbSkin orb, Color color, bool hidden, BallAnimationConfig config)
        {
            ColorIndex = colorIndex;
            Color = color;
            IsHidden = hidden && _shell != null;
            StopObstacleEffects();
            KeyId = null;
            Bomb = BombLook.None;
            _shellShown = IsHidden;
            SetOrb(orb);
            _ring.color = color;
            _haloAlpha = config.OrbHaloAlpha;
            SetGlow(1f);
            _highlight.SetTint(IsHidden ? Color.white : color);
            transform.localScale = Vector3.one;
            ResetVisuals();
            ApplyLook();
        }

        // Used by Undo: the shell comes back at once. Only question mark balls can hide.
        public void Hide()
        {
            if (IsHidden || _shell == null) return;
            IsHidden = true;
            StopReveal();
            _shellShown = true;
            _highlight.SetTint(Color.white);
            ApplyLook();
        }

        // The shell trembles and breaks; the real orb pops in under the break.
        public void Reveal(OrbSkin orb, Color color, BallAnimationConfig config)
        {
            if (!IsHidden) return;
            StopReveal();
            IsHidden = false;
            Color = color;
            SetOrb(orb);
            var shake = config.HiddenShakeDuration;
            _reveal = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, shake, (ball, k) => ball.TrembleShell(k, config)))
                .ChainCallback(this, ball => ball.BreakShell(color, config))
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.HiddenBreakDuration, (ball, k) =>
                {
                    ball._break.transform.localScale = Vector3.one * Mathf.Lerp(1f, config.HiddenBreakScale, k);
                    ball._break.color = new Color(1f, 1f, 1f, 1f - k * k);
                }))
                .Insert(shake + config.HiddenOrbDelay, PrimeTween.Tween.Custom(this, config.HiddenOrbFrom, 1f,
                    config.HiddenOrbDuration, (ball, s) => ball.SetPartsScale(s), PrimeTween.Ease.OutBack))
                .ChainCallback(this, ball => ball.EndReveal());
        }

        // Grows harder toward the end.
        private void TrembleShell(float k, BallAnimationConfig config)
        {
            var swing = Mathf.Sin(k * TrembleCycles * Mathf.PI * 2f);
            var t = _shell.transform;
            t.localScale = Vector3.one * Mathf.Lerp(1f, config.HiddenShakeScale, k);
            t.localRotation = Quaternion.Euler(0f, 0f, config.HiddenShakeDegrees * k * swing);
            t.localPosition = new Vector3(config.HiddenShakeShift * k * Mathf.Sin(k * TrembleCycles * Mathf.PI * 3f),
                0f, t.localPosition.z);
        }

        private void BreakShell(Color color, BallAnimationConfig config)
        {
            ResetShell();
            _shellShown = false;
            _break.enabled = true;
            _break.color = Color.white;
            _break.transform.localScale = Vector3.one;
            SetPartsScale(config.HiddenOrbFrom);
            _highlight.SetTint(color);
            ApplyLook();
        }

        private void EndReveal()
        {
            if (_break != null) _break.enabled = false;
            SetPartsScale(1f);
        }

        // Drops any pending reveal step without finishing it.
        private void StopReveal()
        {
            if (_reveal.isAlive)
            {
                _reveal.Stop();
                SetPartsScale(1f);
            }
            transform.localRotation = Quaternion.identity;
            if (_shell == null) return;
            ResetShell();
            _break.enabled = false;
            _shellShown = IsHidden;
            ApplyLook();
        }

        private void ResetShell()
        {
            var t = _shell.transform;
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            t.localPosition = new Vector3(0f, 0f, t.localPosition.z);
        }

        // id null removes it; tint is the lock's Surface color.
        public void SetKey(string id, Color tint)
        {
            if (_key == null) return;
            // A used key finishes fading on its own.
            if (id == null && _keyUse.isAlive) return;
            if (id == KeyId && !_keyUse.isAlive && (id == null || _key.color == tint)) return;
            StopKeyUse();
            KeyId = id;
            if (id != null) _key.color = Color.Lerp(tint, Color.white, KeyLighten);
            ApplyLook();
        }

        // Where the key sits in the ball, for the flight that carries it to its lock.
        public Transform KeyMark => _key != null ? _key.transform : null;

        // Drawn width of the key mark, so the flight starts at the same size.
        public float KeyWorldWidth => _key != null && _key.sprite != null ? _key.sprite.bounds.size.x * _key.transform.lossyScale.x : 0f;

        // The key leaves the ball for its flight: gone from the orb at once.
        public void ReleaseKey()
        {
            if (KeyId == null) return;
            StopKeyUse();
            KeyId = null;
            ApplyLook();
        }

        // The key grows with a small overshoot and fades, then the ball is a plain orb.
        public void PlayKeyUse(BallAnimationConfig config)
        {
            if (KeyId == null || _keyUse.isAlive) return;
            _keyUse = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 1f, config.KeyUseScale, config.KeyUsePop,
                    (ball, s) => ball._keyRoot.localScale = Vector3.one * s, PrimeTween.Ease.OutBack))
                .Chain(PrimeTween.Tween.Custom(this, 1f, 0f, config.KeyUseFade, (ball, a) => ball.SetKeyAlpha(a)))
                .ChainCallback(this, ball =>
                {
                    ball.KeyId = null;
                    ball.ResetKey();
                    ball.ApplyLook();
                });
        }

        private void StopKeyUse()
        {
            if (_keyUse.isAlive) _keyUse.Stop();
            ResetKey();
        }

        private void ResetKey()
        {
            if (_keyRoot == null) return;
            _keyRoot.localScale = Vector3.one;
            SetKeyAlpha(1f);
        }

        private void SetKeyAlpha(float alpha)
        {
            _keyGlow.color = new Color(KeyShade.r, KeyShade.g, KeyShade.b, KeyShade.a * alpha);
            var c = _key.color;
            _key.color = new Color(c.r, c.g, c.b, alpha);
        }

        // ring: the orb's Surface color. A defuse in progress finishes; an undo puts the bomb back at once.
        public void SetBomb(BombLook look, int fuse, Color ring, BallAnimationConfig config)
        {
            if (_bombBase == null) return;
            if (look == BombLook.None && _defusing) return;
            var changed = look != Bomb || _bombPlay.isAlive || _exploded;
            // Lit by this move: the light fades in; at level start or after an undo it is just on.
            var newlyLit = look == BombLook.Lit && Bomb == BombLook.Passive;
            StopBombPlay();
            Bomb = look;
            _bombRing.color = ring;
            _bombCount.text = fuse.ToString();
            var fast = fuse <= config.BombFastFuse;
            if (look != BombLook.Lit) StopPulse();
            else if (changed || fast != _pulseFast || !_pulse.isAlive)
            {
                if (changed) _litAt = newlyLit ? Time.time : float.NegativeInfinity;
                Pulse(config, fast);
            }
            ApplyLook();
        }

        private void Pulse(BallAnimationConfig config, bool fast)
        {
            if (_pulse.isAlive) _pulse.Stop();
            _pulseFast = fast;
            var period = Mathf.Max(fast ? config.BombPulseFastPeriod : config.BombPulsePeriod, 0.02f);
            var fadeIn = config.BombLitFadeIn;
            var low = config.BombPulseMin;
            _pulse = PrimeTween.Tween.Custom(this, 0f, 1f, period, (ball, k) =>
            {
                var wave = Mathf.Lerp(low, 1f, 0.5f + 0.5f * Mathf.Cos(k * Mathf.PI * 2f));
                var fade = fadeIn > 0f ? Mathf.Clamp01((Time.time - ball._litAt) / fadeIn) : 1f;
                ball._bombFx.color = new Color(1f, 1f, 1f, wave * fade);
            }, cycles: -1);
        }

        private void StopPulse()
        {
            if (_pulse.isAlive) _pulse.Stop();
            if (_bombFx != null) _bombFx.color = Color.white;
        }

        // The bomb splits open and fades; the orb pops in under it.
        public void PlayDefuse(BallAnimationConfig config)
        {
            if (_bombBase == null || Bomb == BombLook.None) return;
            StopBombPlay();
            StopPulse();
            Bomb = BombLook.None;
            _defusing = true;
            _defuse.enabled = true;
            _defuse.color = Color.white;
            _defuse.transform.localScale = Vector3.one * config.DefuseFrom;
            SetPartsScale(0f);
            ApplyLook();
            _bombPlay = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, config.DefuseFrom, 1f, config.DefuseIn,
                    (ball, s) => ball._defuse.transform.localScale = Vector3.one * s, PrimeTween.Ease.OutBack))
                .ChainDelay(config.DefuseHold)
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.DefuseOut, (ball, k) =>
                {
                    ball._defuse.transform.localScale = Vector3.one * Mathf.Lerp(1f, config.DefuseOutScale, k);
                    ball._defuse.color = new Color(1f, 1f, 1f, 1f - k);
                }))
                .Insert(config.DefuseOrbDelay, PrimeTween.Tween.Custom(this, config.DefuseOrbFrom, 1f,
                    config.DefuseOrbDuration, (ball, s) => ball.SetPartsScale(s), PrimeTween.Ease.OutBack))
                .ChainCallback(this, ball => ball.StopBombPlay());
        }

        // The bomb trembles and swells, then it and the orb vanish and onBlast gets the blast's world position.
        public void PlayExplode(BallAnimationConfig config, System.Action<Vector3> onBlast)
        {
            if (_bombBase == null || Bomb == BombLook.None) return;
            StopBombPlay();
            _bombPlay = PrimeTween.Sequence.Create()
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, config.ExplodeShake, (ball, k) =>
                {
                    var swing = Mathf.Sin(k * TrembleCycles * Mathf.PI * 2f);
                    ball.transform.localRotation = Quaternion.Euler(0f, 0f, config.ExplodeShakeDegrees * k * swing);
                    ball.SetPartsScale(Mathf.Lerp(1f, config.ExplodeShakeScale, k));
                }))
                .ChainCallback(this, ball =>
                {
                    ball.transform.localRotation = Quaternion.identity;
                    ball.SetPartsScale(1f);
                    ball._exploded = true;
                    ball.ApplyLook();
                    onBlast?.Invoke(ball._visual.position);
                });
        }

        private void StopBombPlay()
        {
            var playing = _bombPlay.isAlive;
            if (playing) _bombPlay.Stop();
            if (_defuse != null) _defuse.enabled = false;
            if (playing || _defusing || _exploded)
            {
                transform.localRotation = Quaternion.identity;
                SetPartsScale(1f);
            }
            _defusing = false;
            _exploded = false;
        }

        // The completion pulls the glow out of the orb: it dims to share over duration after delay, then comes back at returnAt.
        public void DrainGlow(float delay, float duration, float share, float returnAt, float returnTime)
        {
            // Only its own sequence: the exhale may already be running.
            if (_drain.isAlive) _drain.Stop();
            _drain = PrimeTween.Sequence.Create()
                .ChainDelay(delay)
                .Chain(PrimeTween.Tween.Custom(this, 1f, share, duration, (ball, k) => ball.SetGlow(k), PrimeTween.Ease.InQuad))
                .ChainDelay(Mathf.Max(0f, returnAt - delay - duration))
                .Chain(PrimeTween.Tween.Custom(this, share, 1f, returnTime, (ball, k) => ball.SetGlow(k), PrimeTween.Ease.OutCubic));
        }

        // Full glow and shape at once; Undo, restart and despawn come through here.
        public void StopDrain()
        {
            if (_drain.isAlive) _drain.Stop();
            SetGlow(1f);
            if (!_pull.isAlive) return;
            _pull.Stop();
            ApplySquash(Vector2.one);
        }

        // Shrinks, stretches and drifts up toward the pull above while its essence leaves, then fills back out.
        public void Exhale(float delay, float shrinkTime, float recoverTime, float shrink, float stretch, float drift)
        {
            if (_pull.isAlive) _pull.Stop();
            _pull = PrimeTween.Sequence.Create()
                .ChainDelay(delay)
                .ChainCallback(this, ball => { if (ball._settle.isAlive) ball._settle.Stop(); })
                .Chain(PrimeTween.Tween.Custom(this, 0f, 1f, shrinkTime, (ball, k) => ball.ApplyExhale(k, shrink, stretch, drift), PrimeTween.Ease.InOutSine))
                .Chain(PrimeTween.Tween.Custom(this, 1f, 0f, recoverTime, (ball, k) => ball.ApplyExhale(k, shrink, stretch, drift), PrimeTween.Ease.InOutSine))
                .ChainCallback(this, ball => ball.ApplySquash(Vector2.one));
        }

        private void ApplyExhale(float k, float shrink, float stretch, float drift)
        {
            var scale = 1f - shrink * k;
            _squash = new Vector2(scale * (1f - 0.4f * stretch * k), scale * (1f + stretch * k));
            _visual.localScale = new Vector3(_squash.x, _squash.y, 1f);
            _visual.localPosition = new Vector3(0f, drift * k, 0f);
            _visual.localRotation = Quaternion.identity;
        }

        // Only the glow layers dim; the core keeps its colour.
        private void SetGlow(float share)
        {
            _outerGlow.color = new Color(1f, 1f, 1f, _haloAlpha * share);
            _innerGlow.color = new Color(1f, 1f, 1f, share);
        }

        private void StopObstacleEffects()
        {
            StopDrain();
            StopReveal();
            StopKeyUse();
            StopBombPlay();
            StopPulse();
            SetPartsScale(1f);
        }

        // Small hop up and back after the tube it sits in opens; a new move on the ball cancels it.
        public void Hop(float height, float duration, float delay)
        {
            if (height <= 0f || duration <= 0f || PrimeTween.Tween.GetTweensCount(transform) > 0) return;
            var home = transform.localPosition;
            PrimeTween.Tween.Custom(transform,
                    new PrimeTween.TweenSettings<float>(0f, 1f,
                        new PrimeTween.TweenSettings(duration, PrimeTween.Ease.Linear, startDelay: delay)),
                    (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * k)))
                .OnComplete(transform, t => t.localPosition = home);
        }

        // Which parts draw follows from the shell, key and bomb state.
        private void ApplyLook()
        {
            var orb = !_shellShown && !_exploded;
            foreach (var layer in _layers) layer.enabled = orb;
            if (_shell != null) _shell.enabled = _shellShown;
            if (_key != null) _keyGlow.enabled = _key.enabled = KeyId != null && orb;
            if (_bombBase == null) return;
            var bomb = Bomb != BombLook.None && orb;
            _bombBase.enabled = _bombRing.enabled = bomb;
            _bombFx.enabled = bomb && Bomb == BombLook.Lit;
            _bombCountRenderer.enabled = bomb && !_countCut;
        }

        private void SetPartsScale(float scale)
        {
            if (_popParts == null) return;
            foreach (var part in _popParts) part.localScale = Vector3.one * scale;
        }

        public void SetHighlight(bool on, BallAnimationConfig config) => _highlight.Set(on, config);

        // The highlight keeps fading.
        public void StopMotion()
        {
            PrimeTween.Tween.StopAll(transform);
            StopSettle();
            transform.localScale = Vector3.one;
        }

        public void SetFlying(bool flying)
        {
            _flying = flying;
            ApplySorting();
        }

        // 0 = bottom; each place raises the body by BallSortingSpan so touching balls never interleave layers.
        public void SetStackIndex(int index)
        {
            _stackOffset = index * TubeArt.BallSortingSpan;
            ApplySorting();
        }

        private void ApplySorting()
        {
            var boost = _flying ? FlyingOrderBoost : 0;
            for (var i = 0; i < _layers.Length; i++)
                _layers[i].sortingOrder = _baseOrders[i] + boost + (_layers[i] == _outerGlow ? 0 : _stackOffset);
            for (var i = 0; i < _overlays.Length; i++) _overlays[i].sortingOrder = _overlayOrders[i] + boost + _stackOffset;
            _highlight.SetOrder(boost, _stackOffset);
        }

        // Highlight is hidden while split; the counter shows only while the center is still below the source edge.
        public void SetPortalSplit(PortalView source, Vector3 exit, Vector3 exitShape, PortalView target,
            Transform exitParent)
        {
            if (!_portal.IsSplit) _highlight.Hide();
            _portal.Split(source, exit, exitShape, target, exitParent);
            var sourceEdge = source.Edge;
            SetCountCut(_visual.position.y >= sourceEdge.x - sourceEdge.w);
        }

        // Ball is fully out of the target ring but keeps the exit glow until it falls away.
        public void SetPortalTrail(Vector4 targetEdge)
        {
            _portal.Trail(targetEdge);
            SetCountCut(false);
        }

        public void ClearPortalCut()
        {
            if (!_portal.IsActive) return;
            _portal.Clear();
            SetCountCut(false);
        }

        private void SetCountCut(bool cut)
        {
            if (_countCut == cut) return;
            _countCut = cut;
            ApplyLook();
        }

        // Stretches the ball back along this frame's travel (area kept) so a fast flight reads as one shape.
        public void SetMotionSmear(Vector2 step, BallAnimationConfig config)
        {
            // A hitch frame would flash the ball wide, so cap the step at a 30 fps frame's motion.
            var hitch = Time.deltaTime > SmearFrameLimit ? SmearFrameLimit / Time.deltaTime : 1f;
            step *= hitch;
            var distance = step.magnitude;
            if (distance < 1e-5f)
            {
                ApplySquash(Vector2.one);
                return;
            }
            // Front edge stays put; the tail reaches back to last frame's position.
            var diameter = TubeArt.BallDiameter;
            var length = Mathf.Min(1f + config.SmearAmount * distance / diameter, config.MaxSmear);
            var width = Mathf.Max(1f - config.SmearThin * (length - 1f), MinSmearWidth);
            var direction = step / distance;
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            _visual.localScale = new Vector3(length, width, 1f);
            _visual.localPosition = -(Vector3)direction * ((length - 1f) * diameter * 0.5f);
        }

        // Runs on the root, so any new move stops it.
        public void Bob(float height, float duration)
        {
            if (height <= 0f || duration <= 0f || PrimeTween.Tween.GetTweensCount(transform) > 0) return;
            var home = transform.localPosition;
            PrimeTween.Tween.Custom(transform, 0f, 1f, duration,
                    (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * 2f * k) * (1f - k)))
                .OnComplete(transform, t => t.localPosition = home);
        }

        // Hard stop in the slot: a linear hit into SnapSquash, then an eased release with no overshoot.
        public void Snap(BallAnimationConfig config)
        {
            if (_settle.isAlive) _settle.Stop();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, config.SnapSquash, config.SnapHit, SnapHitCurve, 0f))
                .Chain(Squash(config.SnapSquash, Vector2.one, config.SnapRecover, SnapReleaseCurve, 0f));
        }

        // Scaled-down tube squash (strength 1 = full); starts from the current squash so overlapping waves continue.
        public void Nudge(float strength, float delay, BallAnimationConfig config)
        {
            if (strength <= 0.001f) return;
            if (_settle.isAlive) _settle.Stop();
            var squash = Vector2.one + (config.TubeSquash - Vector2.one) * strength;
            var stretch = Vector2.one + (config.TubeStretch - Vector2.one) * strength;
            stretch.y = Mathf.Min(stretch.y, TubeArt.MaxStackStretch);
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(_squash, squash, config.TubeSquashDuration, config.SquashCurve, delay))
                .Chain(Squash(squash, stretch, config.TubeStretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(stretch, Vector2.one, config.TubeRecoverDuration, config.RecoverCurve, 0f));
        }

        public void Settle(BallAnimationConfig config)
        {
            StopSettle();
            _settle = PrimeTween.Sequence.Create()
                .Chain(Squash(Vector2.one, config.SquashScale, config.SquashDuration, config.SquashCurve, config.SettleDelay))
                .Chain(Squash(config.SquashScale, config.StretchScale, config.StretchDuration, config.StretchCurve, 0f))
                .Chain(Squash(config.StretchScale, Vector2.one, config.RecoverDuration, config.RecoverCurve, 0f));
        }

        // anticipate comes out of duration; mouthY is the mouth for the neck squeeze; overshoot settles over settle.
        public void MoveLocal(Vector3 position, float duration, AnimationCurve curve, BallAnimationConfig config,
            Arrival arrival, float delay = 0f, bool anticipate = false, float mouthY = float.NaN,
            Vector3 overshoot = default, float settle = 0f, System.Action onArrive = null)
        {
            if ((transform.localPosition - position).sqrMagnitude < 1e-6f)
            {
                transform.localPosition = position;
                onArrive?.Invoke();
                return;
            }

            if (anticipate && config.AnticipationDuration > 0f)
            {
                var press = Mathf.Min(config.AnticipationDuration, duration * 0.5f);
                StopSettle();
                _settle = PrimeTween.Sequence.Create()
                    .Chain(Squash(Vector2.one, config.AnticipationSquash, press, config.SquashCurve, delay));
                delay += press;
                duration -= press;
            }

            var from = transform.localPosition;
            var previous = from;
            var peak = position + overshoot;
            settle = overshoot == Vector3.zero ? 0f : settle;
            var total = duration + settle;
            // Apply the neck squeeze on the first frame so the ball never flashes round.
            if (!float.IsNaN(mouthY)) transform.localScale = NeckScale(from.y, mouthY, config);
            PrimeTween.Tween.Custom(transform,
                    new PrimeTween.TweenSettings<float>(0f, 1f, new PrimeTween.TweenSettings(total, PrimeTween.Ease.Linear, startDelay: delay)),
                    (t, k) =>
                    {
                        var time = k * total;
                        Vector3 point;
                        var squish = Vector3.one;
                        if (time < duration || settle <= 0f)
                            point = Vector3.LerpUnclamped(from, peak, curve.Evaluate(Mathf.Clamp01(time / Mathf.Max(duration, 0.0001f))));
                        else
                        {
                            // Squash peaks halfway through the settle.
                            var s = (time - duration) / settle;
                            point = Vector3.Lerp(peak, position, Mathf.SmoothStep(0f, 1f, s));
                            var squash = Vector2.LerpUnclamped(Vector2.one, config.LiftSettleSquash, Mathf.Sin(Mathf.PI * s));
                            squish = new Vector3(squash.x, squash.y, 1f);
                        }
                        t.localPosition = point;
                        var neck = float.IsNaN(mouthY) ? Vector3.one : NeckScale(point.y, mouthY, config);
                        t.localScale = Vector3.Scale(neck, squish);
                        SetMotionSmear(point - previous, config);
                        previous = point;
                    })
                .OnComplete(this, ball =>
                {
                    // A ball that stops in the mouth (short lift) stays squeezed.
                    ball.transform.localScale = float.IsNaN(mouthY) ? Vector3.one : NeckScale(position.y, mouthY, config);
                    ball.Arrive(arrival, config);
                    // Landing sound and haptic fire on this frame.
                    onArrive?.Invoke();
                });
        }

        // Tightest with the center on the mouth line, round again a little over a radius away.
        public static Vector3 NeckScale(float y, float mouthY, BallAnimationConfig config)
        {
            var tight = Mathf.Clamp01(1f - Mathf.Abs(y - mouthY) / (TubeArt.BallDiameter * NeckReach));
            // Never taller than the stack gap allows, so lifted neighbours don't overlap.
            var height = Mathf.Min(config.NeckSqueeze.y, TubeArt.MaxStackStretch);
            return new Vector3(Mathf.Lerp(1f, config.NeckSqueeze.x, tight), Mathf.Lerp(1f, height, tight), 1f);
        }

        private void Arrive(Arrival arrival, BallAnimationConfig config)
        {
            switch (arrival)
            {
                case Arrival.Land:
                    Settle(config);
                    break;
                case Arrival.Hover:
                    ApplySquash(Vector2.one);
                    Hover(config);
                    break;
                default:
                    ApplySquash(Vector2.one);
                    break;
            }
        }

        // Endless sine starting at the rest point so it never jumps. Runs on the root, so StopMotion ends it.
        private void Hover(BallAnimationConfig config)
        {
            if (config.HoverHeight <= 0f || config.HoverPeriod <= 0f) return;
            var home = transform.localPosition;
            var height = config.HoverHeight;
            PrimeTween.Tween.Custom(transform,
                new PrimeTween.TweenSettings<float>(0f, 1f,
                    new PrimeTween.TweenSettings(config.HoverPeriod, PrimeTween.Ease.Linear, cycles: -1)),
                (t, k) => t.localPosition = home + Vector3.up * (height * Mathf.Sin(Mathf.PI * 2f * k)));
        }

        public override void OnDespawn()
        {
            PrimeTween.Tween.StopAll(transform);
            StopObstacleEffects();
            KeyId = null;
            Bomb = BombLook.None;
            _shellShown = IsHidden = false;
            ResetVisuals();
            ApplyLook();
        }

        private void ResetVisuals()
        {
            StopSettle();
            _highlight.Hide();
            _stackOffset = 0;
            SetFlying(false);
            ClearPortalCut();
        }

        private void StopSettle()
        {
            if (_settle.isAlive) _settle.Stop();
            ApplySquash(Vector2.one);
        }

        private PrimeTween.Tween Squash(Vector2 from, Vector2 to, float duration, AnimationCurve curve, float delay) =>
            PrimeTween.Tween.Custom(this,
                new PrimeTween.TweenSettings<float>(0f, 1f, new PrimeTween.TweenSettings(duration, curve, startDelay: delay)),
                (ball, t) => ball.ApplySquash(Vector2.LerpUnclamped(from, to, t)));

        private void ApplySquash(Vector2 squash)
        {
            _squash = squash;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = new Vector3(squash.x, squash.y, 1f);
            // Keep the bottom still.
            _visual.localPosition = new Vector3(0f, (squash.y - 1f) * TubeArt.BallDiameter * 0.5f, 0f);
        }

        private void SetOrb(OrbSkin orb)
        {
            _outerGlow.sprite = orb.OuterGlow;
            _core.sprite = orb.Core;
            _innerGlow.sprite = orb.InnerGlow;
        }
    }
}
