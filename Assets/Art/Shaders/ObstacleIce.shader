// Ice block over a tube, lit as a thick rounded slab; it cracks into bevelled chunks that break off in depth.
Shader "Project/Obstacle Ice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HalfWidth ("Ice half width in u", Float) = 0.44
        _Aspect ("Art height over width", Float) = 2.667
        _LightDir ("Light direction", Vector) = (-0.45, 0.65, 0.62, 0)
        _Shade ("Shade on the side away from the light", Range(0, 1)) = 0.7
        _Lit ("Light on the side facing it", Range(1, 1.5)) = 1.1
        _Round ("Roundness of the slab's sides", Range(0, 1.5)) = 0.85
        _Bump ("Relief from the art", Range(0, 8)) = 1.2
        _RimColor ("Edge light", Color) = (0.86, 0.98, 1, 1)
        _RimGain ("Edge light strength", Range(0, 1)) = 0.32
        _SpecCut ("Glint size (higher is smaller)", Range(0.85, 1)) = 0.935
        _SpecGain ("Glint strength", Range(0, 1)) = 0.55
        _DeepColor ("Inner depth tint", Color) = (0.36, 0.6, 0.95, 1)
        _Depth ("Inner layer shift", Range(0, 0.1)) = 0.035
        _DepthGain ("Inner layer strength", Range(0, 1)) = 0.35
        _ShineColor ("Light drift", Color) = (0.92, 1, 1, 1)
        _ShineGain ("Light drift strength", Range(0, 1)) = 0.1
        _ShineWidth ("Light drift width", Range(0.02, 0.5)) = 0.12
        _ShinePeriod ("Light drift period", Range(2, 20)) = 8
        _ShineSlope ("Light drift slope", Range(0, 2)) = 0.8
        [PerRendererData] _Charge ("Charge (edges brighten before the break)", Range(0, 1)) = 0
        [PerRendererData] _Crack ("Crack reach (0 none, 1 every chunk)", Range(0, 1)) = 0
        [PerRendererData] _CrackOrigin ("Crack origin and its ring radius, in ice units", Vector) = (0.5, 1.33, 0.28, 0)
        _CrackColor ("Light inside the cracks", Color) = (0.55, 0.9, 1, 1)
        _CrackInk ("Crack depth", Color) = (0.1, 0.2, 0.45, 1)
        _CrackWidth ("Crack gap half width", Range(0.002, 0.03)) = 0.01
        _Bevel ("Chunk bevel width", Range(0.005, 0.08)) = 0.05
        _BevelTilt ("Chunk bevel slope", Range(0, 3)) = 1.8
        _Facet ("Chunk facet tilt", Range(0, 2)) = 1.3
        // x chunk index, w on; a chunk alone, seen with its side walls when it turns.
        [PerRendererData] _Piece ("Chunk", Vector) = (0, 0, 0, 0)
        [PerRendererData] _Tilt ("Chunk facing (z toward the viewer)", Vector) = (0, 0, 1, 0)
        _Thickness ("Chunk thickness", Range(0, 0.2)) = 0.09
        _SideColor ("Chunk side", Color) = (0.42, 0.68, 0.95, 1)
        [PerRendererData] _Dissolve ("Chunk turned to dust (0 whole, 1 gone)", Range(0, 1)) = 0
        // World rect a chunk stays inside, so it never covers a neighbour: x min, y min, x max, y max.
        [PerRendererData] _Clip ("Chunk clip rect", Vector) = (-100000, -100000, 100000, 100000)
        _DustColor ("Dust", Color) = (0.8, 0.95, 1, 1)
        _DustSize ("Dust grain size", Range(0.005, 0.1)) = 0.045
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            // Must match FrozenTubeItem.ChunkCount.
            #define CHUNKS 7

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
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _HalfWidth, _Aspect, _Shade, _Lit, _Round, _Bump, _RimGain, _SpecCut, _SpecGain, _Depth, _DepthGain;
            float _ShineGain, _ShineWidth, _ShinePeriod, _ShineSlope;
            float _Charge, _Crack, _CrackWidth, _Bevel, _BevelTilt, _Facet, _Thickness, _Dissolve, _DustSize;
            float4 _LightDir, _CrackOrigin, _Piece, _Tilt, _Clip;
            fixed4 _RimColor, _DeepColor, _ShineColor, _CrackColor, _CrackInk, _SideColor, _DustColor;
            // xy chunk seed in ice units (u across, v times aspect up), zw its facet tilt; set from FrozenTubeItem.
            float4 _Chunks[CHUNKS];

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Nearest chunk, distance to its border and the direction across that border.
            int Chunk(float2 q, out float edge, out float2 across)
            {
                int near = 0;
                float best = 1e9;
                for (int k = 0; k < CHUNKS; k++)
                {
                    float2 d = q - _Chunks[k].xy;
                    float dd = dot(d, d);
                    if (dd < best) { best = dd; near = k; }
                }
                float2 seed = _Chunks[near].xy;
                edge = 1e9;
                across = float2(0, 1);
                for (int j = 0; j < CHUNKS; j++)
                {
                    if (j == near) continue;
                    float2 dir = normalize(_Chunks[j].xy - seed);
                    float e = dot((seed + _Chunks[j].xy) * 0.5 - q, dir);
                    if (e < edge) { edge = e; across = dir; }
                }
                return near;
            }

            float Luma(float2 uv)
            {
                return dot(tex2D(_MainTex, uv).rgb, float3(0.3, 0.59, 0.11));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float x = (i.uv.x - 0.5) / _HalfWidth;
                float2 q = float2(i.uv.x, i.uv.y * _Aspect);

                // The slab is flat in front and rounds off toward its sides; the art's relief lifts its swirls.
                float side = sign(x) * pow(saturate(abs(x)), 2.5) * _Round;
                float2 t = _MainTex_TexelSize.xy * 2;
                float2 relief = float2(Luma(i.uv + float2(t.x, 0)) - Luma(i.uv - float2(t.x, 0)),
                                       Luma(i.uv + float2(0, t.y)) - Luma(i.uv - float2(0, t.y))) * _Bump;
                float3 n = float3(side - relief.x, -relief.y, 1);

                // Cracks run out from the badge ring; each chunk reached takes its own facet and bevels into the gap.
                float edge;
                float2 across;
                int chunk = Chunk(q, edge, across);
                float reach = _Crack * 1.5;
                float from = length(q - _CrackOrigin.xy) - _CrackOrigin.z;
                float aa = max(fwidth(edge), 1e-4);
                float shown = step(1e-3, _Crack) * (1 - smoothstep(reach - 0.02, reach + 0.02, from));
                float seedFrom = length(_Chunks[chunk].xy - _CrackOrigin.xy) - _CrackOrigin.z;
                float facet = saturate((reach - seedFrom) / 0.25) * step(1e-3, _Crack);
                float piece = _Piece.w;
                facet = max(facet, piece);
                shown = max(shown, piece);
                n.xy += _Chunks[chunk].zw * _Facet * facet;
                float bevel = (1 - smoothstep(_CrackWidth, _CrackWidth + _Bevel, edge)) * shown;
                n.xy += across * bevel * _BevelTilt;

                // A turning chunk carries its facing into the light.
                n.xy += _Tilt.xy;
                n.z *= max(abs(_Tilt.z), 0.05);
                n = normalize(n);

                float3 l = normalize(_LightDir.xyz);
                float diffuse = saturate(dot(n, l) * 0.5 + 0.5);
                // The inner layer sits deeper, so it shifts against the surface's slope.
                fixed3 deep = tex2D(_MainTex, i.uv - n.xy * _Depth).rgb * _DeepColor.rgb;
                fixed3 rgb = lerp(tex.rgb, deep, _DepthGain * n.z);
                rgb *= lerp(_Shade, _Lit, diffuse);
                float fresnel = pow(1 - saturate(n.z), 2);
                rgb += _RimColor.rgb * fresnel * _RimGain * (1 + _Charge * 1.5);
                rgb += _RimColor.rgb * _Charge * 0.08;
                float3 h = normalize(l + float3(0, 0, 1));
                float glint = smoothstep(_SpecCut - 0.006, _SpecCut + 0.006, dot(n, h));
                rgb += glint * _SpecGain;

                // A faint diagonal light drifts up the ice now and then; tubes along a row follow a little later.
                float s = i.uv.y * _Aspect * 0.5 + x * _ShineSlope;
                float pos = lerp(-1, _Aspect * 0.5 + 1.5, frac((_Time.y - i.world.x * 0.2) / _ShinePeriod));
                float d = (s - pos) / _ShineWidth;
                rgb += _ShineColor.rgb * exp(-d * d) * _ShineGain * (1 - piece);

                // The gap itself: light caught on its walls, dark at its bottom, burning bright at the newest stretch.
                float gap = (1 - smoothstep(_CrackWidth - aa, _CrackWidth + aa, edge)) * shown;
                float tip = 1 - saturate((reach - from) / 0.2);
                float bottom = 1 - smoothstep(0, _CrackWidth * 0.6, edge);
                fixed3 inside = lerp(_CrackColor.rgb, _CrackInk.rgb, bottom * (1 - tip));
                inside = lerp(inside, fixed3(1, 1, 1), tip * 0.6);
                rgb = lerp(rgb, inside, gap * (1 - piece));

                float alpha = tex.a * i.color.a;
                if (piece > 0.5)
                {
                    // Only this chunk's face; the gap falls away.
                    float face = (float)(chunk == (int)_Piece.x) * smoothstep(_CrackWidth - aa, _CrackWidth + aa, edge);
                    // Its back outline shows behind the face as a side wall, opposite to where it faces.
                    float wall = 0;
                    for (int k = 1; k <= 3; k++)
                    {
                        float2 back = q + _Tilt.xy * _Thickness * (k / 3.0);
                        float be;
                        float2 ba;
                        float inChunk = (float)(Chunk(back, be, ba) == (int)_Piece.x);
                        wall = max(wall, inChunk * step(_CrackWidth, be) * tex2D(_MainTex, float2(back.x, back.y / _Aspect)).a);
                    }
                    float wallLight = saturate(dot(normalize(float3(-_Tilt.xy, 0.4)), l) * 0.5 + 0.5);
                    fixed3 sideRgb = _SideColor.rgb * lerp(0.55, 1.15, wallLight);
                    rgb = lerp(sideRgb, rgb, face);
                    alpha = max(alpha * face, wall * i.color.a);

                    // It breaks up into round grains that shrink away, each at its own pace, turning to pale stardust.
                    float2 grain = floor(q / _DustSize);
                    float2 cell = frac(q / _DustSize) - 0.5;
                    float h = frac(sin(dot(grain, float2(12.9898, 78.233))) * 43758.5453);
                    float radius = 0.75 * (1 - saturate(_Dissolve * 1.6 - h * 0.6));
                    float r = length(cell);
                    float ra = max(fwidth(r), 1e-4);
                    alpha *= 1 - smoothstep(radius - ra, radius + ra, r);
                    rgb = lerp(rgb, _DustColor.rgb, saturate(_Dissolve * 1.4));

                    // Fade out softly before the tube's own edges.
                    float2 inside = min(i.world - _Clip.xy, _Clip.zw - i.world);
                    alpha *= saturate(min(inside.x, inside.y) / 0.12);
                }
                return fixed4(rgb * i.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
