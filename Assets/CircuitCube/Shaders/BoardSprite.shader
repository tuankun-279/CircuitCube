// One textured quad per tile layer, drawn with GPU instancing: every tile of a layer is a single draw call.
// The per-tile tint comes from the instanced _Color, and _UVRect picks the tile's image out of the theme's board
// atlas, so one draw call can show different images. Blend and depth write are material settings, so the same
// shader draws the opaque (or alpha-clipped) tile faces and the transparent wires, lamps and glows.
Shader "CircuitCube/BoardSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Toggle] _ZWrite ("Z Write", Float) = 0
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Cutoff;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)   // xy scale, zw offset of the image in an atlas
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                o.uv = input.uv * rect.xy + rect.zw;
                o.color = (half4)UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);   // the triangle face cuts its corners like the old alpha-clipped material
                return c;
            }
            ENDHLSL
        }
    }
}
