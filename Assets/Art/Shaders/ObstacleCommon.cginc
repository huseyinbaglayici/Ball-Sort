// Shared by the obstacle shaders: crack strokes drawn as bold cartoon lines, a dark ink edge round a lit core with a white heart.
#ifndef OBSTACLE_COMMON_INCLUDED
#define OBSTACLE_COMMON_INCLUDED

// Distance from p to segment ab.
float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * h);
}

// x: lit core, y: ink edge around it, z: white heart in the core; all crisp, antialiased.
float3 CrackStroke(float d, float width, float ink)
{
    float aa = max(fwidth(d), 1e-4);
    float core = 1 - smoothstep(width - aa, width + aa, d);
    float edge = 1 - smoothstep(width + ink - aa, width + ink + aa, d);
    float heart = 1 - smoothstep(width * 0.4 - aa, width * 0.4 + aa, d);
    return float3(core, edge - core, heart);
}

// Shades a texel with a revealed stroke; the newest part of the line burns white, strength fades it.
fixed3 ApplyCrack(fixed3 rgb, float3 stroke, float shown, float tip, fixed3 ink, fixed3 light, float strength)
{
    float k = shown * strength;
    rgb = lerp(rgb, ink, stroke.y * k);
    rgb = lerp(rgb, lerp(light, fixed3(1, 1, 1), tip), stroke.x * k);
    return lerp(rgb, fixed3(1, 1, 1), stroke.z * k);
}

// A sharp-cornered wave in -1..1.
float Zigzag(float x)
{
    return abs(frac(x) - 0.5) * 4 - 1;
}

#endif
