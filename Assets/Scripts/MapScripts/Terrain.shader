Shader "Custom/TerrainURP"
{
    Properties
    {
        // Layer data is pushed from C# via SetColorArray / SetFloatArray / SetTexture.
        // Road appearance is authored here; the mask itself rides in vertex colour red.
        _RoadColour ("Road Colour", Color) = (0.32, 0.30, 0.28, 1)
        _RoadColourStrength ("Road Colour Strength", Range(0, 1)) = 0.45
        _RoadTextureIndex ("Road Texture Layer", Range(0, 7)) = 2
        _RoadTextureScale ("Road Texture Scale", Float) = 4
        _RoadEdgeStart ("Road Edge Start", Range(0, 1)) = 0.25
        _RoadEdgeEnd ("Road Edge End", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma require 2darray

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define maxLayerCount 8
            static const float epsilon = 1E-4;

            
            int    layerCount;
            float4 baseColours[maxLayerCount];
            float  baseStartHeights[maxLayerCount];
            float  baseBlends[maxLayerCount];
            float  baseColourStrength[maxLayerCount];
            float  baseTextureScales[maxLayerCount];

            float minHeight;
            float maxHeight;

            float4 _RoadColour;
            float  _RoadColourStrength;
            float  _RoadTextureIndex;
            float  _RoadTextureScale;
            float  _RoadEdgeStart;
            float  _RoadEdgeEnd;

            TEXTURE2D_ARRAY(baseTextures);
            SAMPLER(sampler_baseTextures);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
                float  surfaceMask : TEXCOORD3;
            };

            float inverseLerp(float a, float b, float value)
            {
                return saturate((value - a) / (b - a));
            }

            float3 triplanar(float3 worldPos, float scale, float3 blendAxes, int textureIndex)
            {
                float3 scaledWorldPos = worldPos / scale;

                float3 xProjection = SAMPLE_TEXTURE2D_ARRAY(
                    baseTextures, sampler_baseTextures,
                    float2(scaledWorldPos.y, scaledWorldPos.z), textureIndex).rgb * blendAxes.x;

                float3 yProjection = SAMPLE_TEXTURE2D_ARRAY(
                    baseTextures, sampler_baseTextures,
                    float2(scaledWorldPos.x, scaledWorldPos.z), textureIndex).rgb * blendAxes.y;

                float3 zProjection = SAMPLE_TEXTURE2D_ARRAY(
                    baseTextures, sampler_baseTextures,
                    float2(scaledWorldPos.x, scaledWorldPos.y), textureIndex).rgb * blendAxes.z;

                return xProjection + yProjection + zProjection;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                OUT.surfaceMask = IN.color.r;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                float heightPercent = inverseLerp(minHeight, maxHeight, IN.positionWS.y);

                float3 blendAxes = abs(normalWS);
                blendAxes /= max(blendAxes.x + blendAxes.y + blendAxes.z, epsilon);

                float3 albedo = 0;

                for (int i = 0; i < layerCount; i++)
                {
                    float drawStrength = inverseLerp(
                        -baseBlends[i] / 2 - epsilon,
                         baseBlends[i] / 2,
                         heightPercent - baseStartHeights[i]);

                    float3 baseColour    = baseColours[i].rgb * baseColourStrength[i];
                    float3 textureColour = triplanar(IN.positionWS, baseTextureScales[i], blendAxes, i)
                                           * (1 - baseColourStrength[i]);

                    albedo = albedo * (1 - drawStrength) + (baseColour + textureColour) * drawStrength;
                }

                // Roads and building pads. The mask ramps across the carved shoulder, so remapping
                // it lets the painted surface be narrower or wider than the flattened geometry.
                float roadMask = smoothstep(_RoadEdgeStart, max(_RoadEdgeEnd, _RoadEdgeStart + 1e-4), IN.surfaceMask);

                if (roadMask > 0)
                {
                    float3 roadTexture = triplanar(IN.positionWS, _RoadTextureScale, blendAxes, (int)_RoadTextureIndex);
                    float3 roadAlbedo = lerp(roadTexture, _RoadColour.rgb, _RoadColourStrength);
                    albedo = lerp(albedo, roadAlbedo, roadMask);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS        = IN.positionWS;
                inputData.normalWS          = normalWS;
                inputData.viewDirectionWS   = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord       = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord          = IN.fogCoord;
                inputData.bakedGI           = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask        = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo;
                surfaceData.metallic   = 0;
                surfaceData.specular   = 0;
                surfaceData.smoothness = 0;
                surfaceData.occlusion  = 1;
                surfaceData.alpha      = 1;
                surfaceData.normalTS   = float3(0, 0, 1);
                surfaceData.emission   = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

       
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
