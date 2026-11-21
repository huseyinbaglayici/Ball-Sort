using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Hands a color's brightness and saturation scales to its core renderer, keeping the renderer's other block values.
    public static class OrbTone
    {
        private static readonly int ShadeId = Shader.PropertyToID("_Shade");
        private static readonly int SaturationId = Shader.PropertyToID("_Sat");
        private static MaterialPropertyBlock _block;

        public static void Apply(SpriteRenderer core, OrbSkin orb)
        {
            _block ??= new MaterialPropertyBlock();
            core.GetPropertyBlock(_block);
            _block.SetFloat(ShadeId, orb.Shade > 0f ? orb.Shade : 1f);
            _block.SetFloat(SaturationId, orb.Saturation > 0f ? orb.Saturation : 1f);
            core.SetPropertyBlock(_block);
        }
    }
}
