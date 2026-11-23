// The level's last frame held still while a glowing orb grows over it from the mascot's hand.
Shader "UI/LevelCloseMask"
{
    Properties
    {
        // Unused; the UI image assigns it, so it must exist.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Snapshot ("Snapshot", 2D) = "black" {}
        // 1 where a screen capture comes out upside down.
        _FlipY ("Flip Y", Float) = 0
        // Rect size, orb centre and radius in image units (origin bottom-left); soft edge in the same units.
        _Size ("Size", Vector) = (1, 1, 0, 0)
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Radius ("Radius", Float) = 0
        _Feather ("Feather", Float) = 1
        // Orb light from its core to its edge, the halo around it and how much the frame darkens behind it.
        _Core ("Core", Color) = (0.95, 0.55, 1, 1)
        _Edge ("Edge", Color) = (0.16, 0.08, 0.38, 1)
        _Glow ("Glow", Float) = 0.6
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

            sampler2D _Snapshot;
            float _FlipY;
            float4 _Size;
            float4 _Center;
            float _Radius;
            float _Feather;
            fixed4 _Core;
            fixed4 _Edge;
            float _Glow;
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
                float2 p = i.uv * _Size.xy;
                float2 suv = float2(i.uv.x, lerp(i.uv.y, 1.0 - i.uv.y, _FlipY));
                fixed3 level = tex2D(_Snapshot, suv).rgb * (1.0 - _Dim);
                float d = distance(p, _Center.xy);
                float r = d / max(_Radius, 0.0001);
                float inside = 1.0 - smoothstep(_Radius - _Feather, _Radius, d);
                // Bright core falling off to the edge colour, with a lit band just inside the rim.
                fixed3 orb = lerp(_Core.rgb, _Edge.rgb, smoothstep(0.0, 1.0, r));
                orb += _Core.rgb * 0.35 * smoothstep(0.75, 0.95, r) * (1.0 - smoothstep(0.95, 1.0, r));
                float halo = exp(-max(d - _Radius, 0.0) / max(_Radius * 0.35, 1.0)) * _Glow * (1.0 - inside);
                fixed3 rgb = lerp(level, orb, inside) + _Core.rgb * halo * 0.5;
                return fixed4(saturate(rgb), 1) * i.color;
            }
            ENDCG
        }
    }
}
