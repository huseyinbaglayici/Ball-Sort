// Shared by the orb layer shaders: the portal cut hides a ball below a ring's lower edge (world space).
#ifndef ORB_COMMON_INCLUDED
#define ORB_COMMON_INCLUDED

#include "UnityCG.cginc"

struct OrbAppdata
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

struct OrbV2f
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 world : TEXCOORD1;
};

sampler2D _MainTex;
float4 _PortalCut;
float _PortalHeat;

OrbV2f OrbVert(OrbAppdata v)
{
    OrbV2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    // Batched sprite vertices are already in world space, so this holds for them too.
    o.world = mul(unity_ObjectToWorld, v.vertex).xy;
    o.uv = v.uv;
    o.color = v.color;
    return o;
}

// Applies the portal cut and its heat to an already shaded texel; returns premultiplied color.
fixed4 OrbPortal(fixed3 rgb, fixed alpha, float2 world)
{
    // Lower arc of the ring's bottom ellipse at this column; nothing is hidden beside the ring.
    float u = (world.x - _PortalCut.y) / max(_PortalCut.z, 1e-5);
    float edge = _PortalCut.x - _PortalCut.w * sqrt(saturate(1 - u * u));
    float dy = edge - world.y;
    float aa = max(fwidth(dy), 1e-5);
    float au = max(fwidth(u), 1e-5);
    fixed beside = smoothstep(1 - au, 1 + au, abs(u));
    fixed visible = max(smoothstep(-aa, aa, dy), beside);

    // 0 one band height under the edge, 1 on it.
    fixed burn = step(1e-3, _PortalHeat) * (1 - beside)
        * (1 - saturate(dy / max(0.3 * _PortalCut.z * _PortalHeat, 1e-5)));
    fixed3 heat = lerp(lerp(fixed3(1.0, 0.85, 0.55), fixed3(0.85, 0.25, 0.75), smoothstep(0.2, 0.75, burn)),
        fixed3(0.08, 0.03, 0.2), smoothstep(0.65, 1, burn));
    #ifndef UNITY_COLORSPACE_GAMMA
    heat = GammaToLinearSpace(heat);
    #endif
    rgb = saturate(rgb + fixed3(1.0, 0.8, 0.5) * 0.35 * smoothstep(0, 0.5, burn) * (1 - burn));
    rgb = lerp(rgb, heat, burn * burn);

    alpha *= visible;
    return fixed4(rgb * alpha, alpha);
}

#endif
