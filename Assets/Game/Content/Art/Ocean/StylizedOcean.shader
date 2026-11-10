Shader "GreatVoyage/StylizedOcean"
{
    Properties
    {
        _DeepColor ("Water shadow", Color) = (0.012,0.16,0.25,1)
        _MidWaterColor ("Water body", Color) = (0.025,0.43,0.50,1)
        _CrestColor ("Surface reflection", Color) = (0.42,0.82,0.84,1)
        _FoamColor ("Foam", Color) = (0.83,0.97,0.95,1)
        _WaveFrequency ("Pattern scale", Range(0.05,1)) = 0.3
        _WaveSpeed ("Motion speed", Range(0,3)) = 0.5
        _NormalStrength ("Surface relief", Range(0,3)) = 0.8
        _CellHighlights ("Connected light patterns", Range(0,1)) = 0.7
        _DebugMode ("Field debug", Range(0,4)) = 0
        [HideInInspector] _OceanTime ("Time", Float) = 0
        [HideInInspector] _FieldOrigin ("Field origin", Vector) = (0,0,0,0)
        [HideInInspector] _FieldWorldSize ("Field size", Vector) = (64,36,0,0)
        _OceanBaseFlowMap ("Base flow", 2D) = "black" {}
        _OceanShipDeltaMap ("Ship delta", 2D) = "black" {}
        _OceanFinalFlowMap ("Final flow", 2D) = "black" {}
        _OceanFoamMap ("Foam history", 2D) = "black" {}
        [HideInInspector] _OceanLightOffsets ("Light offsets", 2D) = "black" {}
        [HideInInspector] _OceanWaveOffsets ("Wave offsets", 2D) = "black" {}
        _OceanWaveContributionMap ("Wave contribution", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ OCEAN_REFERENCE_SLOPE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_OceanBaseFlowMap); SAMPLER(sampler_OceanBaseFlowMap);
            TEXTURE2D(_OceanShipDeltaMap); SAMPLER(sampler_OceanShipDeltaMap);
            TEXTURE2D(_OceanFinalFlowMap); SAMPLER(sampler_OceanFinalFlowMap);
            TEXTURE2D(_OceanFoamMap); SAMPLER(sampler_OceanFoamMap);
            TEXTURE2D(_OceanWaveContributionMap); SAMPLER(sampler_OceanWaveContributionMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _DeepColor, _MidWaterColor, _CrestColor, _FoamColor;
            float4 _FieldOrigin, _FieldWorldSize;
            float4x4 _OceanWorldToField;
            float _WaveFrequency, _WaveSpeed, _NormalStrength, _CellHighlights;
            float _OceanTime, _DebugMode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 ocean : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.ocean = mul(_OceanWorldToField,float4(world,1)).xy;
                return output;
            }
            float2 Hash(float2 p)
            {
                p = frac(p * float2(0.1031,0.11369));
                p += dot(p,p.yx + 19.19);
                return frac(float2(p.x*p.y,(p.x+p.y)*p.x));
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(Hash(cell).x,Hash(cell+float2(1,0)).x,f.x),
                    lerp(Hash(cell+float2(0,1)).x,Hash(cell+1).x,f.x),f.y);
            }
            float2 Warp(float2 p,float t)
            {
                return p + float2(Noise(p*.8+float2(t*.13,0)),
                    Noise(p*.8+float2(7,-t*.11)))*1.1;
            }
            float Height(float2 p,float t)
            {
                p=Warp(p,t);
                return Noise(p+float2(t*.18,-t*.09))*.58
                    + Noise(p*2.13+float2(-t*.14,4))*.27
                    + Noise(p*4.37+float2(2,t*.17))*.15;
            }
            float CellEdges(float2 p,float t)
            {
                p=Warp(p,t);
                float2 cell=floor(p), f=frac(p);
                float first=8, second=8;
                [unroll] for(int y=-1;y<=1;y++)
                [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y);
                    float2 seed=Hash(cell+offset);
                    float2 site=.5+.32*sin(seed*6.283+t*.19);
                    float d=length(offset+site-f);
                    second=min(second,max(first,d));
                    first=min(first,d);
                }
                float edge=second-first;
                float width=lerp(.018,.085,Noise(p*2.7));
                return 1-smoothstep(width,width+max(fwidth(edge)*1.5,.03),edge);
            }
            float2 Flow(float2 p)
            {
                float2 uv=(p-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001);
                return SAMPLE_TEXTURE2D(_OceanFinalFlowMap,sampler_OceanFinalFlowMap,saturate(uv)).xy;
            }
            TEXTURE2D(_OceanLightOffsets);
            SAMPLER(sampler_OceanLightOffsets);
            TEXTURE2D(_OceanWaveOffsets);
            SAMPLER(sampler_OceanWaveOffsets);
            float _OceanWaveBlend;

            float Patch(float2 p,float2 center,float t)
            {
                return Height(p-center+Hash(center)*31,t);
            }

            float4 SurfaceCoordinates(float2 world)
            {
                float2 uv=saturate((world-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001));
                float4 offsets=SAMPLE_TEXTURE2D_LOD(_OceanWaveOffsets,sampler_OceanWaveOffsets,uv,0);
                return (world.xyxy+offsets)*_WaveFrequency;
            }
            float2 WaveContribution(float2 world)
            {
                float2 uv=saturate((world-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001));
                return SAMPLE_TEXTURE2D_LOD(_OceanWaveContributionMap,sampler_OceanWaveContributionMap,uv,0).rg;
            }

            float PatternHeight(float2 p,float t)
            {
                float2 grid=floor(p/4), f=frac(p/4);
                f=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(Patch(p,grid*4,t),Patch(p,(grid+float2(1,0))*4,t),f.x),
                    lerp(Patch(p,(grid+float2(0,1))*4,t),Patch(p,(grid+1)*4,t),f.x),f.y);
            }

            // Returns value and its derivatives in noise coordinates.
            float3 NoiseGradient(float2 p)
            {
                float2 c=floor(p), u=frac(p);
                float2 f=u*u*u*(u*(u*6-15)+10);
                float2 df=30*u*u*(u-1)*(u-1);
                float a=Hash(c).x, b=Hash(c+float2(1,0)).x;
                float d=Hash(c+float2(0,1)).x, e=Hash(c+1).x;
                float lo=lerp(a,b,f.x), hi=lerp(d,e,f.x);
                return float3(lerp(lo,hi,f.y), lerp(b-a,e-d,f.y)*df.x, (hi-lo)*df.y);
            }

            float3 PatchGradient(float2 p,float2 center,float t)
            {
                p=p-center+Hash(center)*31;
                float3 x=NoiseGradient(p*.8+float2(t*.13,0));
                float3 y=NoiseGradient(p*.8+float2(7,-t*.11));
                float2 q=p+float2(x.x,y.x)*1.1;
                float3 a=NoiseGradient(q+float2(t*.18,-t*.09));
                float3 b=NoiseGradient(q*2.13+float2(-t*.14,4));
                float3 c=NoiseGradient(q*4.37+float2(2,t*.17));
                float2 g=a.yz*.58+b.yz*(.27*2.13)+c.yz*(.15*4.37);
                return float3(a.x*.58+b.x*.27+c.x*.15,
                    g+.88*(g.x*x.yz+g.y*y.yz));
            }

            float3 PatternGradient(float2 p,float t)
            {
                float2 c=floor(p/4)*4, u=frac(p/4);
                float2 f=u*u*u*(u*(u*6-15)+10);
                float2 df=7.5*u*u*(u-1)*(u-1);
                float3 a=PatchGradient(p,c,t), b=PatchGradient(p,c+float2(4,0),t);
                float3 d=PatchGradient(p,c+float2(0,4),t), e=PatchGradient(p,c+4,t);
                float3 lo=lerp(a,b,f.x), hi=lerp(d,e,f.x);
                float3 result=lerp(lo,hi,f.y);
                result.yz+=float2(lerp(b.x-a.x,e.x-d.x,f.y)*df.x,(hi.x-lo.x)*df.y);
                return result;
            }
            struct WaveSurface
            {
                float height;
                float2 slope;
                float3 normal;
                float crest;
                float4 coordinates;
            };

            WaveSurface EvaluateSurface(float2 world,float t,float density)
            {
                WaveSurface surface;
                const float e=.04;
                surface.coordinates=SurfaceCoordinates(world);
                float2 contribution=WaveContribution(world);
                float2 dx=float2(e,0), dy=float2(0,e);
                float4 qLeft=SurfaceCoordinates(world-dx), qRight=SurfaceCoordinates(world+dx);
                float4 qDown=SurfaceCoordinates(world-dy), qUp=SurfaceCoordinates(world+dy);
                float leftR=WaveContribution(world-dx).r, rightR=WaveContribution(world+dx).r;
                float downR=WaveContribution(world-dy).r, upR=WaveContribution(world+dy).r;
                #if defined(OCEAN_REFERENCE_SLOPE)
                surface.height=lerp(PatternHeight(surface.coordinates.xy,t),
                    PatternHeight(surface.coordinates.zw,t),_OceanWaveBlend)+contribution.r;
                #else
                float3 a=PatternGradient(surface.coordinates.xy,t);
                float3 b=PatternGradient(surface.coordinates.zw,t);
                surface.height=lerp(a.x,b.x,_OceanWaveBlend)+contribution.r;
                float4 gx=qRight-qLeft, gy=qUp-qDown;
                surface.slope=(lerp(float2(dot(a.yz,gx.xy),dot(a.yz,gy.xy)),
                    float2(dot(b.yz,gx.zw),dot(b.yz,gy.zw)),_OceanWaveBlend)
                    +float2(rightR-leftR,upR-downR))/(2*e);
                #endif
                surface.crest=0;
                #if defined(OCEAN_REFERENCE_SLOPE)
                {
                #else
                [branch] if (density > .12)
                {
                #endif
                    float left=lerp(PatternHeight(qLeft.xy,t),PatternHeight(qLeft.zw,t),_OceanWaveBlend)+leftR;
                    float right=lerp(PatternHeight(qRight.xy,t),PatternHeight(qRight.zw,t),_OceanWaveBlend)+rightR;
                    float down=lerp(PatternHeight(qDown.xy,t),PatternHeight(qDown.zw,t),_OceanWaveBlend)+downR;
                    float up=lerp(PatternHeight(qUp.xy,t),PatternHeight(qUp.zw,t),_OceanWaveBlend)+upR;
                    #if defined(OCEAN_REFERENCE_SLOPE)
                    surface.slope=float2(right-left,up-down)/(2*e);
                    #endif
                    float curvature=(4*surface.height-left-right-down-up)/(e*e);
                    surface.crest=saturate(smoothstep(.55,.8,surface.height)*smoothstep(0,.5,curvature)
                        +contribution.g);
                }
                surface.normal=normalize(float3(-surface.slope,1));
                                return surface;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv=saturate((input.ocean-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001));

                float density=SAMPLE_TEXTURE2D(_OceanFoamMap,sampler_OceanFoamMap,uv).r;
                if(_DebugMode>.5)
                {
                    if(_DebugMode>=3.5) return half4(density.xxx,1);
                    float2 v;
                    if(_DebugMode<1.5) v=SAMPLE_TEXTURE2D(_OceanBaseFlowMap,sampler_OceanBaseFlowMap,uv).xy;
                    else if(_DebugMode<2.5) v=SAMPLE_TEXTURE2D(_OceanShipDeltaMap,sampler_OceanShipDeltaMap,uv).xy;
                    else v=Flow(input.ocean);
                    return half4(saturate(float3(v*.2+.5,length(v)*.2)),1);
                }
                float t=_OceanTime*_WaveSpeed;
                float2 p=input.ocean*_WaveFrequency;
                WaveSurface surface=EvaluateSurface(input.ocean,t,density);
                float3 normal=normalize(float3(-surface.slope*_NormalStrength/max(_WaveFrequency,.001),1));
                float light=saturate(dot(normal,normalize(float3(-.4,.5,1))));
                float reflection=smoothstep(.87,.99,light);
                float glint=pow(light,64);
                float depthTone=saturate(surface.height*.6+Noise(p*.3)*.4);
                float3 color=lerp(_DeepColor.rgb,_MidWaterColor.rgb,depthTone);
                color=lerp(color,_CrestColor.rgb,reflection*.5);
                [branch] if (_CellHighlights > 0)
                {
                float2 lightOffset=SAMPLE_TEXTURE2D(_OceanLightOffsets,sampler_OceanLightOffsets,uv).xy;
                float2 lightPosition=(input.ocean+lightOffset)*_WaveFrequency;
                float connected=CellEdges(lightPosition*.8,t);
                color=lerp(color,_CrestColor.rgb,connected*_CellHighlights);
                }
                color+=glint*.12;
                float foam=0;
                [branch] if (density > .12)
                {
                    float detail=smoothstep(.2,.65,lerp(Noise(Warp(surface.coordinates.xy*3,t)),
                        Noise(Warp(surface.coordinates.zw*3,t)),_OceanWaveBlend));
                    foam=smoothstep(.12,.8,density)*lerp(.35,1,max(surface.crest,detail));
                }
                return half4(lerp(color,_FoamColor.rgb,foam),1);
            }
            ENDHLSL
        }
    }
}
