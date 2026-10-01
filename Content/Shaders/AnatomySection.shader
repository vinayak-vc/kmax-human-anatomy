Shader "Kmax Anatomy/Section"
{
    // An opaque, lit surface that can be cut away, for the scan. It is lit exactly as a Universal Lit material would be, so a
    // structure looks the same here as in its own topic, and it adds only cuts:
    //
    //   - a box (_KmaxBoxMin and _KmaxBoxMax) that trims the whole figure to the torso, and
    //   - the scan's tunnel: everything nearer the viewer than the scan plane, within a distance of its axis, is cut away.
    //     A very large radius makes the tunnel the whole plane, which cuts the figure in two.
    //
    // Where a surface has been cut away the inside of what is behind it shows, and it is drawn as a flat cut face in the colour
    // of whatever it is (_CapColor) and not as the dark hollow of a shell. A glowing ring marks the edge of the tunnel on the
    // surface, and a thin bright disc the plane at the tip of the pen.
    //
    // The cuts are the same for every material, so they are set once, globally, by the scan, and nothing is cut while the
    // lens radius is zero and the box is off, which is how every other topic leaves them.
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.3
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _CapColor ("Colour Of The Cut Face", Color) = (0.7, 0.2, 0.2, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half4 _EmissionColor;
            half4 _CapColor;
        CBUFFER_END

        // Set for every material at once by the scan.
        float4 _KmaxLens;       // xyz: the point the scan plane passes through; w: the lens radius, or 0 for none
        float4 _KmaxLensAxis;   // xyz: the unit vector into the screen; w: how wide the glowing ring is
        float4 _KmaxLensColor;  // rgb: the colour of the glow; w: how thick the scan plane's disc is
        float4 _KmaxBoxMin;     // xyz: the lower corner of the box the figure is cut to; w: 1 to cut, 0 not to
        float4 _KmaxBoxMax;

        // Where a point lies against the cuts, and whether they cut it away. "Along" is how far the point is from the scan
        // plane, negative on the viewer's side, and "lateral" how far it is from the scan's axis.
        bool IsCutAway(float3 positionWS, out float along, out float lateral)
        {
            float3 relative = positionWS - _KmaxLens.xyz;
            along = dot(relative, _KmaxLensAxis.xyz);
            lateral = length(relative - along * _KmaxLensAxis.xyz);

            if (_KmaxBoxMin.w > 0.5)
            {
                if (any(positionWS < _KmaxBoxMin.xyz) || any(positionWS > _KmaxBoxMax.xyz))
                {
                    return true;
                }
            }

            return _KmaxLens.w > 0.0 && along < 0.0 && lateral < _KmaxLens.w;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float along;
                float lateral;
                if (IsCutAway(input.positionWS, along, lateral))
                {
                    discard;
                }

                bool lensOn = _KmaxLens.w > 0.0;
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // The back of a surface, seen through a cut, is the inside of something: a flat cut face.
                if (!IS_FRONT_VFACE(isFront, true, false))
                {
                    half3 inward = normalize(input.normalWS);
                    half facing = saturate(dot(-inward, viewDirectionWS));
                    half3 cut = _CapColor.rgb * input.color.rgb * (0.62 + 0.38 * facing);
                    if (lensOn)
                    {
                        cut += _KmaxLensColor.rgb * 0.12;
                    }

                    return half4(MixFog(cut, input.fogFactor), 1.0);
                }

                // The glow that marks the scan: a ring where the tunnel meets a surface, and a thin disc at the scan plane.
                half3 glow = half3(0, 0, 0);
                if (lensOn)
                {
                    float rim = _KmaxLensAxis.w;
                    half ring = (along < 0.0 && lateral >= _KmaxLens.w && lateral < _KmaxLens.w + rim)
                        ? 1.0 - (lateral - _KmaxLens.w) / rim : 0.0;
                    float thickness = _KmaxLensColor.w;
                    half disc = (abs(along) < thickness && lateral < _KmaxLens.w) ? 1.0 - abs(along) / thickness : 0.0;
                    glow = _KmaxLensColor.rgb * (ring * 1.6 + disc * 1.2);
                }

                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = viewDirectionWS;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = texel.rgb * _BaseColor.rgb * input.color.rgb;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = _EmissionColor.rgb + glow;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 colour = UniversalFragmentPBR(inputData, surfaceData);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1.0;
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                float along;
                float lateral;
                if (IsCutAway(input.positionWS, along, lateral))
                {
                    discard;
                }

                return 0;
            }
            ENDHLSL
        }
    }
}
