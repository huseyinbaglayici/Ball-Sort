using System;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Ice over a whole tube with the needed color's orb on its badge; parented to the tube's art.
    public sealed class FrozenTubeItem : Item
    {
        // Must match CHUNKS in the ice shader.
        public const int ChunkCount = 7;
        // Badge center in the art, from its top.
        private const float BadgeYPx = 515f;
        // Badge ring radius over the art width; cracks start at it.
        private const float BadgeRingRadius = 0.28f;
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private static readonly int CrackOriginId = Shader.PropertyToID("_CrackOrigin");
        private static readonly int ChunksId = Shader.PropertyToID("_Chunks");
        private static readonly int PieceId = Shader.PropertyToID("_Piece");
        private static readonly int TiltId = Shader.PropertyToID("_Tilt");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ClipId = Shader.PropertyToID("_Clip");
        // Chunk seeds in ice units (u across, v times the art's aspect up) with their facet tilt; the badge's chunk first.
        private static readonly Vector4[] Chunks =
        {
            new(0.48f, 1.34f, 0f, 0f), new(0.2f, 2.06f, -0.18f, 0.1f), new(0.83f, 1.86f, 0.15f, 0.14f),
            new(0.6f, 2.44f, 0.05f, 0.2f), new(0.17f, 0.78f, -0.16f, -0.1f), new(0.84f, 0.64f, 0.2f, -0.08f),
            new(0.4f, 0.2f, -0.05f, -0.18f)
        };
        private static readonly float Aspect = TubeArt.ArtHeightPx / TubeArt.ArtWidthPx;
        private static readonly Vector2 Origin = new(0.5f, (1f - BadgeYPx / TubeArt.ArtHeightPx) * Aspect);

        [SerializeField] private SpriteRenderer _frozen;
        [SerializeField] private Transform _badge;
        // Orb layers on the badge, as on a ball: outer glow, core and inner glow take the color.
        [SerializeField] private SpriteRenderer _badgeOuterGlow;
        [SerializeField] private SpriteRenderer _badgeCore;
        [SerializeField] private SpriteRenderer _badgeInnerGlow;
        [SerializeField] private SpriteRenderer[] _badgeLayers;
        // One renderer per chunk, each under a pivot at its seed so it turns about its own middle.
        [SerializeField] private SpriteRenderer[] _pieces;

        private static MaterialPropertyBlock _block;
        private PrimeTween.Sequence _play;
        private float _haloAlpha;
        private float _badgeScale = 1f;
        private readonly Vector3[] _rest = new Vector3[ChunkCount];
        private readonly Vector2[] _away = new Vector2[ChunkCount];
        private readonly float[] _delay = new float[ChunkCount];

        public bool IsPlaying => _play.isAlive;

        // rise: the tube's extra height over the standard art; the ice stretches with it, the badge stays its size.
        public void Setup(OrbSkin orb, float rise, BallAnimationConfig config)
        {
            Stop();
            transform.localPosition = new Vector3(0f, rise * 0.5f, 0f);
            var stretch = new Vector3(1f, 1f + rise * TubeArt.PixelsPerUnit / TubeArt.ArtHeightPx, 1f);
            _frozen.transform.localScale = stretch;
            _badge.localPosition = BadgeRest;
            // The orb art's sphere is TubeArt.BallDiameter wide at scale 1.
            _badgeScale = config.FrozenBadgeOrbDiameter / TubeArt.BallDiameter;
            _badge.localScale = Vector3.one * _badgeScale;
            _badgeOuterGlow.sprite = orb.OuterGlow;
            _badgeCore.sprite = orb.Core;
            _badgeInnerGlow.sprite = orb.InnerGlow;
            OrbTone.Apply(_badgeCore, orb);
            _haloAlpha = config.OrbHaloAlpha;

            var origin = new Vector4(Origin.x, Origin.y, BadgeRingRadius, 0f);
            Shade(_frozen, origin);
            var bounds = _frozen.sprite.bounds;
            var badge = Local(bounds, stretch, Origin);
            for (var i = 0; i < ChunkCount; i++)
            {
                var piece = _pieces[i];
                var seed = Local(bounds, stretch, Chunks[i]);
                _rest[i] = seed;
                piece.transform.parent.localPosition = seed;
                piece.transform.localPosition = -seed;
                piece.transform.localScale = stretch;
                Shade(piece, origin);
                SetVector(piece, PieceId, new Vector4(i, 0f, 0f, 1f));
                // Away from the badge; its own chunk drops a little.
                var away = (Vector2)(seed - badge);
                _away[i] = away.sqrMagnitude < 0.01f ? new Vector2(0.15f, -0.35f) : away.normalized;
                _delay[i] = ((Vector2)Chunks[i] - Origin).magnitude;
            }
            // Chunks leave in order of their distance from the badge, a beat apart.
            var order = (float[])_delay.Clone();
            Array.Sort(order);
            for (var i = 0; i < ChunkCount; i++) _delay[i] = Array.IndexOf(order, _delay[i]) * config.FrozenChunkStagger;
            Stop();
        }

        private static Vector3 Local(Bounds bounds, Vector3 stretch, Vector2 ice) => new(
            (bounds.min.x + ice.x * bounds.size.x) * stretch.x,
            (bounds.min.y + ice.y / Aspect * bounds.size.y) * stretch.y, 0f);

        private static void Shade(Renderer renderer, Vector4 origin)
        {
            renderer.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetVectorArray(ChunksId, Chunks);
            _block.SetVector(CrackOriginId, origin);
            renderer.SetPropertyBlock(_block);
        }

        // A short tremble, the ice cracks out from the badge in beats, then breaks into chunks that tumble away.
        public void PlayMelt(BallAnimationConfig config, Action onClear, Action onDone)
        {
            Stop();
            var wobble = config.MeltWobbleDuration;
            var breakAt = Mathf.Max(config.MeltDuration, wobble);
            var flight = config.FrozenChunkStagger * (ChunkCount - 1) + config.FrozenChunkTime;
            _play = PrimeTween.Sequence.Create()
                .Insert(0f, PrimeTween.Tween.Custom(this, 0f, 1f, breakAt, (ice, k) => ice.Strain(config, k * breakAt)))
                .InsertCallback(breakAt, this, ice => ice.Break())
                .Insert(breakAt, PrimeTween.Tween.Custom(this, 0f, 1f, flight, (ice, k) => ice.Tumble(config, k * flight)))
                .InsertCallback(breakAt + config.MeltOutDuration, () => onClear?.Invoke())
                .ChainCallback(() => onDone?.Invoke());
        }

        // Tremble, then each beat runs the cracks further with a small knock; the edges brighten toward the break.
        private void Strain(BallAnimationConfig config, float time)
        {
            var wobble = config.MeltWobbleDuration;
            var w = 0f;
            if (time < wobble)
            {
                var k = time / Mathf.Max(wobble, 0.0001f);
                w = config.MeltWobble * (1f - k) * Mathf.Sin(k * config.MeltWobbleCycles * Mathf.PI * 2f);
            }
            var beats = Mathf.Max(config.FrozenCrackBeats, 1);
            var span = (Mathf.Max(config.MeltDuration, wobble) - wobble) / beats;
            var crack = 0f;
            var punch = 0f;
            for (var b = 0; b < beats; b++)
            {
                var since = time - (wobble + b * span);
                if (since < 0f) break;
                var jump = Mathf.Clamp01(since / Mathf.Max(config.FrozenBeatTime, 0.0001f));
                crack = (b + 1f - (1f - jump) * (1f - jump)) / beats;
                punch = config.FrozenPunch * Mathf.Max(0f, 1f - since / 0.15f);
            }
            transform.localScale = new Vector3(1f + w + punch, 1f - w + punch, 1f);
            SetFloat(_frozen, CrackId, crack);
            var charge = Mathf.Clamp01((time - wobble) / Mathf.Max(config.MeltDuration - wobble, 0.0001f));
            SetFloat(_frozen, ChargeId, charge * charge);
        }

        // The whole block gives way to its chunks.
        private void Break()
        {
            transform.localScale = Vector3.one;
            _frozen.enabled = false;
            foreach (var piece in _pieces) piece.enabled = true;
        }

        // Chunks drift down slowly as if in low gravity, turning in depth, and dissolve to dust inside the tube's own column.
        private void Tumble(BallAnimationConfig config, float time)
        {
            var art = _frozen.sprite.bounds;
            var low = _frozen.transform.TransformPoint(art.min);
            var high = _frozen.transform.TransformPoint(art.max);
            var clip = new Vector4(low.x, low.y, high.x, high.y);
            for (var i = 0; i < ChunkCount; i++)
            {
                var piece = _pieces[i];
                var pivot = piece.transform.parent;
                var t = Mathf.Max(time - _delay[i], 0f);
                var k = Mathf.Clamp01(t / Mathf.Max(config.FrozenChunkTime, 0.0001f));
                var away = _away[i];
                var turn = 1f - (1f - k) * (1f - k);
                pivot.localPosition = _rest[i] + new Vector3(away.x * config.FrozenChunkSpeed * t,
                    away.y * config.FrozenChunkSpeed * t + config.FrozenChunkLift * t - 0.5f * config.FrozenChunkGravity * t * t, 0f);
                var rotation = Quaternion.Euler(away.y * config.FrozenChunkTilt * turn, -away.x * config.FrozenChunkTilt * turn,
                    -away.x * config.FrozenChunkSpin * turn);
                pivot.localRotation = rotation;
                pivot.localScale = Vector3.one * Mathf.Lerp(1f, config.FrozenChunkShrink, k);
                var facing = rotation * Vector3.back;
                SetVector(piece, TiltId, new Vector4(facing.x, facing.y, -facing.z, 0f));
                var dissolve = Mathf.SmoothStep(0f, 1f, (k - config.FrozenChunkDissolveAt) / Mathf.Max(1f - config.FrozenChunkDissolveAt, 0.0001f));
                piece.enabled = dissolve < 1f;
                SetVector(piece, ClipId, clip);
                SetFloat(piece, DissolveId, dissolve);
            }

            var holder = _pieces[0].transform.parent;
            _badge.localPosition = holder.localPosition + holder.localRotation * (BadgeRest - _rest[0]) * holder.localScale.x;
            _badge.localRotation = holder.localRotation;
            _badge.localScale = Vector3.one * (_badgeScale * holder.localScale.x);
            var badgeK = Mathf.Clamp01((time - _delay[0]) / Mathf.Max(config.FrozenChunkTime, 0.0001f));
            SetBadgeAlpha(1f - Mathf.SmoothStep(0f, 1f, badgeK / Mathf.Max(config.FrozenChunkDissolveAt + 0.2f, 0.0001f)));
        }

        private static Vector3 BadgeRest => new(0f, TubeArt.ArtY(BadgeYPx), 0f);

        private void SetBadgeAlpha(float alpha)
        {
            foreach (var layer in _badgeLayers)
                layer.color = new Color(1f, 1f, 1f, layer == _badgeOuterGlow ? alpha * _haloAlpha : alpha);
        }

        // Read first: the renderer keeps its sprite texture in this block.
        private static void SetFloat(Renderer renderer, int id, float value)
        {
            renderer.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetFloat(id, value);
            renderer.SetPropertyBlock(_block);
        }

        private static void SetVector(Renderer renderer, int id, Vector4 value)
        {
            renderer.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetVector(id, value);
            renderer.SetPropertyBlock(_block);
        }

        // Back to the ice at rest without running callbacks.
        public void Stop()
        {
            if (_play.isAlive) _play.Stop();
            transform.localScale = Vector3.one;
            _frozen.enabled = true;
            _frozen.color = Color.white;
            SetFloat(_frozen, CrackId, 0f);
            SetFloat(_frozen, ChargeId, 0f);
            for (var i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                piece.enabled = false;
                SetFloat(piece, DissolveId, 0f);
                var pivot = piece.transform.parent;
                pivot.localPosition = _rest[i];
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
            }
            _badge.localPosition = BadgeRest;
            _badge.localRotation = Quaternion.identity;
            _badge.localScale = Vector3.one * _badgeScale;
            SetBadgeAlpha(1f);
        }

        public override void OnDespawn() => Stop();
    }
}
