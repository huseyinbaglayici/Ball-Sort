using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Per-renderer shader values in one shared block; read first, as it also holds the sprite, tone and portal cut.
    public static class RendererBlocks
    {
        private static MaterialPropertyBlock _block;

        public static void SetFloat(Renderer renderer, int id, float value)
        {
            renderer.GetPropertyBlock(Block);
            _block.SetFloat(id, value);
            renderer.SetPropertyBlock(_block);
        }

        public static void SetVector(Renderer renderer, int id, Vector4 value)
        {
            renderer.GetPropertyBlock(Block);
            _block.SetVector(id, value);
            renderer.SetPropertyBlock(_block);
        }

        public static void SetVectorArray(Renderer renderer, int id, Vector4[] values)
        {
            renderer.GetPropertyBlock(Block);
            _block.SetVectorArray(id, values);
            renderer.SetPropertyBlock(_block);
        }

        private static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();
    }
}
