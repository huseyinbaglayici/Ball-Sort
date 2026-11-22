// Ice over a tube: frost on its sides, a slow diagonal light, a crack light from an origin; a band mask cuts it into pieces.
Shader "Project/Obstacle Ice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HalfWidth ("Ice half width in u", Float) = 0.44
        _Aspect ("Art height over width", Float) = 2.667
        _FrostColor ("Frost edge", Color) = (0.9, 0.98, 1, 1)
        _FrostGain ("Frost edge strength", Range(0, 1)) = 0.35
        _FrostPower ("Frost edge falloff", Range(1, 12)) = 4
        _ShineColor ("Light drift", Color) = (0.92, 1, 1, 1)
        _ShineGain ("Light drift strength", Range(0, 1)) = 0.1
        _ShineWidth ("Light drift width", Range(0.02, 0.5)) = 0.12
        _ShinePeriod ("Light drift period", Range(2, 20)) = 8
        _ShineSlope ("Light drift slope", Range(0, 2)) = 0.8
        _CoolColor ("Cold tint", Color) = (0.62, 0.78, 1, 1)
        _CoolGain ("Cold tint strength", Range(0, 1)) = 0.12
        [PerRendererData] _Crack ("Crack reach (0 none, 1 whole ice)", Range(0, 1)) = 0
        [PerRendererData] _CrackGlow ("Crack light strength", Range(0, 2)) = 1
        [PerRendererData] _CrackOrigin ("Crack origin uv", Vector) = (0.5, 0.5, 0, 0)
        _CrackColor ("Crack light", Color) = (0.78, 0.97, 1, 1)
        _CrackScale ("Crack cells across the ice", Range(1, 10)) = 2.2
        _CrackWidth ("Crack line width", Range(0.01, 0.2)) = 0.035
        // x lower cut v, y upper cut v, z jag, w on.
        [PerRendererData] _Piece ("Piece band", Vector) = (0, 1, 0, 0)
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ObstacleCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float worldX : TEXCOORD1;
            };

            sampler2D _MainTex;
            float _HalfWidth, _Aspect, _FrostGain, _FrostPower, _ShineGain, _ShineWidth, _ShinePeriod, _ShineSlope, _CoolGain;
            float _Crack, _CrackGlow, _CrackScale, _CrackWidth;
            float4 _CrackOrigin, _Piece;
            fixed4 _FrostColor, _ShineColor, _CoolColor, _CrackColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldX = mul(unity_ObjectToWorld, v.vertex).x;
                return o;
            }

            // The jagged line between two pieces, the same for both.
            float Cut(float u, float v0, float jag)
            {
                return v0 + jag * (sin(u * 23 + v0 * 40) * 0.6 + sin(u * 9 + v0 * 17) * 0.4);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float x = (i.uv.x - 0.5) / _HalfWidth;
                fixed3 rgb = lerp(tex.rgb, tex.rgb * _CoolColor.rgb, _CoolGain);

                // Frost brightens toward the sides like a rounded block.
                rgb += _FrostColor.rgb * pow(saturate(abs(x)), _FrostPower) * _FrostGain;

                // A faint diagonal light drifts up the ice now and then; tubes along a row follow a little later.
                float s = i.uv.y * _Aspect * 0.5 + x * _ShineSlope;
                float pos = lerp(-1, _Aspect * 0.5 + 1.5, frac((_Time.y - i.worldX * 0.2) / _ShinePeriod));
                float d = (s - pos) / _ShineWidth;
                rgb += _ShineColor.rgb * exp(-d * d) * _ShineGain;

                // Crack light from the origin outward: small cells near it, wide ones far off, fading with distance.
                float2 q = (i.uv - _CrackOrigin.xy) * float2(1, _Aspect);
                float r = length(q) / (_Aspect * 0.55);
                float reach = _Crack * 1.2;
                float front = smoothstep(reach, reach - 0.25, r) * step(1e-3, _Crack);
                float lines = CrackLine(q * _CrackScale / (0.25 + r * 0.9), _CrackWidth);
                rgb += _CrackColor.rgb * front * (lines * 1.4 * (1 - 0.7 * saturate(r)) + 0.08) * _CrackGlow;

                // Pieces: one band between two jagged cuts.
                float lo = Cut(i.uv.x, _Piece.x, _Piece.z);
                float hi = Cut(i.uv.x, _Piece.y, _Piece.z);
                float aa = max(fwidth(i.uv.y), 1e-4);
                float band = smoothstep(lo - aa, lo + aa, i.uv.y) * (1 - smoothstep(hi - aa, hi + aa, i.uv.y));
                float alpha = tex.a * i.color.a * lerp(1, band, _Piece.w);
                return fixed4(rgb * i.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
