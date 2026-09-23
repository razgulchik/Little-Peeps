// Returns a pseudo-random gradient vector based on an integer coordinate
float2 grad(int2 z)
{
    // 2D -> 1D hash
    int n = z.x + z.y * 11111;

    // Hugo Elias hash
    n = (n << 13) ^ n;
    n = (n * (n * n * 15731 + 789221) + 1376312589) >> 16;

    n &= 7;

    float2 gr = float2(n & 1, n >> 1) * 2.0 - 1.0;

    if (n >= 6)
        return float2(0.0, gr.x);
    else if (n >= 4)
        return float2(gr.x, 0.0);
    else
        return gr;
}

// Classic Perlin-style noise
float noise(float2 p)
{
    int2 i = int2(floor(p));
    float2 f = frac(p);

    // Smoothstep (you can replace with quintic version if desired)
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = dot(grad(i + int2(0, 0)), f - float2(0.0, 0.0));
    float b = dot(grad(i + int2(1, 0)), f - float2(1.0, 0.0));
    float c = dot(grad(i + int2(0, 1)), f - float2(0.0, 1.0));
    float d = dot(grad(i + int2(1, 1)), f - float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

void gradientNoiseF_float(float2 uv, float scale, out float Out)
{
    Out = noise(uv * scale);

}

void gradientNoiseF_half(half2 uv, half scale, out half Out)
{
    Out = noise(uv * scale);

}
