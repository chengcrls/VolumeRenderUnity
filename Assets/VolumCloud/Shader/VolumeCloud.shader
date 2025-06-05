Shader "Unlit/VolumeCloud"
{
    Properties
    {
        _BaseColor("BaseColor",Color)=(1,1,1,1)
        _Size("Size",Vector)=(1,1,1)
        _SpecColor("Specular Color",Color)=(1,1,1,1)
        _Shininess("Shininess",Range(1,256))=32
        cloudShape("cloudShape",3D)=""{}
    }
    SubShader
    {
        LOD 100
        Pass
        {
            Tags{"LightMode"="UniversalForward" "RenderType"="Opaque"}
            HLSLPROGRAM

            #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            struct Attribute
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float3 _Center;
                float3 _Size;
                float4 _SpecColor;
                float _Shiness;
                float3 boundsMin;
                float3 boundsMax;
            CBUFFER_END

            TEXTURE3D(cloudShape);
            SAMPLER(sampler_cloudShape);

            float sdfBox(float3 p,float3 c,float3 s)
            {
                float x = max
                (p.x - c.x - float3(s.x / 2., 0, 0),
                    c.x - p.x - float3(s.x / 2., 0, 0)
                );
                float y = max
                (p.y - c.y - float3(s.y / 2., 0, 0),
                    c.y - p.y - float3(s.y / 2., 0, 0)
                );

                float z = max
                (p.z - c.z - float3(s.z / 2., 0, 0),
                    c.z - p.z - float3(s.z / 2., 0, 0)
                );
                float d = x;
                d = max(d, y);
                d = max(d, z);
                return d;
            }

            float sdfSphere(float3 p, float3 c, float3 s)
            {
                return distance(p, c) - s.x;
            }

            float sdfBlend(float d1, float d2, float a)
            {
                return a * d1 + (1 - a) * d2;
            }
            float sdfDistance(float3 p)
            {
                return sdfBox(p,_Center,_Size);
                //return sdfBlend(sdfSphere(p, _Center, _Size), sdfBox(p, _Center, _Size), (_SinTime[3] + 1.)/2.);
            }

            float3 normal(float3 p)
            {
                const float eps = 0.01;
                return normalize
                (float3
                    (sdfDistance(p + float3(eps, 0, 0)) - sdfDistance(p - float3(eps, 0, 0)),
                        sdfDistance(p + float3(0, eps, 0)) - sdfDistance(p - float3(0, eps, 0)),
                        sdfDistance(p + float3(0, 0, eps)) - sdfDistance(p - float3(0, 0, eps))
                        )
                );
            }

            half4 blinnPhone(half3 normal,half3 direction)
            {
                Light mainLight = GetMainLight();
                
                half3 N = normalize(normal);
                half3 L = normalize(mainLight.direction);
                half3 V = normalize(direction);
                half3 H = normalize(L+V);

                half diff = saturate(dot(N,L));
                half spec = pow(saturate(dot(N,H)),_Shiness);

                half3 diffuse = _BaseColor.rgb * mainLight.color * diff;
                half3 specular = _SpecColor.rgb * mainLight.color * spec;

                return half4(diffuse + specular, 1.0);
            }

            half4 renderSurface(float3 p,float3 direction)
            {
                float3 n = normal(p);
                return blinnPhone(n, direction);
            }

            half4 rayMarch(float3 p, float3 direction)
            {
                float STEPS = 64;
                float STEP_SIZE = 0.01;
                for(int i = 0; i < STEPS; ++i)
                {
                    float distance = sdfDistance(p);
                    if(distance < STEP_SIZE)
                        return renderSurface(p, -direction);
                    p += distance * direction;
                }
                return 1;
            }

            Varyings vert(Attribute IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirection = normalize(IN.worldPos - _WorldSpaceCameraPos);
                float3 pos = IN.worldPos;
                float stepSize=0.1;
                float density=0;
                for(int i=0;i<32;++i)
                {
                    pos+=viewDirection*stepSize;
                    if(all(pos<boundsMax)&&all(pos>boundsMin))
                    {
                        density +=pow(SAMPLE_TEXTURE3D(cloudShape,sampler_cloudShape,pos).r,5);
                        //density+=0.01;
                    }
                }
                return density;
                return rayMarch(IN.worldPos,viewDirection);
            }
            
            ENDHLSL
        }
    }
}
