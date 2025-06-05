Shader "Unlit/NewUnlitShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _SpecColor ("Specular Color", Color) = (1,1,1,1)
        _Shininess ("Shininess", Range(1, 256)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardBlinnPhong"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            // 必须包含这些头文件
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float _Shininess;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(OUT.worldPos);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 获取主光源信息
                Light light = GetMainLight();

                half3 N = normalize(IN.worldNormal);
                half3 L = normalize(light.direction); // 注意：已是指向光源方向
                half3 V = normalize(_WorldSpaceCameraPos - IN.worldPos);
                half3 H = normalize(L + V);

                // 光照计算
                half diff = saturate(dot(N, L));
                half spec = pow(saturate(dot(N, H)), _Shininess);

                half3 diffuse = _BaseColor.rgb * light.color * diff;
                half3 specular = _SpecColor.rgb * light.color * spec;

                return half4(specular + diffuse, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
