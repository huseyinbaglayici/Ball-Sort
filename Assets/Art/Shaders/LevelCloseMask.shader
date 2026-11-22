// The level's last frame zooming into one spot that drifts to the screen's middle, washing into a flat tint.
Shader "UI/LevelCloseMask"
{
    Properties
    {
        // Unused; the UI image assigns it, so it must exist.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Snapshot ("Snapshot", 2D) = "black" {}
        // Rect size, the snapshot spot being zoomed into and where it shows, in the image's own units (origin bottom-left).
        _Size ("Size", Vector) = (1, 1, 0, 0)
        _Anchor ("Anchor", Vector) = (0, 0, 0, 0)
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Scale ("Scale", Float) = 1
        _Tint ("Tint", Color) = (0, 0, 0, 1)
        _TintAmount ("Tint Amount", Float) = 0
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
            float4 _Size;
            float4 _Anchor;
            float4 _Center;
            float _Scale;
            fixed4 _Tint;
            float _TintAmount;

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
                // Clamped, so the frame's edge stretches rather than showing what is under it.
                float2 source = saturate((_Anchor.xy + (p - _Center.xy) / max(_Scale, 0.0001)) / _Size.xy);
                fixed3 level = tex2D(_Snapshot, source).rgb;
                return fixed4(lerp(level, _Tint.rgb, saturate(_TintAmount)), 1) * i.color;
            }
            ENDCG
        }
    }
}
