// Glow and glass layers of an orb: the texel as drawn, with the portal cut.
Shader "Project/Orb Layer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        [PerRendererData] _PortalCut ("Portal edge (x center y, y center x, z half width, w half height)", Vector) = (100000, 0, 1, 0)
        [PerRendererData] _PortalHeat ("Event horizon glow at the portal edge (0 off, else its height)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"
            "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex OrbVert
            #pragma fragment frag
            #include "OrbCommon.cginc"

            fixed4 frag(OrbV2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                return OrbPortal(tex.rgb, tex.a, i.world);
            }
            ENDCG
        }
    }
}
