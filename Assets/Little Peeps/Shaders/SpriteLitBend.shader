// URP's "Sprite-Lit-Default" — the island's own look, lights and all — whose vertices move with the land
// when the island bends (IslandBend.cs, IslandBend.hlsl). For the ground and trim tilemaps and for whatever
// is drawn as part of the land (river, mountain). Off a bend it draws exactly like Sprite-Lit-Default.
// Copied from URP 17's Shaders/2D/Sprite-Lit-Default.shader; the only changes are the bend: the weight
// property, the include and the vertex positions — and, behind _RIVER_WATER, the river's water (RiverWater.hlsl),
// which changes the texel before the tint and the light.
Shader "Little Peeps/Sprite Lit Bend"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _IslandBendWeight("Bends With The Island", Range(0, 1)) = 1

        // The river's water (RiverWater.hlsl): off for everything else drawn with this shader.
        [Header(River Water)]
        [Toggle(_RIVER_WATER)] _RiverWater("River Water (glints)", Float) = 0
        _RiverShallow("Shallow Colour (as in the art)", Color) = (0.2, 0.572549, 1, 1)
        _RiverDeep("Deep Colour (as in the art)", Color) = (0.3019608, 0.4627451, 0.6666667, 1)
        _RiverMatch("Colour Match (distance)", Range(0, 0.5)) = 0.08
        _RiverGlintShare("Share Of Deep Texels That Glint", Range(0, 0.5)) = 0.04
        _RiverGlintCycle("Glint Cycle (seconds)", Float) = 4
        _RiverGlintFrame("Glint Frame (seconds)", Float) = 0.2

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "IslandBend.hlsl"

            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma shader_feature_local _RIVER_WATER
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 normal       : NORMAL;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                half2   lightingUV  : TEXCOORD1;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                half3  normalWS     : TEXCOORD3;
                #endif
                #if defined(_RIVER_WATER)
                float2  riverWS     : TEXCOORD4;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_MainTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _IslandBendWeight;
                half4 _RiverShallow;
                half4 _RiverDeep;
                float _RiverMatch;
                float _RiverGlintShare;
                float _RiverGlintCycle;
                float _RiverGlintFrame;
            CBUFFER_END

            #include "RiverWater.hlsl"

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(v);

                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = IslandBendObjectToHClip(v.positionOS, _IslandBendWeight);
                #if defined(DEBUG_DISPLAY)
                o.positionWS = IslandBendWorld(TransformObjectToWorld(v.positionOS), _IslandBendWeight);
                o.normalWS = TransformObjectToWorldDir(v.normal);
                #endif
                o.uv = v.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                #if defined(_RIVER_WATER)
                o.riverWS = TransformObjectToWorld(v.positionOS).xy;
                #endif

                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                #if defined(_RIVER_WATER)
                texel = RiverWater(texel, i.uv, i.riverWS);
                #endif
                const half4 main = i.color * texel;
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);

#if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, i.positionWS, i.positionCS, _MainTex);
                surfaceData.normalWS = i.normalWS;
#endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "IslandBend.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 normal       : NORMAL;
                float4 tangent      : TANGENT;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                half4   color           : COLOR;
                float2  uv              : TEXCOORD0;
                half3   normalWS        : TEXCOORD1;
                half3   tangentWS       : TEXCOORD2;
                half3   bitangentWS     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                float _IslandBendWeight;
                half4 _RiverShallow;
                half4 _RiverDeep;
                float _RiverMatch;
                float _RiverGlintShare;
                float _RiverGlintCycle;
                float _RiverGlintFrame;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(attributes);

                SetUpSpriteInstanceProperties();
                attributes.positionOS = UnityFlipSprite(attributes.positionOS, unity_SpriteProps.xy);
                o.positionCS = IslandBendObjectToHClip(attributes.positionOS, _IslandBendWeight);
                o.uv = attributes.uv;
                o.color = attributes.color * _Color * unity_SpriteColor;
                o.normalWS = TransformObjectToWorldDir(attributes.normal);
                o.tangentWS = TransformObjectToWorldDir(attributes.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * attributes.tangent.w;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                const half4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));

                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #if defined(DEBUG_DISPLAY)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"
            #endif
            #include "IslandBend.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma shader_feature_local _RIVER_WATER
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                float4  color           : COLOR;
                float2  uv              : TEXCOORD0;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                #endif
                #if defined(_RIVER_WATER)
                float2  riverWS     : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_MainTex);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                float _IslandBendWeight;
                half4 _RiverShallow;
                half4 _RiverDeep;
                float _RiverMatch;
                float _RiverGlintShare;
                float _RiverGlintCycle;
                float _RiverGlintFrame;
            CBUFFER_END

            Varyings UnlitVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(attributes);

                SetUpSpriteInstanceProperties();
                attributes.positionOS = UnityFlipSprite( attributes.positionOS, unity_SpriteProps.xy);
                o.positionCS = IslandBendObjectToHClip(attributes.positionOS, _IslandBendWeight);
                #if defined(DEBUG_DISPLAY)
                o.positionWS = IslandBendWorld(TransformObjectToWorld(attributes.positionOS), _IslandBendWeight);
                #endif
                o.uv = attributes.uv;
                #if defined(_RIVER_WATER)
                o.riverWS = TransformObjectToWorld(attributes.positionOS).xy;
                #endif
                o.color = attributes.color * _Color * unity_SpriteColor;
                return o;
            }

            #include "RiverWater.hlsl"

            float4 UnlitFragment(Varyings i) : SV_Target
            {
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                #if defined(_RIVER_WATER)
                texel = RiverWater(texel, i.uv, i.riverWS);
                #endif
                float4 mainTex = i.color * texel;

                #if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
                InitializeInputData(i.uv, inputData);
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, i.positionWS, i.positionCS, _MainTex);

                if(CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
                #endif

                return mainTex;
            }
            ENDHLSL
        }
    }
}
