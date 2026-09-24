// The island's land bent like one sheet (IslandBend.cs): a vertex moves up by the height of the land under
// it, read by world position from a texture of heights at the grid's corners. The switch is 0 unless a bend
// is running, and the weight is the material's own (0 = never bends).
#ifndef LITTLE_PEEPS_ISLAND_BEND_INCLUDED
#define LITTLE_PEEPS_ISLAND_BEND_INCLUDED

TEXTURE2D(_IslandBendTex);
SAMPLER(sampler_IslandBendTex);
float4 _IslandBendRect;   // xy: world position the texture starts at; zw: 1 / its world size
float _IslandBendOn;

float3 IslandBendWorld(float3 positionWS, float weight)
{
    float2 uv = (positionWS.xy - _IslandBendRect.xy) * _IslandBendRect.zw;
    float lift = SAMPLE_TEXTURE2D_LOD(_IslandBendTex, sampler_IslandBendTex, uv, 0).r;
    positionWS.y += lift * _IslandBendOn * weight;
    return positionWS;
}

float4 IslandBendObjectToHClip(float3 positionOS, float weight)
{
    return TransformWorldToHClip(IslandBendWorld(TransformObjectToWorld(positionOS), weight));
}

#endif
