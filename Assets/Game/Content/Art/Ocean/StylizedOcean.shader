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
        _CellHighlights ("Light areas", Range(0,1)) = 0.7
        _DebugMode ("Field debug", Range(0,6)) = 0
        [HideInInspector] _OceanTime ("Time", Float) = 0
        [HideInInspector] _FieldOrigin ("Field origin", Vector) = (0,0,0,0)
        [HideInInspector] _FieldWorldSize ("Field size", Vector) = (64,36,0,0)
        _OceanBaseFlowMap ("Base flow", 2D) = "black" {}
        _OceanFinalFlowMap ("Final flow", 2D) = "black" {}
        _OceanFoamMap ("Foam history", 2D) = "black" {}
        _LightWidth ("Light area spread", Range(0.005,0.08)) = 0.028
        _WakeLightIntensity ("Light intensity beside wake", Range(0,1)) = 0.4
        _LightSway ("Light area sway", Range(0,0.4)) = 0.12
        _LightWakeDistortion ("Light wake distortion", Range(0,0.5)) = 0.2

        [HideInInspector] _OceanWaveOffsets ("Wave offsets", 2D) = "black" {}
        _OceanBoatFlowMap ("Boat height / foam source / foam velocity", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ OCEAN_REFERENCE_SLOPE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_OceanBaseFlowMap); SAMPLER(sampler_OceanBaseFlowMap);
            TEXTURE2D(_OceanFinalFlowMap); SAMPLER(sampler_OceanFinalFlowMap);
            TEXTURE2D(_OceanFoamMap); SAMPLER(sampler_OceanFoamMap);
            TEXTURE2D(_OceanBoatFlowMap); SAMPLER(sampler_OceanBoatFlowMap);
            float4 _OceanBoatFlowMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
            float4 _DeepColor, _MidWaterColor, _CrestColor, _FoamColor;
            float4 _FieldOrigin, _FieldWorldSize;
            float4x4 _OceanWorldToField;
            float4 _OceanLightTravel;
            float _WaveFrequency, _WaveSpeed, _NormalStrength, _CellHighlights;
            float _OceanTime, _DebugMode, _LightWidth, _WakeLightIntensity;
            float _LightSway, _LightWakeDistortion;
            CBUFFER_END
            #include "OceanLightArea.hlsl"
            TEXTURE2D(_OceanLightPattern); SAMPLER(sampler_OceanLightPattern);
            float _OceanLightReady;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 ocean : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 field = mul(_OceanWorldToField,float4(world,1)).xyz;
                output.ocean = field.xy;
                output.positionCS = TransformWorldToHClip(world);
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
            float LightArea(float2 world, float heightDelta, float2 wakeSlope)
            {
                if(_OceanLightReady<.5) return 0;
                world += _OceanLightTravel.xy;
                float scale=max(_WaveFrequency*.8,.001);
                float2 p=float2(.8*world.x-.6*world.y,.6*world.x+.8*world.y)*scale;
                float t=_OceanTime*_WaveSpeed;
                p+=float2(sin(p.y*.73+t*.45),sin(p.x*.61-t*.38))*(_LightSway*.25);
                // Bounded, continuous refraction-like distortion. No exclusion mask,
                // sign test, connectivity search or Base-wave height in this input.
                float2 slope=float2(.8*wakeSlope.x-.6*wakeSlope.y,
                    .6*wakeSlope.x+.8*wakeSlope.y);
                p+=slope*rsqrt(1+dot(slope,slope))*_LightWakeDistortion;
                float4 pattern=SAMPLE_TEXTURE2D(_OceanLightPattern,sampler_OceanLightPattern,p/16);
                float width=_LightWidth*pattern.g*pattern.b;
                float aa=max(.001,fwidth(pattern.r));
                float coverage=1-smoothstep(max(0,width-aa),width+aa,pattern.r);
                float activity=heightDelta*heightDelta+dot(wakeSlope,wakeSlope)*.1;
                float influence=activity/(activity+.04);
                return coverage*lerp(1,_WakeLightIntensity,influence);
            }

            float2 Flow(float2 p)
            {
                float2 uv=(p-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001);
                return SAMPLE_TEXTURE2D(_OceanFinalFlowMap,sampler_OceanFinalFlowMap,saturate(uv)).xy;
            }
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
            float2 BoatContribution(float2 world)
            {
                float2 uv=saturate((world-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001));
                return SAMPLE_TEXTURE2D_LOD(_OceanBoatFlowMap,sampler_OceanBoatFlowMap,uv,0).rg;
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
                float wakeHeight;
                float2 wakeSlope;
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
                float2 contribution=BoatContribution(world);
                surface.wakeHeight=contribution.r;
                float2 dx=float2(e,0), dy=float2(0,e);
                float4 qLeft=SurfaceCoordinates(world-dx), qRight=SurfaceCoordinates(world+dx);
                float4 qDown=SurfaceCoordinates(world-dy), qUp=SurfaceCoordinates(world+dy);
                float leftR=BoatContribution(world-dx).r, rightR=BoatContribution(world+dx).r;
                float downR=BoatContribution(world-dy).r, upR=BoatContribution(world+dy).r;
                surface.wakeSlope=float2(rightR-leftR,upR-downR)/(2*e);
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
                        );
                }
                surface.normal=normalize(float3(-surface.slope,1));
                return surface;
            }
            int _OceanContactCount;
            float4 _OceanContactEdges[64];
            float4 _OceanContactShapes[8];
            float4 _OceanContactBounds[8];

            float SurfaceContact(float2 p, float pixelSize)
            {
                float coverage = 0;
                [loop] for (int i = 0; i < _OceanContactCount; i++)
                {
                    float4 bounds = _OceanContactBounds[i];
                    if (any(p < bounds.xy) || any(p > bounds.zw)) continue;
                    float4 shape = _OceanContactShapes[i];
                    float distanceSquared = 1e10;
                    bool inside = false;
                    [loop] for (int j = 0; j < (int)shape.x; j++)
                    {
                        float4 edge = _OceanContactEdges[i * 8 + j];
                        float2 axis = edge.zw - edge.xy;
                        float2 offset = p - edge.xy;
                        float t = saturate(dot(offset, axis) / max(dot(axis, axis), 1e-8));
                        float2 delta = offset - t * axis;
                        distanceSquared = min(distanceSquared, dot(delta, delta));
                        if (shape.w > 0 && ((edge.y > p.y) != (edge.w > p.y)))
                            inside = inside != (offset.x < axis.x * offset.y / axis.y);
                    }
                    float distanceToOutline = sqrt(distanceSquared) - shape.y;
                    if (inside || distanceToOutline < 0) continue;
                    float ripple = Noise(p * 2.4 + _OceanTime * float2(.53, -.37));
                    float width = shape.z * lerp(.30, .96, ripple);
                    float feather = min(pixelSize * .65, width * .35);
                    float mask = 1 - smoothstep(width - feather, width + feather, distanceToOutline);
                    coverage = max(coverage, mask);
                }
                return coverage;
            }

            half4 Fragment(Varyings input) : SV_Target
            {

                float2 uv=saturate((input.ocean-_FieldOrigin.xy)/max(_FieldWorldSize.xy,.001));

                float density=SAMPLE_TEXTURE2D(_OceanFoamMap,sampler_OceanFoamMap,uv).r;
                if(_DebugMode>.5)
                {
                    float4 boat=SAMPLE_TEXTURE2D(_OceanBoatFlowMap,sampler_OceanBoatFlowMap,uv);
                    if(_DebugMode>=5.5) return half4(saturate(boat.g).xxx,1);
                    if(_DebugMode>=4.5) return half4(saturate(.5+boat.r).xxx,1);
                    if(_DebugMode>=3.5) return half4(density.xxx,1);
                    float2 v;
                    if(_DebugMode<1.5) v=SAMPLE_TEXTURE2D(_OceanBaseFlowMap,sampler_OceanBaseFlowMap,uv).xy;
                    else if(_DebugMode<2.5) v=SAMPLE_TEXTURE2D(_OceanBoatFlowMap,sampler_OceanBoatFlowMap,uv).ba;
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
                [branch] if(_CellHighlights>0)
                    color=lerp(color,_CrestColor.rgb,
                        LightArea(input.ocean,surface.wakeHeight,surface.wakeSlope)*_CellHighlights);
                color+=glint*.12;
                // History owns the silhouettes and their motion. Decay erodes
                // coverage rather than fading a broad translucent white sheet.
                float edgeWidth=max(0.035, fwidth(density));
                float foam=smoothstep(.22-edgeWidth,.22+edgeWidth,density);
                return half4(lerp(color,_FoamColor.rgb,max(foam,SurfaceContact(input.ocean, max(length(fwidth(input.ocean)), .0001)))),1);
            }
            ENDHLSL
        }
    }
}
