Shader "GreatVoyage/OceanSurface2D"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.022, 0.13, 0.24, 1)
        _MidWaterColor ("Mid Water", Color) = (0.035, 0.32, 0.40, 1)
        _CrestColor ("Wave Crest", Color) = (0.26, 0.66, 0.68, 1)
        _FoamColor ("Foam", Color) = (0.80, 0.96, 0.91, 1)
        _WaveFrequency ("Wave Frequency", Float) = 0.18
        [HideInInspector] _OceanTime ("Ocean Time", Float) = 0
        _WaveSpeed ("Wave Animation Speed", Range(0, 3)) = 0.9
        _DebugMode ("Field Debug (0=Surface, 1=Base, 2=Delta, 3=Final, 4=Foam)", Range(0,4)) = 0
        [Enum(Cellular Water,0,Open Sea,1)] _SurfaceStyle ("Reference Style", Float) = 0
        [HideInInspector] _FieldOrigin ("Field Origin", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FieldWorldSize ("Field World Size", Vector) = (400, 400, 0, 0)
        _OceanBaseFlowMap ("Base FlowMap", 2D) = "black" {}
        _OceanShipDeltaMap ("Ship DeltaMap", 2D) = "black" {}
        _OceanFinalFlowMap ("Final FlowMap", 2D) = "black" {}
        _OceanWaveContributionMap ("Future Wave Contributions", 2D) = "black" {}
        _OceanFoamMap ("Foam History", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "OceanSurface2D"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                float4 positionCS : SV_POSITION;
                float2 worldXY : TEXCOORD0;
            };

            TEXTURE2D(_OceanBaseFlowMap);
            SAMPLER(sampler_OceanBaseFlowMap);
            TEXTURE2D(_OceanShipDeltaMap);
            SAMPLER(sampler_OceanShipDeltaMap);
            TEXTURE2D(_OceanWaveContributionMap);
            SAMPLER(sampler_OceanWaveContributionMap);
            TEXTURE2D(_OceanFinalFlowMap);
            SAMPLER(sampler_OceanFinalFlowMap);
            TEXTURE2D(_OceanFoamMap);
            SAMPLER(sampler_OceanFoamMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _MidWaterColor;
                float4 _CrestColor;
                float4 _FoamColor;
                float _WaveFrequency;
                float _WaveSpeed;
                float _OceanTime;
                float _DebugMode;
                float _SurfaceStyle;
                float4x4 _OceanWorldToField;
                float4 _FieldOrigin;
                float4 _FieldWorldSize;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.worldXY = mul(_OceanWorldToField, float4(world, 1)).xy;
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            float WavePattern(float2 position, float time)
            {
                float2 p = position * _WaveFrequency;
                float along = dot(p, float2(-0.31, 0.95));
                float across = dot(p, float2(0.95, 0.31));
                float bend = 0.65 * sin(along * 1.7 + across * 0.51 + time * 0.23);
                float swell = sin(across * 1.8 + bend - time);
                float crossSwell = sin(dot(p, float2(-0.63, 1.43)) - time * 0.61);
                float ripple = sin(across * 4.4 + bend * 1.7 - time * 1.46);
                return swell * 0.55 + crossSwell * 0.31 + ripple * 0.14;
            }

            float FlowWaves(float2 position, float2 flow, float time)
            {
                // Keep displacement bounded, including at opposing flows and long session times.
                float2 travel = flow / max(1.0, length(flow)) * 4.0;
                float phaseA = frac(time * 0.25);
                float phaseB = frac(phaseA + 0.5);
                // A sample has zero weight at its wrap. At zero flow both samples coincide.
                float weightA = 1.0 - abs(2.0 * phaseA - 1.0);
                return lerp(WavePattern(position - travel * (phaseB - 0.5), time),
                            WavePattern(position - travel * (phaseA - 0.5), time), weightA);
            }

            float2 CellSeed(float2 cell)
            {
                return frac(sin(float2(dot(cell, float2(127.1, 311.7)),
                    dot(cell, float2(269.5, 183.3)))) * 43758.5453);
            }

            float3 CellularWater(float2 position, float2 flow, float time)
            {
                float2 p = position * (_WaveFrequency * 1.8);
                p += flow / max(1.0, length(flow)) * sin(time * 0.73) * 0.32;
                p += float2(sin(p.y * 2.4 + time + sin(p.x)),
                            sin(p.x * 2.1 - time * 0.7 + sin(p.y))) * 0.29;
                float2 origin = floor(p);
                float2 local = frac(p);
                float nearest = 9;
                float nextNearest = 9;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 seed = CellSeed(origin + offset);
                    float2 delta = offset + 0.5 + (seed - 0.5) * 0.66 - local;
                    delta += sin(time * 0.56 + seed * 6.28) * 0.12;
                    float distanceSq = dot(delta, delta);
                    nextNearest = min(nextNearest, max(nearest, distanceSq));
                    nearest = min(nearest, distanceSq);
                }
                float edge = nextNearest - nearest;
                float width = 0.055 + 0.035 * sin(p.x * 2.3 + p.y * 1.9 - time);
                float edgeMask = 1 - smoothstep(width, width + max(fwidth(edge), 0.02), edge);
                float halo = 1 - smoothstep(0.08, 0.32, edge);
                float shade = 0.5 + 0.5 * sin(p.x * 0.53 - p.y * 0.38 + time * 0.47);
                float3 water = lerp(float3(0.012, 0.28, 0.44), float3(0.025, 0.56, 0.66),
                    saturate(shade * 0.58 + halo * 0.26));
                return lerp(water, float3(0.51, 0.94, 0.94), edgeMask * (0.67 + shade * 0.30));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = (input.worldXY - _FieldOrigin.xy) / max(_FieldWorldSize.xy, 0.00001);
                float2 baseFlow = SAMPLE_TEXTURE2D(_OceanBaseFlowMap, sampler_OceanBaseFlowMap, saturate(uv)).xy;
                float2 shipDelta = SAMPLE_TEXTURE2D(_OceanShipDeltaMap, sampler_OceanShipDeltaMap, saturate(uv)).xy;
                float2 flow = SAMPLE_TEXTURE2D(_OceanFinalFlowMap, sampler_OceanFinalFlowMap, saturate(uv)).xy;
                float foamDensity = SAMPLE_TEXTURE2D(_OceanFoamMap, sampler_OceanFoamMap, saturate(uv)).r;
                if (_DebugMode > 0.5 && _DebugMode < 3.5)
                {
                    float2 debugVector = _DebugMode < 1.5 ? baseFlow : (_DebugMode < 2.5 ? shipDelta : flow);
                    return half4(saturate(float3(debugVector.x * 0.2 + 0.5, debugVector.y * 0.2 + 0.5, length(debugVector) * 0.2)), 1.0);
                }
                if (_DebugMode >= 3.5)
                    return half4(float3(foamDensity, foamDensity, foamDensity), 1.0);

                float time = _OceanTime;
                float waveContribution = SAMPLE_TEXTURE2D(_OceanWaveContributionMap, sampler_OceanWaveContributionMap, saturate(uv)).r;
                float wave = FlowWaves(input.worldXY, flow, time * _WaveSpeed) + waveContribution;
                float body = smoothstep(-0.65, 0.58, wave);
                float crest = smoothstep(0.54, 0.77, wave);
                float glint = smoothstep(0.79, 0.94, wave);
                float irregular = 0.75 + 0.25 * sin(dot(input.worldXY, float2(0.83, 1.37)) + time * 0.35);
                float foam = saturate(foamDensity * irregular);
                float3 water;
                if (_SurfaceStyle < 0.5)
                    water = CellularWater(input.worldXY, flow, time * _WaveSpeed);
                else
                {
                    float2 p = input.worldXY;
                    float waveTime = time * _WaveSpeed;
                    float baseWave = wave - waveContribution;
                    float dx = FlowWaves(p + float2(0.15, 0), flow, waveTime) - baseWave;
                    float dy = FlowWaves(p + float2(0, 0.15), flow, waveTime) - baseWave;
                    float3 normal = normalize(float3(-dx * 13, -dy * 13, 1));
                    float specular = pow(saturate(dot(normal, normalize(float3(-0.38, 0.3, 1)))), 28);
                    float brokenCrest = smoothstep(0.65, 0.93, wave) *
                        smoothstep(0.38, 0.8, sin(p.x * 1.32 + sin(p.y * 1.13) + time * 0.4));
                    water = lerp(_DeepColor.rgb, _MidWaterColor.rgb, body);
                    water = lerp(water, _CrestColor.rgb, crest * 0.64 + glint * 0.36);
                    water = lerp(water, float3(0.86, 0.96, 1), saturate(specular * 0.48 + brokenCrest * 0.65));
                }
                float3 color = lerp(water, _FoamColor.rgb, foam);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
