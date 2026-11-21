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
                float cover = smoothstep(_Radius - _Feather, _Radius, d);
                fixed4 c = _Color * i.color;
                c.a *= cover;
                return c;
            }
            ENDCG
        }
    }
}
