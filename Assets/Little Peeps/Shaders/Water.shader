// The sea: one flat colour with flow strokes drifting over it. The strokes are the artist's tile (_FlowTex):
// white where a stroke is, clear elsewhere, tinted by _FlowColor.
//
// Drawn on a quad whose vertices are the screen's corners (WaterSystem makes it): the sea covers the whole
// frame wherever the camera is, so nothing has to follow the camera and nothing falls a frame behind a pan.
// Each pixel finds its place in the world through the camera's own inverse view-projection — the matrices URP
// draws everything else with, render-texture flip and Pixel Perfect Camera included.
//
// One texel of the tile is one art pixel (or a whole number of them, _FlowScale), so a stroke is as thick and
// as long as it is drawn. It drifts smoothly — a screen pixel at a time, not an art pixel — like everything
// else that moves in the game.
//
// Every stroke lives on its own: through a cycle of _FlowCycle seconds it is seen for the first _FlowVisible of
// it. Over the first _FlowEase of that it grows from its middle out to its full length while it fades in; over
// the last _FlowEase it draws back to its middle while it fades out. All the while it is brightest in the middle
// and _FlowEnds as strong at its ends. Its place in the cycle comes from where it starts in the world, so every
// copy of the tile across the sea has its strokes on clocks of their own.
//
// The shader cannot see where a stroke starts in the tile itself: WaterSystem hands it a map of the tile
// (WaterFlowStrokes) — per texel, its offset into its stroke and the stroke's length — read with Load, wrapped by
// hand: the exact texel, whatever the filter or wrap.
//
// Time is _Time, which UnscaledShaderTimeFeature feeds from unscaled time: the sea keeps flowing on pauses.
Shader "Little Peeps/Water"
{
    Properties
    {
        _Color ("Sea Colour", Color) = (0.05098039, 0.5294118, 0.8039216, 1)
        [NoScaleOffset] _FlowTex ("Strokes (one texel = one art pixel; Read/Write on; read as the game starts)", 2D) = "black" {}
        _FlowColor ("Stroke Colour (alpha = strength)", Color) = (0.3529412, 0.7960784, 0.9607843, 1)
        [IntRange] _FlowScale ("Stroke Scale (art pixels per texel)", Range(1, 4)) = 1
        _FlowVelocity ("Flow (art pixels per second: x, y)", Vector) = (-3, 0, 0, 0)
        _FlowCycle ("Stroke Cycle (seconds)", Float) = 6
        _FlowVisible ("Share Of The Cycle Seen", Range(0, 1)) = 0.6
        _FlowEase ("Share Of That Growing (And As Much Shrinking)", Range(0, 0.5)) = 0.3
        _FlowEnds ("Strength At The Ends (Middle = 1)", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }

        // The bottom of everything: it writes its colour over whatever the camera cleared to.
        Blend Off
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 positionWS : TEXCOORD0;
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _FlowColor;
            float _FlowScale;
            float4 _FlowVelocity;
            float _FlowCycle;
            float _FlowVisible;
            float _FlowEase;
            float _FlowEnds;
        CBUFFER_END

        // Not material properties: WaterSystem sets them on the renderer. The art's pixels per unit, so the grid
        // has one source; the strokes' map and its size in texels (0 = no map, no strokes).
        float _ArtPixelsPerUnit;
        TEXTURE2D(_FlowStrokes);
        float4 _FlowStrokesSize;

        // A number in 0..1 from a place, the same every frame ("Hash without Sine", Dave Hoskins).
        float Hash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        Varyings SeaVertex(Attributes v)
        {
            Varyings o;
            // The vertices are (±1, ±1): the screen's corners in clip space. The depth is inside the clip range
            // on every API (0..1 and -1..1), and nothing tests against it.
            o.positionCS = float4(v.positionOS.xy, 0.5, 1.0);
            float4 world = mul(UNITY_MATRIX_I_VP, o.positionCS);
            o.positionWS = world.xy / world.w;
            return o;
        }

        half4 SeaFragment(Varyings i) : SV_Target
        {
            half4 sea = half4(_Color.rgb, 1.0h);
            float2 size = _FlowStrokesSize.xy;
            if (size.x < 1.0) return sea;

            // The texel of the endless tiled strokes that has drifted onto this screen pixel. The drift is not
            // rounded, so the strokes slide by screen pixels.
            float2 drifted = i.positionWS * _ArtPixelsPerUnit - _Time.y * _FlowVelocity.xy;
            float2 texel = floor(drifted / max(1.0, _FlowScale));
            int2 coord = (int2)(texel - floor(texel / size) * size);
            half4 map = LOAD_TEXTURE2D(_FlowStrokes, coord);
            if (map.a < 0.5h) return sea;

            float along = round(map.r * 255.0);          // pixels from the stroke's first one
            float strokeLength = round(map.g * 255.0);

            // Where in its cycle the stroke is, by where it starts: the same for all its pixels, different in
            // every copy of the tile.
            float life = frac(_Time.y / max(0.01, _FlowCycle) + Hash(float2(texel.x - along, texel.y)));
            float seen = life / max(0.0001, _FlowVisible);                                  // 0..1 while seen
            float grown = saturate(min(seen, 1.0 - seen) / max(0.0001, _FlowEase));        // 0, up to 1, holds, down

            // Grown from the middle: a pixel shows once the stroke has reached it.
            float halfLength = 0.5 * strokeLength;
            float fromMiddle = abs(along + 0.5 - halfLength) / halfLength;                  // 0 middle .. ~1 ends
            float reached = fromMiddle <= grown ? 1.0 : 0.0;

            float strength = map.b * _FlowColor.a * grown * reached * lerp(1.0, _FlowEnds, fromMiddle);
            return half4(lerp(_Color.rgb, _FlowColor.rgb, strength), 1.0h);
        }
        ENDHLSL

        // The 2D Renderer draws this pass...
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment
            ENDHLSL
        }

        // ...a Universal (forward) Renderer this one.
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment
            ENDHLSL
        }
    }
}
