// The river's water kept alive from its still art (river_tile_*): a stand-in until the artist draws frames.
// "Little Peeps/Sprite Lit Bend" runs it with _RIVER_WATER on (RiverMaterial).
//
// Glints, like a fish at play. Every texel of the art is read as shallow (the light band along the bank), deep
// or neither, by how near its colour is to _RiverShallow / _RiverDeep. _RiverGlintShare of the deep texels away
// from the band each light up now and then, on clocks of their own: three frames of _RiverGlintFrame —
// half-way to shallow, shallow, half-way — once a _RiverGlintCycle.
//
// Nothing flows: a straight piece is laid both ways round, so a current would run against itself from cell to
// cell. A tide at the banks was tried (in, then out a texel) and dropped.
//
// Neighbours are read with Load, clamped to the sprite's own texture: the sprite must be the whole texture (not
// packed into an atlas), or the edge texels would read their neighbours on the sheet. The texture's size comes
// from the texture itself (GetDimensions), not from _MainTex_TexelSize: sprites go through the SRP Batcher,
// which hands the shader the material's per-material values — and the material has no texture of its own.
// The cell a texel is in is the whole world units under it (cells are one unit) — for the hash only. A texel
// never straddles two: the art is pixels per unit to the grid. Time is _Time, unscaled
// (UnscaledShaderTimeFeature).
//
// Include after the pass's UnityPerMaterial (with the _River* fields) and _MainTex.
#ifndef LITTLE_PEEPS_RIVER_WATER_INCLUDED
#define LITTLE_PEEPS_RIVER_WATER_INCLUDED

#define RIVER_OTHER 0
#define RIVER_SHALLOW 1
#define RIVER_DEEP 2

static const int2 RiverSides[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

// A number in 0..1 from a place, the same every frame ("Hash without Sine", Dave Hoskins).
float RiverHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

int RiverKind(int2 texel, int2 size)
{
    half4 c = LOAD_TEXTURE2D(_MainTex, clamp(texel, int2(0, 0), size - 1));
    if (c.a < 0.5h) return RIVER_OTHER;
    if (distance(c.rgb, _RiverShallow.rgb) <= _RiverMatch) return RIVER_SHALLOW;
    if (distance(c.rgb, _RiverDeep.rgb) <= _RiverMatch) return RIVER_DEEP;
    return RIVER_OTHER;
}

// The art's texel at uv as the water shows it now. positionWS: where it is in the world, before any bend.
half4 RiverWater(half4 texel, float2 uv, float2 positionWS)
{
    uint width, height;
    _MainTex.GetDimensions(width, height);
    int2 size = int2(width, height);
    int2 p = clamp((int2)floor(uv * float2(size)), int2(0, 0), size - 1);
    if (RiverKind(p, size) != RIVER_DEEP) return texel;

    [unroll] for (int s = 0; s < 4; s++)
        if (RiverKind(p + RiverSides[s], size) == RIVER_SHALLOW) return texel;

    float2 place = float2(p) + floor(positionWS) * 17.0;
    if (RiverHash(place + 101.0) >= _RiverGlintShare) return texel;

    float cycle = max(3.0 * _RiverGlintFrame, _RiverGlintCycle);
    float since = frac(_Time.y / cycle + RiverHash(place + 211.0)) * cycle;
    int frame = (int)floor(since / max(0.01, _RiverGlintFrame));
    if (frame == 1) return half4(_RiverShallow.rgb, texel.a);
    if (frame == 0 || frame == 2) return half4(lerp(_RiverDeep.rgb, _RiverShallow.rgb, 0.5h), texel.a);
    return texel;
}

#endif
