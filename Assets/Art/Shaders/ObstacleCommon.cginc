// Shared by the obstacle shaders: a cheap cell pattern whose borders read as cracks.
#ifndef OBSTACLE_COMMON_INCLUDED
#define OBSTACLE_COMMON_INCLUDED

float2 ObstacleHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

// Distance to the nearest cell border (second minus first nearest feature).
float CrackEdge(float2 p)
{
    float2 n = floor(p);
    float2 f = frac(p);
    float f1 = 8, f2 = 8;
    for (int j = -1; j <= 1; j++)
    for (int i = -1; i <= 1; i++)
    {
        float2 g = float2(i, j);
        float2 d = g + ObstacleHash2(n + g) * 0.8 + 0.1 - f;
        float dd = dot(d, d);
        if (dd < f1) { f2 = f1; f1 = dd; }
        else if (dd < f2) f2 = dd;
    }
    return sqrt(f2) - sqrt(f1);
}

// 1 on a crack line of the given width, antialiased.
float CrackLine(float2 p, float width)
{
    float e = CrackEdge(p);
    float aa = max(fwidth(e), 1e-4);
    return 1 - smoothstep(width - aa, width + aa, e);
}

#endif
