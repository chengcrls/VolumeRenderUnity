Shader "Unlit/texture3Dtest"
{
    Properties
    {
        MainTex("MainTex",3D)=""{}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float4 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = float4(TransformObjectToWorld(IN.positionOS),1);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 customColor = SAMPLE_TEXTURE3D(MainTex,sampler_MainTex,IN.positionWS.xyz);
                
                return customColor;
            }
            ENDHLSL
        }
    }
}
