// Hidden orb shell: dark violet energy and a slow light band; one crack lights from the centre and splits it.
Shader "Project/Obstacle Shell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _PortalCut ("Portal edge", Vector) = (100000, 0, 1, 0)
        [PerRendererData] _PortalHeat ("Portal heat", Float) = 0
        _Sphere ("Sphere in uv (center x, center y, radius)", Vector) = (0.5, 0.5, 0.42, 0)
        _EnergyColor ("Energy", Color) = (0.2, 0.08, 0.5, 1)
        _EnergyGain ("Energy strength", Range(0, 1)) = 0.35
        _SwirlSpeed ("Energy swirl speed", Range(0, 2)) = 0.12
        _BandColor ("Light band", Color) = (0.75, 0.95, 1, 1)
        _BandGain ("Light band strength", Range(0, 2)) = 0.28
        _BandWidth ("Light band width (radians)", Range(0.02, 0.6)) = 0.14
        _BandPeriod ("Light band period", Range(1, 10)) = 3.6
        _BandTravel ("Share of the period the band moves", Range(0.1, 1)) = 0.55
        _BandWave ("Band delay per world unit", Range(0, 1)) = 0.18
        [PerRendererData] _Crack ("Crack reach (0 none, 1 whole line)", Range(0, 1)) = 0
        [PerRendererData] _CrackLight ("Crack strength", Range(0, 1)) = 1
        _CrackColor ("Crack light", Color) = (0.72, 0.96, 1, 1)
        _CrackInk ("Crack ink", Color) = (0.1, 0.04, 0.26, 1)
        _CrackWidth ("Crack line half width (sphere radii)", Range(0.01, 0.2)) = 0.06
        _CrackInkWidth ("Crack ink width", Range(0, 0.1)) = 0.035
        // x: +1 keeps the half above the crack, -1 the half below; y: on.
        [PerRendererData] _Half ("Shell half", Vector) = (0, 0, 0, 0)
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
            #include "ObstacleCommon.cginc"

            float4 _Sphere, _Half;
            fixed4 _EnergyColor, _BandColor, _CrackColor, _CrackInk;
            float _EnergyGain, _SwirlSpeed, _BandGain, _BandWidth, _BandPeriod, _BandTravel, _BandWave;
            float _Crack, _CrackLight, _CrackWidth, _CrackInkWidth;

            // The crack's zigzag across the sphere, left to right, in sphere radii; the halves part along it.
            static const float2 Zig[6] =
            {
                float2(-1.2, 0.14), float2(-0.52, -0.12), float2(-0.16, 0.17),
                float2(0.2, -0.13), float2(0.54, 0.11), float2(1.2, -0.06)
            };
            // Short side cracks from the zigzag's corners.
            static const float4 Twig[3] =
            {
                float4(-0.16, 0.17, -0.32, 0.52), float4(0.2, -0.13, 0.36, -0.5), float4(-0.52, -0.12, -0.7, -0.4)
            };

            float ZigY(float x)
            {
                float y = Zig[0].y;
                for (int k = 0; k < 5; k++)
                {
                    float t = saturate((x - Zig[k].x) / (Zig[k + 1].x - Zig[k].x));
                    y = x >= Zig[k].x ? lerp(Zig[k].y, Zig[k + 1].y, t) : y;
                }
                return y;
            }

            fixed4 frag(OrbV2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                float2 p = (i.uv - _Sphere.xy) / _Sphere.z;
                float r2 = dot(p, p);
                float r = sqrt(r2);
                float inside = 1 - smoothstep(0.85, 1, r);
                float nz = sqrt(saturate(1 - r2));

                // Energy turns slowly round the centre, darker toward violet.
                float angle = atan2(p.y, p.x);
                float swirl = 0.5 + 0.5 * sin(angle * 2 + r * 5 - _Time.y * _SwirlSpeed * 6.2832);
                fixed3 rgb = lerp(tex.rgb, tex.rgb * _EnergyColor.rgb * 2.2, _EnergyGain * swirl * inside);

                // A thin meridian sweeps across the sphere, then rests; neighbours follow a little later.
                float t = frac((_Time.y - i.world.x * _BandWave) / _BandPeriod) / _BandTravel;
                float lon = atan2(p.x, nz);
                float d = (lon - lerp(-1.9, 1.9, t)) / _BandWidth;
                float band = exp(-d * d) * step(t, 1) * inside * smoothstep(0, 0.35, nz);
                rgb += _BandColor.rgb * band * _BandGain;

                // The crack runs out from the centre both ways; its newest stretch burns white.
                float crackDist = 1e3;
                for (int k = 0; k < 5; k++) crackDist = min(crackDist, SegmentDistance(p, Zig[k], Zig[k + 1]));
                for (int b = 0; b < 3; b++) crackDist = min(crackDist, SegmentDistance(p, Twig[b].xy, Twig[b].zw) + 0.012);
                float taper = lerp(1, 0.55, saturate(r));
                float3 stroke = CrackStroke(crackDist, _CrackWidth * taper, _CrackInkWidth);
                float reach = _Crack * 1.15;
                float aa = max(fwidth(r), 1e-4);
                float shown = (1 - smoothstep(reach - aa, reach + aa, r)) * step(1e-3, _Crack);
                float tip = 1 - saturate((reach - r) / 0.25);
                rgb = ApplyCrack(rgb, stroke, shown * inside, tip, _CrackInk.rgb, _CrackColor.rgb, _CrackLight);

                // A half keeps its side of the zigzag.
                float side = (p.y - ZigY(p.x)) * _Half.x;
                float sa = max(fwidth(side), 1e-4);
                float keep = lerp(1, smoothstep(-sa, sa, side), _Half.y);
                return OrbPortal(rgb, tex.a * keep, i.world);
            }
            ENDCG
        }
    }
}
