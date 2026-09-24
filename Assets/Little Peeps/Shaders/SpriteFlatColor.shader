// A sprite drawn as a flat-coloured silhouette: every pixel takes _Color, only the sprite's alpha is kept.
// A tint cannot do this — it multiplies the art's colours, so it can darken a sprite to black (SpriteShadow)
// but never lift it to white. Unlit; works on SpriteRenderers and TilemapRenderers alike.
Shader "Little Peeps/Sprite Flat Color"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4  color      : COLOR;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
        CBUFFER_END

        Varyings FlatVertex(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.color = v.color;
            o.uv = v.uv;
            return o;
        }

        half4 FlatFragment(Varyings i) : SV_Target
        {
            half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
            return half4(_Color.rgb, _Color.a * alpha);
        }
        ENDHLSL

        // The 2D Renderer draws this pass...
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex FlatVertex
            #pragma fragment FlatFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // ...a Universal (forward) Renderer this one.
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex FlatVertex
            #pragma fragment FlatFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
