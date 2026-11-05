Shader "Great Voyage/Mirrored World Surface"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _UvRect ("Atlas bounds", Vector) = (0,0,1,1)
        _TileSize ("World tile size", Vector) = (3.3,3.3,0,0)
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
                float4 _UvRect;
                float4 _TileSize;
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
                // Mirror-repeat the original interior atlas region: neighboring edges sample the same texels.
                // This changes sampling only; no image pixels or style-generation rules are modified.
                float2 uv = abs(frac(input.surfacePosition / _TileSize.xy * .5) * 2 - 1);
                uv = lerp(_UvRect.xy, _UvRect.zw, uv);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
            }
            ENDHLSL
        }
    }
}
