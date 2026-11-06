// The tile faces of a whole board as one mesh: one draw call whatever the shape. Each tile is a fan of triangles
// whose UVs point into the theme's board atlas (the face image of the tile's outline). Faces turned towards the light
// are brighter, as the old per-tile tint did, worked out from the mesh normal so nothing is updated per frame.
Shader "CircuitCube/BoardFace"
{
    Properties
    {
        _MainTex ("Board atlas", 2D) = "white" {}
        _ShadeDark ("Shade away from the light", Color) = (0.62, 0.64, 0.72, 1)
        _ShadeLight ("Shade towards the light", Color) = (1, 1, 1, 1)
        _LightDir ("Light direction (world)", Vector) = (-0.45, 0.75, -0.5, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ShadeDark, _ShadeLight;
                float4 _LightDir;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 shade : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                float3 n = normalize(TransformObjectToWorldNormal(input.normalOS));
                float t = saturate(dot(n, -normalize(_LightDir.xyz)) * 0.5 + 0.5);
                o.shade = lerp(_ShadeDark, _ShadeLight, (half)t);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.shade;
                c.a = 1;
                return c;
            }
            ENDHLSL
        }
    }
}
