using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // A hidden ball's reveal: a ring snaps in, one crack lights up across the shell, its halves part, the orb's rim flares, a gold glint.
    public sealed class ShellBreakFx : MonoBehaviour
    {
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int CrackLightId = Shader.PropertyToID("_CrackLight");
        private static readonly int HalfId = Shader.PropertyToID("_Half");
        private static readonly int RimLightId = Shader.PropertyToID("_RimLight");
        // Ring flattening, as round the shutter's lock.
        private const float RingAspect = 0.32f;
        // Upper half rises a little to the right, lower half drops to the left.
        private static readonly Vector3[] HalfPaths = { new(0.35f, 1f, 0f), new(-0.4f, -0.45f, 0f) };

        [SerializeField] private SpriteRenderer _ringNear;
        [SerializeField] private SpriteRenderer _ringFar;
        [SerializeField] private SpriteRenderer _hot;
        [SerializeField] private SpriteRenderer _spark;
        // The shell's two halves, above and below the crack.
        [SerializeField] private SpriteRenderer[] _halves;

        // Every renderer, so the ball can sort them with its overlays.
        public Renderer[] Renderers
        {
            get
            {
                var all = new Renderer[4 + _halves.Length];
                all[0] = _ringNear;
                all[1] = _ringFar;
                all[2] = _hot;
                all[3] = _spark;
                for (var i = 0; i < _halves.Length; i++) all[4 + i] = _halves[i];
                return all;
            }
        }

        // The ring snaps in round the shell.
        public void Charge(BallAnimationConfig config, float k)
        {
            var size = TubeArt.BallDiameter * Mathf.Lerp(config.HiddenRingFrom, config.HiddenRingTo, 1f - (1f - k) * (1f - k));
            Ring(config.Finish.Core, Mathf.Clamp01(k * 3f), size);
        }

        // Crack light on the shell: reach along the line from the centre.
        public static void Crack(SpriteRenderer shell, float reach)
        {
            RendererBlocks.SetFloat(shell, CrackId, reach);
            RendererBlocks.SetFloat(shell, CrackLightId, 1f);
        }

        // The shell is gone: its halves take over, crack fully lit along their edges.
        public void Break()
        {
            for (var i = 0; i < _halves.Length; i++)
            {
                RendererBlocks.SetVector(_halves[i], HalfId, new Vector4(i == 0 ? 1f : -1f, 1f, 0f, 0f));
                RendererBlocks.SetFloat(_halves[i], CrackId, 1f);
            }
        }

        // The horizon ring widens and fades; a small hot core flashes out.
        public void Hit(BallAnimationConfig config, float k)
        {
            var d = TubeArt.BallDiameter;
            var grow = 1f - (1f - k) * (1f - k) * (1f - k);
            Ring(Color.Lerp(Color.white, config.Finish.Core, k), (1f - k * k), d * Mathf.Lerp(config.HiddenRingTo, config.HiddenHorizonTo, grow));
            var flash = d * Mathf.Lerp(0.5f, 0.8f, grow);
            Stretch(_hot, Vector3.zero, flash, flash, Color.white, (1f - grow) * 0.6f);
            if (k >= 1f) _ringNear.enabled = _ringFar.enabled = _hot.enabled = false;
        }

        // The halves part on short arcs with a slight turn; their lit edges cool, then they fade.
        public void Halves(BallAnimationConfig config, float k)
        {
            var out3 = 1f - (1f - k) * (1f - k) * (1f - k);
            var alpha = 1f - Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.65f);
            for (var i = 0; i < _halves.Length; i++)
            {
                var half = _halves[i];
                var path = HalfPaths[i];
                var position = path * (config.HiddenShardDistance * out3) + Vector3.down * (config.HiddenShardDrop * k * k);
                half.enabled = alpha > 0.001f;
                var t = half.transform;
                t.localPosition = new Vector3(position.x, position.y, t.localPosition.z);
                t.localScale = Vector3.one * Mathf.Lerp(1f, config.HiddenBreakScale, out3);
                t.localRotation = Quaternion.Euler(0f, 0f, config.HiddenShardSpin * out3 * (i == 0 ? -1f : 1f));
                half.color = new Color(1f, 1f, 1f, alpha);
                RendererBlocks.SetFloat(half, CrackLightId, 1f - out3);
            }
            if (k >= 1f) HideHalves();
        }

        // The orb's rim light flares and settles back to its material value.
        public static void Flare(SpriteRenderer core, float amount)
        {
            var rest = core.sharedMaterial != null ? core.sharedMaterial.GetFloat(RimLightId) : 0.3f;
            RendererBlocks.SetFloat(core, RimLightId, Mathf.Lerp(rest, 1f, amount));
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
            _ringNear.enabled = _ringFar.enabled = _hot.enabled = _spark.enabled = false;
            HideHalves();
        }

        private void HideHalves()
        {
            foreach (var half in _halves)
            {
                half.enabled = false;
                var t = half.transform;
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
    }
}
