using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // The Orb Layer shader hides everything above a ring's lowest edge; a twin visual shows the part past the target.
    public sealed class BallPortalMask
    {
        private static readonly int PortalCutId = Shader.PropertyToID("_PortalCut");
        private static readonly int PortalHeatId = Shader.PropertyToID("_PortalHeat");
        // Edge far above everything: nothing hidden.
        private static readonly Vector4 NoCut = new(100000f, 0f, 1f, 0f);
        // _PortalHeat glow height going in and coming out.
        private const float EntryGlow = 1f;
        private const float ExitGlow = 2f;
        private static MaterialPropertyBlock _block;

        private readonly Transform _root;
        private readonly Transform _visual;
        private readonly SpriteRenderer[] _layers;
        // The part already out of the target portal.
        private Transform _exitVisual;
        private SpriteRenderer[] _exitLayers;

        public BallPortalMask(Transform root, Transform visual, SpriteRenderer[] layers)
        {
            _root = root;
            _visual = visual;
            _layers = layers;
        }

        public bool IsSplit { get; private set; }

        private bool _trailing;

        public bool IsActive => IsSplit || _trailing;

        // exitShape lets the twin round up while the entering tail stretches; call after moving or scaling the ball.
        public void Split(PortalView source, Vector3 exit, Vector3 exitShape, PortalView target, Transform exitParent)
        {
            IsSplit = true;
            _trailing = false;
            Apply(_layers, source.Edge, EntryGlow);

            EnsureExitCopy();
            var copy = _exitVisual;
            copy.gameObject.SetActive(true);
            // Each part sorts with its own tube, under that tube's glass.
            if (copy.parent != exitParent) copy.SetParent(exitParent, false);
            var parentScale = exitParent.lossyScale;
            var scale = Vector3.Scale(_visual.lossyScale, exitShape);
            copy.rotation = _visual.rotation;
            copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            copy.position = exit + Vector3.Scale(_visual.position - _root.position, exitShape);
            for (var i = 0; i < _layers.Length; i++)
            {
                _exitLayers[i].enabled = _layers[i].enabled;
                _exitLayers[i].sprite = _layers[i].sprite;
                _exitLayers[i].color = _layers[i].color;
                _exitLayers[i].sortingOrder = _layers[i].sortingOrder;
            }
            // Use the open ring's edge so the waiting twin stays hidden even while the ring is still a slit.
            Apply(_exitLayers, target.OpenEdge, ExitGlow);
        }

        // Ball is fully out: drop the twin, keep the exit glow on the ball.
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
                // Read first: the SpriteRenderer keeps its sprite texture in this block.
                layer.GetPropertyBlock(_block);
                _block.SetVector(PortalCutId, cut);
                _block.SetFloat(PortalHeatId, heat);
                layer.SetPropertyBlock(_block);
            }
        }

        // Clone of the visual; its layers are matched to the ball's by path.
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
                // The copy keeps the layer's block values, such as the core's tone.
                _layers[i].GetPropertyBlock(_block ??= new MaterialPropertyBlock());
                _exitLayers[i].SetPropertyBlock(_block);
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
