// Hidden orb shell: dark violet energy, a slow meridian light band, a crack light; a wedge mask cuts shards from it.
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
        [PerRendererData] _Crack ("Crack reach (0 none, 1 whole shell)", Range(0, 1)) = 0
        _CrackColor ("Crack light", Color) = (0.8, 0.97, 1, 1)
        _CrackScale ("Crack cells across the shell", Range(1, 8)) = 2.4
        _CrackWidth ("Crack line width", Range(0.01, 0.2)) = 0.035
        // x start angle, y span (radians), z edge jag (radians), w on.
        [PerRendererData] _Wedge ("Shard wedge", Vector) = (0, 0, 0, 0)
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

            float4 _Sphere, _Wedge;
            fixed4 _EnergyColor, _BandColor, _CrackColor;
            float _EnergyGain, _SwirlSpeed, _BandGain, _BandWidth, _BandPeriod, _BandTravel, _BandWave;
            float _Crack, _CrackScale, _CrackWidth;

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

                // The crack light grows from the centre: denser and brighter there, its lines thin.
                float reach = _Crack * 1.3;
                float front = smoothstep(reach, reach - 0.3, r) * step(1e-3, _Crack);
                float lines = CrackLine(p * _CrackScale / (0.55 + r * 0.6), _CrackWidth);
                rgb += _CrackColor.rgb * front * (lines * (1.3 - r * 0.6) + 0.06 * _Crack) * inside;

                // Shards: one wedge of the art; its sides wander the same way for every wedge, so they fit.
                float bend = _Wedge.z * (sin(r * 17) + 0.5 * sin(r * 31 + 1.7));
                float a = frac((angle + bend - _Wedge.x) / 6.2832) * 6.2832;
                float wedge = lerp(1, step(a, _Wedge.y), _Wedge.w);
                return OrbPortal(rgb, tex.a * wedge, i.world);
            }
            ENDCG
        }
    }
}
