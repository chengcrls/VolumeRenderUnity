Shader "Unlit/CloudSea"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Height("Height",Range(0,1))=0.5
        _HeightTileSpeed("Turbulence Tile&Speed",Vector) = (1.0,1.0,0.05,0.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags
            {
                "RenderPipeline" = "UniversalPipeline"
            }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            float _Height;
            TEXTURE2D(_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _HeightTileSpeed;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS : TANGENT;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 uv   : TEXCOORD2;
                float3 viewDirTS : TEXCOORD3;
            };

            Varyings vert(Attributes INPUT)
            {
                Varyings OUTPUT;
                OUTPUT.positionHCS = TransformObjectToHClip(INPUT.positionOS);
                OUTPUT.positionWS = TransformObjectToWorld(INPUT.positionOS);
                OUTPUT.normalWS = TransformObjectToWorldNormal(INPUT.normalOS);
                OUTPUT.uv.xy = TRANSFORM_TEX(INPUT.uv,_MainTex) + frac(_Time.y*_HeightTileSpeed.zw);
                OUTPUT.uv.zw = INPUT.uv*_HeightTileSpeed.xy;
                
                float3 tangentWS = TransformObjectToWorldDir(INPUT.tangentOS.xyz, true);
                float tangentSign = INPUT.tangentOS.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(OUTPUT.normalWS, tangentWS) * tangentSign;
                float3x3 worldToTangent = float3x3(tangentWS,bitangentWS,OUTPUT.normalWS);
                
                float3 viewDir = _WorldSpaceCameraPos - OUTPUT.positionWS;
                
                OUTPUT.viewDirTS = TransformWorldToTangentDir(viewDir,worldToTangent, true);
                
                return OUTPUT;
            }
            half4 frag(Varyings INPUT) : SV_Target
            {
                float3 viewDirTS = normalize(-1*INPUT.viewDirTS);
                viewDirTS.xy*=_Height;
                
                float3 shadeP=float3(INPUT.uv.xy,0);
                
                float linearStep = 16;
                
                float3 viewDirOffset = viewDirTS/(viewDirTS.z * linearStep);
                float d = 1-SAMPLE_TEXTURE2D(_MainTex,sampler_LinearRepeat,shadeP.xy).a;
                float prevD = d;
                float3 prevShadeP = shadeP;
                float i =0;
                UNITY_LOOP
                while (d>shadeP.z)
                {
                    i++;
                    prevShadeP = shadeP;
                    shadeP+=viewDirOffset;
                    prevD = d;
                    d = 1-SAMPLE_TEXTURE2D(_MainTex,sampler_LinearRepeat,shadeP.xy).a;
                }
                float d1 = d-shadeP.z;
                float d2 = prevD-prevShadeP.z;
                float w = d1/(d1-d2);
                shadeP=lerp(shadeP,prevShadeP,w);

                half3 color = SAMPLE_TEXTURE2D(_MainTex,sampler_LinearRepeat,shadeP.xy).rgb;

                float3 noramlWS = normalize(INPUT.normalWS);
                Light light = GetMainLight();
                float3 lightDirWS = light.direction;
                float NdotL = max(0,dot(noramlWS,lightDirWS));
                half3 finalColor = color * (light.color.rgb*NdotL+0.1)*_Color.rgb;
                return half4(finalColor,1);
            }
            ENDHLSL
        }
    }
}
