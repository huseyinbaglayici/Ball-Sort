// The housing ring behind an orb: a near-black tone of the orb's own color, keeping a thin violet outer edge.
Shader "Project/Orb Ring"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Radius ("Ring radius in sprite uv", Float) = 0.355
        _ToneGain ("Tone gain", Range(0, 2)) = 0.55
        _EdgeStart ("Outer edge start", Range(0.5, 1)) = 0.92
        _EdgeEnd ("Outer edge end", Range(0.5, 1.2)) = 0.97
        _EdgeKeep ("Outer edge brightness", Range(0, 1.5)) = 0.9
        [PerRendererData] _PortalCut ("Portal edge", Vector) = (100000, 0, 1, 0)
        [PerRendererData] _PortalHeat ("Portal heat", Float) = 0
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex OrbVert
            #pragma fragment frag
            #include "OrbCommon.cginc"

            float _Radius, _ToneGain, _EdgeStart, _EdgeEnd, _EdgeKeep;

            fixed4 frag(OrbV2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // The renderer color carries the orb's palette color.
                fixed lum = dot(i.color.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 tone = saturate(lum + (i.color.rgb - lum) * 1.45);
                fixed texLum = dot(tex.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 dark = texLum * tone * _ToneGain;

                float r = length(i.uv - 0.5) / _Radius;
                float edge = smoothstep(_EdgeStart, _EdgeEnd, r);
                fixed3 rgb = lerp(dark, tex.rgb * _EdgeKeep, edge);

                return OrbPortal(rgb, tex.a * i.color.a, i.world);
            }
            ENDCG
        }
    }
}
