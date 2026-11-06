Shader "Great Voyage/Ocean/Toon Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite carrier", 2D) = "white" {}
        _DeepColor ("Deep color", Color) = (.025,.20,.34,1)
        _MidColor ("Mid color", Color) = (.025,.30,.45,1)
        _BodyColor ("Body color", Color) = (.025,.38,.52,1)
        _WaveColor ("Wave color", Color) = (.035,.46,.58,1)
        _CrestColor ("Crest shoulder color", Color) = (.10,.57,.66,1)
        _FoamColor ("Crest foam color", Color) = (.79,.94,.91,1)
        _MajorFrequency ("Major wave frequency", Range(.2,2)) = 1.15
        _Curvature ("Major wave curvature", Range(0,2)) = .95
        _RippleScale ("Secondary ripple scale", Range(2,7)) = 3.6
        _RippleStrength ("Secondary ripple strength", Range(0,.15)) = .035
        _FoamCoverage ("Crest foam coverage", Range(0,1)) = .48
        _FoamWidth ("Crest foam width", Range(.01,.2)) = .085
        _OceanPhase ("Shared ambient phase", Vector) = (0,0,0,0)
        _OceanTravel ("Ambient travel (large XY, ripple ZW)", Vector) = (0,0,0,0)
        _ShapeEvolution ("Natural shape evolution", Range(0,1)) = .35
        _RelativeOceanOffset ("Navigation sampling offset", Vector) = (0,0,0,0)
        [Enum(Combined,0,LargeWaves,1,Ripples,2,CrestFoam,3)] _DebugLayer ("Review layer", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 oceanPosition : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "OceanSurfaceField.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _MidColor, _BodyColor, _WaveColor, _CrestColor, _FoamColor;
                float _MajorFrequency, _Curvature, _RippleScale, _RippleStrength, _FoamCoverage, _FoamWidth, _DebugLayer;
                float4 _OceanPhase, _OceanTravel, _RelativeOceanOffset;
                float _ShapeEvolution;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output = CommonUnlitVertex(input);
                output.oceanPosition = TransformObjectToWorld(input.positionOS).xy;
                output.color = input.color;
                return output;
            }

            half3 OceanPalette(float field)
            {
                float aa = max(fwidth(field)*.8, .002);
                half3 color = lerp(_DeepColor.rgb,_MidColor.rgb,smoothstep(.20-aa,.20+aa,field));
                color = lerp(color,_BodyColor.rgb,smoothstep(.43-aa,.43+aa,field));
                color = lerp(color,_WaveColor.rgb,smoothstep(.64-aa,.64+aa,field));
                return lerp(color,_CrestColor.rgb,smoothstep(.84-aa,.84+aa,field));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 samplingPosition = input.oceanPosition + _RelativeOceanOffset.xy;
                OceanSurfaceShape shape = EvaluateOceanShape(samplingPosition, _MajorFrequency, _Curvature, _RippleScale,
                    _OceanPhase, _OceanTravel, _ShapeEvolution);
                float field = saturate(shape.broadField + shape.ripple * _RippleStrength);
                float aa = max(fwidth(shape.crestDistance), .002);
                float width = _FoamWidth * lerp(.35,1.65,shape.crestEdge);
                float segments = smoothstep(1-_FoamCoverage-.035,1-_FoamCoverage+.035,shape.crestBreakup);
                float foam = (1-smoothstep(width-aa,width+aa,shape.crestDistance)) * shape.crestGate * segments;
                float shoulder = (1-smoothstep(width*1.2,width*3.4,shape.crestDistance)) * shape.crestGate * segments;
                float fringe = (1-smoothstep(width*1.2,width*4.0,shape.crestDistance)) * shape.crestGate * shape.crestFringe * segments;
                foam = saturate(foam + fringe*.7 + shape.minorCrest * .21);
                half3 color = OceanPalette(field) * (1+shape.surfaceVariation*.075);
                color = lerp(color,_CrestColor.rgb,shoulder*.38);
                color = lerp(color,_FoamColor.rgb,foam);
                if (_DebugLayer > 2.5) color = foam.xxx;
                else if (_DebugLayer > 1.5) color = (.5+shape.ripple*.5).xxx;
                else if (_DebugLayer > .5) color = OceanPalette(shape.broadField);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
