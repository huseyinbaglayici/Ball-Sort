// The won level's last frame inside a round window that shrinks into Home's hand orb, Home showing around it.
Shader "UI/LevelCloseMask"
{
    Properties
    {
        // Unused; the UI image assigns it, so it must exist.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Snapshot ("Snapshot", 2D) = "black" {}
        // Window centre and rect size in the image's own units (origin bottom-left); radius and edge softness in the same units.
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Size ("Size", Vector) = (1, 1, 0, 0)
        _Radius ("Radius", Float) = 0
        _Feather ("Feather", Float) = 1
        // The snapshot shrinks about the centre by this factor, so the whole level fits the window as it closes.
        _Scale ("Scale", Float) = 1
        // The snapshot point shown at the centre: the same spot at first, the level's middle by the end.
        _Anchor ("Anchor", Vector) = (0, 0, 0, 0)
        _RimInner ("Rim Inner", Color) = (0.55, 0.35, 1, 1)
        _RimOuter ("Rim Outer", Color) = (0.75, 0.97, 1, 1)
        _RimWidth ("Rim Width", Float) = 3
        _RimAlpha ("Rim Alpha", Float) = 0.35
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

            sampler2D _Snapshot;
            float4 _Center;
            float4 _Size;
            float _Radius;
            float _Feather;
            float _Scale;
            float4 _Anchor;
            fixed4 _RimInner;
            fixed4 _RimOuter;
            float _RimWidth;
            float _RimAlpha;

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
                float2 p = i.uv * _Size.xy;
                float d = distance(p, _Center.xy);
                float inside = 1.0 - smoothstep(_Radius - _Feather, _Radius, d);
                float2 source = (_Anchor.xy + (p - _Center.xy) / max(_Scale, 0.0001)) / _Size.xy;
                // Outside the shrunk frame there is no level to show.
                float inFrame = step(0.0, source.x) * step(source.x, 1.0) * step(0.0, source.y) * step(source.y, 1.0);
                fixed3 level = tex2D(_Snapshot, source).rgb;
                float edge = _Radius - _Feather;
                float across = saturate((d - edge) / (2.0 * _RimWidth) + 0.5);
                float band = (1.0 - saturate(abs(d - edge) / _RimWidth)) * _RimAlpha;
                fixed3 rim = lerp(_RimInner.rgb, _RimOuter.rgb, across);
                float a = inside * inFrame;
                fixed3 rgb = lerp(level, rim, band);
                return fixed4(rgb, saturate(a + band * inside)) * i.color;
            }
            ENDCG
        }
    }
}
