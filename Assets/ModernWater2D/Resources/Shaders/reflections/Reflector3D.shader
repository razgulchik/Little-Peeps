Shader "Custom/URP/Reflector3D"
{
    Properties
    {
       
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "AdditiveReflect"

            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float y01 : TEXCOORD0;
            };

            float4 _yFalloff3dColor;
            float _RForgColor;
            float _RFalpha;

            float _yFalloffStart;
            float _yFalloffStrength;

            float _ObjectYMin;
            float _ObjectYMax;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);

                // Normalize Y within object bounds
                o.y01 = saturate((v.positionOS.y - _ObjectYMin) / (_ObjectYMax - _ObjectYMin));

                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // Apply Y-based falloff
                float falloff = saturate((i.y01 - _yFalloffStart) / (1.0 - _yFalloffStart));
                falloff = pow(falloff, _yFalloffStrength);

                // Final alpha
                float alpha = falloff ;

                // Color adder independent of Y
                float3 color = _yFalloff3dColor.rgb;

                return float4(color,  alpha);
            }

            ENDHLSL
        }
    }
}