// A one-pixel ring around a UI sprite's silhouette, in the Image's colour: every transparent texel that
// touches an opaque one (sides or corners) is coloured, the rest stays empty. Put it on an Image showing the
// same sprite as the icon, sized one pixel larger on every side (13×13 icon → 15×15 ring) — the quad is read
// as the sprite grown by one texel all round, so the ring can stand outside an icon drawn edge to edge.
// Expects the sprite to fill its texture (single-sprite import, not atlased) — true for the resource icons.
// Otherwise UI/Default: stencil for Mask, clip rect for RectMask2D.
Shader "Little Peeps/UI Icon Ring"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Alpha of the texel centred at c (in texels); nothing outside the sprite. Mip 0 always: the
            // coordinate jumps from texel to texel, and its derivatives would pick a blurred mip at every step.
            float AlphaAt(float2 c, float2 size)
            {
                if (c.x < 0 || c.y < 0 || c.x > size.x || c.y > size.y) return 0;
                return tex2Dlod(_MainTex, float4(c / size, 0, 0)).a;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 c = floor(i.texcoord * (size + 2) - 1) + 0.5;

                float near = 0;
                if (AlphaAt(c, size) < 0.5)
                {
                    for (int y = -1; y <= 1; y++)
                        for (int x = -1; x <= 1; x++)
                            near = max(near, AlphaAt(c + float2(x, y), size));
                }

                fixed4 color = i.color;
                color.a *= near >= 0.5 ? 1 : 0;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
