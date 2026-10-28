// The island's land bent like one sheet (IslandBend.cs): a vertex moves up by the height of the land under
// it, read by world position from a texture of heights at the grid's corners. On top of that the coast gives
// under a hit (CoastBounce.cs): a second texture, of pushes across the screen at the same corners, read the
// same way. Each layer's switch is 0 unless it is running, and the weight is the material's own (0 = never
// moves).
#ifndef LITTLE_PEEPS_ISLAND_BEND_INCLUDED
#define LITTLE_PEEPS_ISLAND_BEND_INCLUDED

TEXTURE2D(_IslandBendTex);
SAMPLER(sampler_IslandBendTex);
float4 _IslandBendRect;   // xy: world position the texture starts at; zw: 1 / its world size
float _IslandBendOn;

TEXTURE2D(_CoastBounceTex);
SAMPLER(sampler_CoastBounceTex);
float4 _CoastBounceRect;  // as _IslandBendRect
float _CoastBounceOn;

float3 IslandBendWorld(float3 positionWS, float weight)
{
    float2 uv = (positionWS.xy - _IslandBendRect.xy) * _IslandBendRect.zw;
    float lift = SAMPLE_TEXTURE2D_LOD(_IslandBendTex, sampler_IslandBendTex, uv, 0).r;
    float2 coastUV = (positionWS.xy - _CoastBounceRect.xy) * _CoastBounceRect.zw;
    float2 push = SAMPLE_TEXTURE2D_LOD(_CoastBounceTex, sampler_CoastBounceTex, coastUV, 0).rg;
    positionWS.y += lift * _IslandBendOn * weight;
    positionWS.xy += push * _CoastBounceOn * weight;
    return positionWS;
}

float4 IslandBendObjectToHClip(float3 positionOS, float weight)
{
    return TransformWorldToHClip(IslandBendWorld(TransformObjectToWorld(positionOS), weight));
}

#endif
