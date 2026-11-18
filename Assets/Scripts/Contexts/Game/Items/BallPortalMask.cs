using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// Splits a ball between two portals while it passes through them. Each portal hides the ball above its ring's
    /// lowest edge (see the Orb Layer shader), so nothing of the ball ever shows on the ring or in its void. The
    /// ball itself is drawn below the source edge only, and burns up into it: the hole's heat glows on it near the edge.
    /// A twin of its visual, made on the first warp, is drawn below the target edge only, so it comes down out from under
    /// the target ring along its edge, glowing the same way as it comes out. That glow is twice as tall, so it reads at
    /// game size, and it does not stop when the ball is all out: the ball keeps it (<see cref="Trail"/>) and it fades
    /// on its own as the ball falls away from the edge.
    /// </summary>
    public sealed class BallPortalMask
    {
        private static readonly int PortalCutId = Shader.PropertyToID("_PortalCut");
        private static readonly int PortalHeatId = Shader.PropertyToID("_PortalHeat");
        // The default edge is far above everything, so nothing is hidden.
        private static readonly Vector4 NoCut = new(100000f, 0f, 1f, 0f);
        // Height of the edge glow (the shader's _PortalHeat) going in and coming out.
        private const float EntryGlow = 1f;
        private const float ExitGlow = 2f;
        private static MaterialPropertyBlock _block;

        private readonly Transform _root;
        private readonly Transform _visual;
        private readonly SpriteRenderer[] _layers;
        // The part of the ball already out of the target portal.
        private Transform _exitVisual;
        private SpriteRenderer[] _exitLayers;

        public BallPortalMask(Transform root, Transform visual, SpriteRenderer[] layers)
        {
            _root = root;
            _visual = visual;
            _layers = layers;
        }

        public bool IsSplit { get; private set; }

        // All out, still glowing from the target edge.
        private bool _trailing;

        /// <summary>Split, or trailing the exit glow: something to clear.</summary>
        public bool IsActive => IsSplit || _trailing;

        /// <summary>
        /// The ball goes into <paramref name="source"/> and comes out of <paramref name="target"/>. The twin stands at
        /// <paramref name="exit"/> (where the root would be at the target portal) under <paramref name="exitParent"/>,
        /// shaped like the ball times <paramref name="exitShape"/>: it rounds up while the tail going in keeps stretching.
        /// Call it after moving or scaling the ball.
        /// </summary>
        public void Split(PortalView source, Vector3 exit, Vector3 exitShape, PortalView target, Transform exitParent)
        {
            IsSplit = true;
            _trailing = false;
            Apply(_layers, source.Edge, EntryGlow);

            EnsureExitCopy();
            var copy = _exitVisual;
            copy.gameObject.SetActive(true);
            // The twin lives in the target tube while the ball itself is still in the source tube, so each part draws
            // with its own tube (under its glass) and not with the other one.
            if (copy.parent != exitParent) copy.SetParent(exitParent, false);
            var parentScale = exitParent.lossyScale;
            var scale = Vector3.Scale(_visual.lossyScale, exitShape);
            copy.rotation = _visual.rotation;
            copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            copy.position = exit + Vector3.Scale(_visual.position - _root.position, exitShape);
            for (var i = 0; i < _layers.Length; i++)
            {
                _exitLayers[i].sprite = _layers[i].sprite;
                _exitLayers[i].sortingOrder = _layers[i].sortingOrder;
            }
            // Cut on the open ring's edge: the twin waits above that line for the ball to go in, and must stay hidden
            // there even while the ring is still a slit (beside a narrow ring the mask would hide nothing).
            Apply(_exitLayers, target.OpenEdge, ExitGlow);
        }

        /// <summary>
        /// The ball is all out of the target ring (its <paramref name="edge"/>, see <see cref="PortalView.OpenEdge"/>):
        /// the twin goes and the ball itself keeps the exit glow, which fades as it falls away from the edge.
        /// </summary>
        public void Trail(Vector4 edge)
        {
            if (IsSplit)
            {
                IsSplit = false;
                HideExitCopy();
            }
            _trailing = true;
            Apply(_layers, edge, ExitGlow);
        }

        public void Clear()
        {
            if (!IsActive) return;
            IsSplit = false;
            _trailing = false;
            Apply(_layers, NoCut, 0f);
            HideExitCopy();
        }

        private void HideExitCopy()
        {
            if (_exitVisual == null) return;
            _exitVisual.gameObject.SetActive(false);
            if (_exitVisual.parent != _root) _exitVisual.SetParent(_root, false);
        }

        private static void Apply(SpriteRenderer[] layers, Vector4 cut, float heat)
        {
            _block ??= new MaterialPropertyBlock();
            foreach (var layer in layers)
            {
                // Read first: the SpriteRenderer keeps its sprite texture in the same block.
                layer.GetPropertyBlock(_block);
                _block.SetVector(PortalCutId, cut);
                _block.SetFloat(PortalHeatId, heat);
                layer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// The twin: a clone of the visual under the root, so it sorts with the ball. Its layers line up with the
        /// ball's layers by their path under the visual.
        /// </summary>
        private void EnsureExitCopy()
        {
            if (_exitVisual != null) return;
            _exitVisual = Object.Instantiate(_visual.gameObject, _root).transform;
            _exitVisual.name = "PortalExit";
            _exitLayers = new SpriteRenderer[_layers.Length];
            for (var i = 0; i < _layers.Length; i++)
            {
                var path = PathUnder(_visual, _layers[i].transform);
                _exitLayers[i] = (path.Length == 0 ? _exitVisual : _exitVisual.Find(path)).GetComponent<SpriteRenderer>();
            }
        }

        private static string PathUnder(Transform root, Transform child)
        {
            var path = "";
            for (var t = child; t != root; t = t.parent)
                path = path.Length == 0 ? t.name : t.name + "/" + path;
            return path;
        }
    }
}
