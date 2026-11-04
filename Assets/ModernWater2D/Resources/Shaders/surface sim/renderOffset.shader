Shader "Water2D/Simulations/offset"
{
    Properties
    {
        _HistoryTransform ("History UV scale and offset", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION; 
                float2 uv : TEXCOORD0; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0; 
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            uniform sampler2D _tex;
            uniform float4 _HistoryTransform;

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * _HistoryTransform.xy + _HistoryTransform.zw;
                // Newly exposed water has no history. Clamping or wrapping would drag old waves
                // into it when zooming out or panning beyond the previous simulation rectangle.
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
                return tex2D(_tex, uv);
            }

            ENDCG
        }
    }
}
