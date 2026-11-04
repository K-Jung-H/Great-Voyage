Shader "Great Voyage/Interaction Outline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline", Color) = (1,.85,.45,1)
        _OutlinePixels ("Outline Pixels", Float) = 1.25
        _SpriteUvRect ("Sprite UV Bounds", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float4 _SpriteUvRect;
                float _OutlinePixels;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half AlphaAt(float2 uv)
            {
                if (any(uv < _SpriteUvRect.xy) || any(uv > _SpriteUvRect.zw)) return 0;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                float2 stepUV = (abs(ddx(input.uv)) + abs(ddy(input.uv))) * _OutlinePixels;
                half a = AlphaAt(input.uv + float2(stepUV.x, 0));
                a = min(a, AlphaAt(input.uv - float2(stepUV.x, 0)));
                a = min(a, AlphaAt(input.uv + float2(0, stepUV.y)));
                a = min(a, AlphaAt(input.uv - float2(0, stepUV.y)));
                a = min(a, AlphaAt(input.uv + stepUV * .7071));
                a = min(a, AlphaAt(input.uv - stepUV * .7071));
                a = min(a, AlphaAt(input.uv + float2(stepUV.x, -stepUV.y) * .7071));
                a = min(a, AlphaAt(input.uv + float2(-stepUV.x, stepUV.y) * .7071));
                // Recolor a thin inner silhouette edge; preserve source alpha and all interior material detail.
                half edge = (1 - smoothstep(.02, .5, a)) * step(.01, color.a);
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, edge * _OutlineColor.a);
                return color;
            }
            ENDHLSL
        }
    }
}
