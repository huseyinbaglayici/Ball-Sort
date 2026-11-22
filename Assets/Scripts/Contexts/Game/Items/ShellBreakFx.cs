using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A hidden ball's reveal: orbit ring and crack light on the shell, then shards, a horizon ring and a gold glint.
    public sealed class ShellBreakFx : MonoBehaviour
    {
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int WedgeId = Shader.PropertyToID("_Wedge");
        // Ring flattening, as round the shutter's lock.
        private const float RingAspect = 0.32f;
        // How far the shards' sides wander, in radians.
        private const float ShardJag = 0.22f;

        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _glow;
        [SerializeField] private SpriteRenderer _hot;
        [SerializeField] private SpriteRenderer _spark;
        // Wedges of the shell.
        [SerializeField] private SpriteRenderer[] _shards;

        private static MaterialPropertyBlock _block;
        private readonly Vector3[] _shardDirections = new Vector3[3];
        private float _shardSpin;

        // Every renderer, so the ball can sort them with its overlays.
        public Renderer[] Renderers
        {
            get
            {
                var all = new Renderer[5 + _shards.Length];
                all[0] = _ringNear;
                all[1] = _ringFar;
                all[2] = _glow;
                all[3] = _hot;
                all[4] = _spark;
                for (var i = 0; i < _shards.Length; i++) all[5 + i] = _shards[i];
                return all;
            }
        }

        // The ring snaps in round the shell while the core warms.
        public void Charge(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var d = TubeArt.BallDiameter;
            var size = d * Mathf.Lerp(config.HiddenRingFrom, config.HiddenRingTo, 1f - (1f - k) * (1f - k));
            Ring(f.Core, Mathf.Clamp01(k * 3f) * 0.8f, size);
            Stretch(_glow, Vector3.zero, d * 1.3f, d * 1.3f, Color.Lerp(f.Violet, f.Core, k), 0.3f * k);
        }

        // Crack light on the shell; the ring holds.
        public static void Crack(SpriteRenderer shell, float reach) => SetFloat(shell, CrackId, reach);

        // The shell is gone: shards take their wedges in a fresh turn.
        public void Break()
        {
            _shardSpin = Random.Range(0f, Mathf.PI * 2f);
            var span = Mathf.PI * 2f / _shards.Length;
            for (var i = 0; i < _shards.Length; i++)
            {
                var start = _shardSpin + span * i;
                var mid = start + span * 0.5f;
                _shardDirections[i] = new Vector3(Mathf.Cos(mid), Mathf.Sin(mid), 0f);
                SetVector(_shards[i], WedgeId, new Vector4(start, span, ShardJag, 1f));
            }
        }

        // The horizon ring widens and fades, the hot core flashes out.
        public void Hit(BallAnimationConfig config, float k)
        {
            var f = config.Finish;
            var d = TubeArt.BallDiameter;
            var grow = 1f - (1f - k) * (1f - k) * (1f - k);
            var fade = 1f - k * k;
            Ring(Color.Lerp(Color.white, f.Core, k), fade * 0.8f, d * Mathf.Lerp(config.HiddenRingTo, config.HiddenHorizonTo, grow));
            var flash = d * Mathf.Lerp(0.7f, 1.1f, grow);
            Stretch(_hot, Vector3.zero, flash, flash, Color.white, (1f - grow) * 0.55f);
            Stretch(_glow, Vector3.zero, flash * 1.5f, flash * 1.5f, f.Core, (1f - grow) * 0.35f);
        }

        // Shards fly out on short arcs, turn a little and fade, their crack light dying.
        public void Shards(BallAnimationConfig config, float k)
        {
            var out3 = 1f - (1f - k) * (1f - k) * (1f - k);
            var alpha = 1f - Mathf.SmoothStep(0f, 1f, (k - 0.3f) / 0.7f);
            for (var i = 0; i < _shards.Length; i++)
            {
                var shard = _shards[i];
                var dir = _shardDirections[i];
                var position = dir * (config.HiddenShardDistance * out3) + Vector3.down * (config.HiddenShardDrop * k * k);
                var scale = Mathf.Lerp(1f, config.HiddenBreakScale, out3);
                shard.enabled = alpha > 0.001f;
                var t = shard.transform;
                t.localPosition = new Vector3(position.x, position.y, t.localPosition.z);
                t.localScale = Vector3.one * scale;
                t.localRotation = Quaternion.Euler(0f, 0f, config.HiddenShardSpin * out3 * (dir.x >= 0f ? -1f : 1f));
                shard.color = new Color(1f, 1f, 1f, alpha);
                SetFloat(shard, CrackId, 1f - out3);
            }
            if (k >= 1f) HideShards();
        }

        // A small gold glint on the orb's shoulder as it settles.
        public void Spark(BallAnimationConfig config, float k)
        {
            var d = TubeArt.BallDiameter;
            var size = d * config.HiddenSparkSize * Mathf.Sin(Mathf.PI * k);
            var gold = Color.Lerp(config.Finish.Gold, Color.white, 0.35f);
            Stretch(_spark, new Vector3(d * 0.26f, d * 0.28f, 0f), size, size, gold, 1f - k * 0.5f);
            if (k >= 1f) _spark.enabled = false;
        }

        public void Hide()
        {
            _ringNear.enabled = _ringFar.enabled = _glow.enabled = _hot.enabled = _spark.enabled = false;
            HideShards();
        }

        private void HideShards()
        {
            foreach (var shard in _shards)
            {
                shard.enabled = false;
                var t = shard.transform;
                t.localPosition = new Vector3(0f, 0f, t.localPosition.z);
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
        }

        private void Ring(Color color, float alpha, float width)
        {
            Stretch(_ringNear, Vector3.zero, width, width * RingAspect, color, alpha);
            Stretch(_ringFar, Vector3.zero, width, width * RingAspect, color, alpha * 0.6f);
        }

        // Keeps the renderer's depth step.
        private static void Stretch(SpriteRenderer renderer, Vector3 position, float width, float height, Color color, float alpha)
        {
            renderer.enabled = alpha > 0.001f && width > 0.0001f && renderer.sprite != null;
            if (!renderer.enabled) return;
            var native = renderer.sprite.bounds.size;
            var t = renderer.transform;
            t.localPosition = new Vector3(position.x, position.y, t.localPosition.z);
            t.localScale = new Vector3(width / native.x, height / native.y, 1f);
            renderer.color = new Color(color.r, color.g, color.b, color.a * alpha);
        }

        // Read first: the renderer keeps its sprite texture and portal cut in this block.
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
    }
}
