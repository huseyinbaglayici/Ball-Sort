// Full-screen UI cover with a feathered round hole: dark outside, clear inside, like a vignette closing on one spot.
Shader "UI/LevelOpenMask"
{
    Properties
    {
        // Unused; the UI image assigns it, so it must exist.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        // Hole centre and rect size in the image's own units (origin bottom-left); radius and edge softness in the same units.
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Size ("Size", Vector) = (1, 1, 0, 0)
        _Radius ("Radius", Float) = 0
        _Feather ("Feather", Float) = 1
        // Thin rim where the feather starts, violet inside to white-cyan outside; dim tints the clear inside.
        _RimInner ("Rim Inner", Color) = (0.55, 0.35, 1, 1)
        _RimOuter ("Rim Outer", Color) = (0.75, 0.97, 1, 1)
        _RimWidth ("Rim Width", Float) = 3
        _RimAlpha ("Rim Alpha", Float) = 0.35
        _Dim ("Dim", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float4 _Center;
            float4 _Size;
            float _Radius;
            float _Feather;
            fixed4 _RimInner;
            fixed4 _RimOuter;
            float _RimWidth;
            float _RimAlpha;
            float _Dim;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = distance(i.uv * _Size.xy, _Center.xy);
                float edge = _Radius - _Feather;
                float cover = max(smoothstep(edge, _Radius, d), _Dim);
                float across = saturate((d - edge) / (2.0 * _RimWidth) + 0.5);
                float band = (1.0 - saturate(abs(d - edge) / _RimWidth)) * _RimAlpha;
                fixed3 rim = lerp(_RimInner.rgb, _RimOuter.rgb, across);
                // Dark cover first, the rim laid over it.
                float a = cover + band - cover * band;
                fixed3 rgb = a > 0.0001 ? (_Color.rgb * cover * (1.0 - band) + rim * band) / a : _Color.rgb;
                return fixed4(rgb, a) * i.color;
            }
            ENDCG
        }
    }
}
