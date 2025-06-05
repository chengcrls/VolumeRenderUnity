Shader "Unlit/volume1"
{
    Properties
    {
        _Color("Color",Color) = (1,1,1)
        _Center1 ("Center1",Vector) = (1.5,0,0)
        _Center2 ("Center2",Vector) = (-1.5,0,0)
        _Radius1 ("Radius1", Range(0,5)) = 0.8
        _Radius2 ("Radius2", Range(0,5)) = 0.8
        _SpecularPower("SpecularPower",Range(0,20))=4
    }
    SubShader
    {
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "RenderType" = "Opaque" }
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;    // Clip space
                float3 wPos : TEXCOORD1;    // World position
            };

            float3 _Center1;
            float3 _Center2;
            float _Radius1;
            float _Radius2;
            float _SpecularPower;
            float4 _Color;

            float sdf_sphere(float3 p, float3 c, float r)
            {
                return distance(p,c)-r;
            }

            float sdfSmin(float a, float b, float k = 32)
            {
                float res = exp(-k * a) + exp(-k * b);
                return -log(max(0.00001,res))/k;
            }
            
            float sphereDistance(float3 p)
            {
                //_Center1.y = _SinTime[3]*4;
                return sdfSmin(sdf_sphere(p,_Center1,_Radius1),sdf_sphere(p,_Center2,_Radius2),2);
                return min
                (
                    sdf_sphere(p,_Center1,_Radius1),
                    sdf_sphere(p,_Center2,_Radius2)
                );
            }
            
            float3 normal(float3 p)
            {
                float eps =0.01;
                return normalize
                (float3
                    (sphereDistance(p + float3(eps, 0, 0)) - sphereDistance(p - float3(eps, 0, 0)),
                        sphereDistance(p + float3(0, eps, 0)) - sphereDistance(p - float3(0, eps, 0)),
                        sphereDistance(p + float3(0, 0, eps)) - sphereDistance(p - float3(0, 0, eps))
                        )
                );
            }
            float ambientOcclusion(float3 pos, float3 normal)
            {
                float sum = 0;
                float maxSum = 0;
                float AOStep = 30;
                float AOStepSize = 8.5;
                for(int i = 0; i < AOStep; ++i)
                {
                    float3 p = pos + normal * (i + 1) * AOStepSize;
                    sum += 1./pow(2.,i) * sphereDistance(p);
                    maxSum += 1./pow(2.,i) * (i+1) * AOStepSize;
                }
                return sum/maxSum;
            }
            
            half3 simpleLambertBlinn(half3 normal,half3 direction)
            {
                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half3 lightColor = mainLight.color;
                half3 viewDirection = direction;
                //half3 lightDir = normalize(-_MainLightPosition.xyz);//_WorldSpaceLightPos0.xyz;
                half3 h = normalize(lightDir+viewDirection);

                //half3 lightCol = _MainLightColor.rgb;//_LightColor0.rgb;
                
                float NdotL=max(dot(normal,lightDir),0);
                float NdotH=max(dot(normal,h),0);

                half3 diffuse = lightColor * NdotL;
                half3 specular = lightColor*pow(NdotH,_SpecularPower);
                
                return specular + diffuse ;
            }

            half4 renderSurface(float3 p, float3 direction)
            {
                float3 n = normal(p);
                return half4(simpleLambertBlinn(n,direction),1);
            }
            //光线步进
            half4 raymarchHit(float3 position, float3 direction)
            {
                float STEPS = 64;
                float STEP_SIZE = 0.01;
                for (int i = 0; i < STEPS; i++)
                {
                    float distance = sphereDistance(position);
                    if (distance<STEP_SIZE)
                    {
                        half4 color = renderSurface(position,-direction);
                        float ao = ambientOcclusion(position, normal(position));
                        color.rgb *= ao;
                        return color;
                    }
                    position += direction * distance;
                }
                return 1;
            }


            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.pos = vertexInput.positionCS;
                o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 worldPosition = i.wPos;
                float3 viewDirection = normalize(i.wPos - _WorldSpaceCameraPos);
                return raymarchHit(worldPosition,viewDirection);
            }
        ENDHLSL
        }
    }
}
