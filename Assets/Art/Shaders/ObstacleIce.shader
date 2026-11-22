// Ice over a tube: frost on its sides, a slow diagonal light; bold crack strokes run from the badge and become the cuts it splits along.
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
        [PerRendererData] _Charge ("Charge (frost brightens before the hit)", Range(0, 1)) = 0
        [PerRendererData] _Crack ("Crack reach (0 none, 1 every stroke)", Range(0, 1)) = 0
        [PerRendererData] _CrackLight ("Crack strength", Range(0, 1)) = 1
        [PerRendererData] _CrackOrigin ("Crack origin uv (the badge)", Vector) = (0.5, 0.5, 0, 0)
        // x lower cut v, y upper cut v, z jag.
        [PerRendererData] _Cuts ("Cut lines", Vector) = (0.34, 0.66, 0.025, 0)
        _CrackColor ("Crack light", Color) = (0.42, 0.86, 1, 1)
        _CrackInk ("Crack ink", Color) = (0.12, 0.2, 0.5, 1)
        _CrackWidth ("Crack line half width in u", Range(0.002, 0.05)) = 0.02
        _CrackInkWidth ("Crack ink width in u", Range(0, 0.05)) = 0.009
        // x lower cut v, y upper cut v, w on.
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
            float _Charge, _Crack, _CrackLight, _CrackWidth, _CrackInkWidth;
            float4 _CrackOrigin, _Cuts, _Piece;
            fixed4 _FrostColor, _ShineColor, _CoolColor, _CrackColor, _CrackInk;

            // Strokes from the badge's ring to the cuts, and short twigs off the cuts; x across, y up from the badge, in u.
            static const float4 Strokes[8] =
            {
                float4(0.035, 0.27, -0.04, 0.34), float4(-0.04, 0.34, 0.02, 0.45),
                float4(-0.03, -0.27, 0.045, -0.35), float4(0.045, -0.35, -0.02, -0.45),
                float4(0.22, 0.44, 0.3, 0.66), float4(-0.25, 0.43, -0.31, 0.6),
                float4(-0.2, -0.44, -0.28, -0.68), float4(0.26, -0.45, 0.33, -0.6)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldX = mul(unity_ObjectToWorld, v.vertex).x;
                return o;
            }

            // The jagged line between two pieces, the same for both and for its crack stroke.
            float Cut(float u, float v0, float jag)
            {
                return v0 + jag * (Zigzag(u * 2.6 + v0 * 7) * 0.7 + Zigzag(u * 6.1 + v0 * 13) * 0.3);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float x = (i.uv.x - 0.5) / _HalfWidth;
                fixed3 rgb = lerp(tex.rgb, tex.rgb * _CoolColor.rgb, _CoolGain);

                // Frost brightens toward the sides like a rounded block, more while it charges.
                rgb += _FrostColor.rgb * pow(saturate(abs(x)), _FrostPower) * _FrostGain * (1 + _Charge * 2);
                rgb += _FrostColor.rgb * _Charge * 0.12;

                // A faint diagonal light drifts up the ice now and then; tubes along a row follow a little later.
                float s = i.uv.y * _Aspect * 0.5 + x * _ShineSlope;
                float pos = lerp(-1, _Aspect * 0.5 + 1.5, frac((_Time.y - i.worldX * 0.2) / _ShinePeriod));
                float d = (s - pos) / _ShineWidth;
                rgb += _ShineColor.rgb * exp(-d * d) * _ShineGain;

                // Crack strokes in u units from the badge; the light runs along them out to the ends.
                float2 q = float2(i.uv.x - _CrackOrigin.x, (i.uv.y - _CrackOrigin.y) * _Aspect);
                float lo = Cut(i.uv.x, _Cuts.x, _Cuts.z);
                float hi = Cut(i.uv.x, _Cuts.y, _Cuts.z);
                float crackDist = min(abs(i.uv.y - lo), abs(i.uv.y - hi)) * _Aspect;
                crackDist = lerp(1e3, crackDist, step(abs(q.x), _HalfWidth * 0.98));
                for (int k = 0; k < 8; k++) crackDist = min(crackDist, SegmentDistance(q, Strokes[k].xy, Strokes[k].zw) + (k >= 4 ? 0.003 : 0));
                float3 stroke = CrackStroke(crackDist, _CrackWidth, _CrackInkWidth);
                float ay = abs(q.y);
                float path = min(max(ay - 0.27, 0), 0.17) + abs(q.x) + max(ay - 0.44, 0);
                float reach = _Crack * 0.95;
                float aa = max(fwidth(path), 1e-4);
                float shown = (1 - smoothstep(reach - aa, reach + aa, path)) * step(1e-3, _Crack) * step(0.25, length(q));
                float tip = 1 - saturate((reach - path) / 0.12);
                rgb = ApplyCrack(rgb, stroke, shown, tip, _CrackInk.rgb, _CrackColor.rgb, _CrackLight);

                // Pieces: one band between two jagged cuts.
                float pa = max(fwidth(i.uv.y), 1e-4);
                float pieceLo = Cut(i.uv.x, _Piece.x, _Cuts.z);
                float pieceHi = Cut(i.uv.x, _Piece.y, _Cuts.z);
                float band = smoothstep(pieceLo - pa, pieceLo + pa, i.uv.y) * (1 - smoothstep(pieceHi - pa, pieceHi + pa, i.uv.y));
                float alpha = tex.a * i.color.a * lerp(1, band, _Piece.w);
                return fixed4(rgb * i.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
