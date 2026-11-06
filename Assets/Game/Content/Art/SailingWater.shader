Shader "Great Voyage/Sailing Water"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _UvRect ("Atlas bounds", Vector) = (0,0,1,1)
        _TileSize ("World tile size", Vector) = (3.3,3.3,0,0)
        _SeaPosition ("Logical displacement", Vector) = (0,0,0,0)
        _Heading ("Heading radians", Float) = 3.14159265
        _Sail ("Underway", Float) = 0
        _WavePhase ("Game time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 surfacePosition : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _UvRect, _TileSize, _SeaPosition;
                float _Heading, _Sail, _WavePhase;
            CBUFFER_END
            Varyings Vertex(Attributes input)
            {
                Varyings output = CommonUnlitVertex(input);
                output.surfacePosition = input.positionOS.xy;
                output.color = input.color;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.surfacePosition;
                float s, c; sincos(_Heading, s, c);
                float2 world = float2(-p.x*c-p.y*s, -p.x*s+p.y*c) + _SeaPosition.xy;
                world += .025 * float2(sin(p.y*.8+_WavePhase*.45), cos(p.x*.7+_WavePhase*.4));
                float2 uv = abs(frac(world / _TileSize.xy * .5) * 2 - 1);
                half4 surface = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, lerp(_UvRect.xy,_UvRect.zw,uv)) * input.color;
                float bow = exp(-pow((length((p-float2(-6.2,0))/float2(1.25,1.9))-1)*20,2)) * (1-smoothstep(-6.2,-5.7,p.x));
                float tailX = max(0,p.x-6.7);
                float edge = abs(abs(p.y)-(.5+tailX*.2));
                float wake = exp(-edge*edge*90) * smoothstep(6.7,7.4,p.x) * exp(-tailX*.45);
                float foam = saturate((bow*.55+wake*.4)*_Sail) * (.6+.4*sin(p.x*5+_WavePhase*3)*sin(p.y*8-_WavePhase*2));
                surface.rgb = lerp(surface.rgb,half3(.78,.91,.9),foam);
                return surface;
            }
            ENDHLSL
        }
    }
}
