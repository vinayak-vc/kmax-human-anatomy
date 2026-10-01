Shader "Kmax Anatomy/Glow"
{
    // Unlit and see-through. With "Fade Towards The Middle" on it draws only the silhouette of a surface, which is
    // what makes a receded structure read as glass; with it off it is a flat tinted line, used for leader lines.
    // "Glow Kept In The Middle" lets a surface keep some of its glow away from the edge, so the body map's layers,
    // which are drawn as glow alone, still read as solid forms. The vertex colour multiplies the colour, and its alpha
    // carries the body map's crop fade.
    Properties
    {
        [MainColor] _BaseColor ("Colour", Color) = (0.6, 0.85, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _Floor ("Glow Kept In The Middle", Range(0.0, 1.0)) = 0.0
        [Toggle] _Fresnel ("Fade Towards The Middle", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Glow"

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _Floor;
                half _Fresnel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS = half3(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = half3(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = SafeNormalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                half edge = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half4 colour = _BaseColor * input.color;
                colour.a *= _Fresnel > 0.5h ? lerp(_Floor, 1.0h, edge) : 1.0h;
                return colour;
            }
            ENDHLSL
        }
    }
}