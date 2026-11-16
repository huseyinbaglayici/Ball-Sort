using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// Splits a ball between two portals while it passes through them. Each portal hides the ball above its ring's
    /// lowest edge (see the Sprite Tint Mask shader). The ball itself is drawn below the source edge only; a twin of its
    /// visual, made on the first warp, is drawn below the target edge only, so it comes down out from under the target
    /// ring along its edge.
    /// </summary>
    public sealed class BallPortalMask
    {
        private static readonly int PortalCutId = Shader.PropertyToID("_PortalCut");
        // The default edge is far above everything, so nothing is hidden.
        private static readonly Vector4 NoCut = new(100000f, 0f, 1f, 0f);
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

        /// <summary>
        /// <paramref name="sourceEdge"/> and <paramref name="targetEdge"/> are the ellipses at the bottom of the rings'
        /// side walls in world space (center y, center x, half width, half height), whose lower arcs are the edges. The
        /// twin stands at <paramref name="exit"/> (where the root would be at the target portal) under
        /// <paramref name="exitParent"/>. Call it after moving or scaling the ball.
        /// </summary>
        public void Split(Vector4 sourceEdge, Vector3 exit, Vector4 targetEdge, Transform exitParent)
        {
            IsSplit = true;
            Apply(_layers, sourceEdge);

            EnsureExitCopy();
            var copy = _exitVisual;
            copy.gameObject.SetActive(true);
            // The twin lives in the target tube while the ball itself is still in the source tube, so each part draws
            // with its own tube (under its glass) and not with the other one.
            if (copy.parent != exitParent) copy.SetParent(exitParent, false);
            var parentScale = exitParent.lossyScale;
            var scale = _visual.lossyScale;
            copy.rotation = _visual.rotation;
            copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            copy.position = exit + (_visual.position - _root.position);
            for (var i = 0; i < _layers.Length; i++)
            {
                _exitLayers[i].color = _layers[i].color;
                _exitLayers[i].sortingOrder = _layers[i].sortingOrder;
            }
            Apply(_exitLayers, targetEdge);
        }

        public void Clear()
        {
            if (!IsSplit) return;
            IsSplit = false;
            Apply(_layers, NoCut);
            if (_exitVisual == null) return;
            _exitVisual.gameObject.SetActive(false);
            if (_exitVisual.parent != _root) _exitVisual.SetParent(_root, false);
        }

        private static void Apply(SpriteRenderer[] layers, Vector4 cut)
        {
            _block ??= new MaterialPropertyBlock();
            foreach (var layer in layers)
            {
                // Read first: the SpriteRenderer keeps its sprite texture in the same block.
                layer.GetPropertyBlock(_block);
                _block.SetVector(PortalCutId, cut);
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
