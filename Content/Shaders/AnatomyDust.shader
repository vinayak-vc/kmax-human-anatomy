Shader "Kmax Anatomy/Dust"
{
    // Soft round motes for the launcher's backdrop. Additive and unlit: a soft dot, tinted by the material and again by the particle's
    // own colour, so each mote can be its own brightness. Both faces are drawn and nothing writes depth, so motes never hide the model.
    Properties
    {
        [MainTexture] _BaseMap ("Soft Dot", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (0.62, 0.86, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Dust"

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 dot = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                return dot * _BaseColor * input.color;
            }
            ENDHLSL
        }
    }
}
