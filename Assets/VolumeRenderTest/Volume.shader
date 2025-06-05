Shader "Unlit/Volume"
{
    Properties
    {
        _Center ("Center",Vector) = (0,0,0)
        _Radius ("Radius", Range(0,1)) = 0.8
    }
    SubShader
        {
            LOD 100

            Pass
            {
                Tags { "LightMode" = "UniversalForward" "RenderType" = "Opaque" }
                Cull Off
                
                CGPROGRAM
                #include "Lighting.cginc"

                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"


                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f {
                    float4 pos : SV_POSITION;    // Clip space
                    float3 wPos : TEXCOORD1;    // World position
                };

                float3 _Center;
                fixed _Radius;

                // 判断是否进入球内
                bool sphereHit(float3 p)
                {
                    return distance(p, _Center) < _Radius;
                }

                float sphereDistance(float3 p)
                {
                    return distance(p,_Center)-_Radius;
                }

                //光线步进
                bool raymarchHit(float3 position, float3 direction)
                {
                    float STEPS = 1;
                    float STEP_SIZE = 0.01;
                    for (int i = 0; i < STEPS; i++)
                    {
                        float distance = sphereDistance(position);
                        if (distance<STEP_SIZE)
                            return true;

                        position += direction * distance;
                    }
                    return false;
                }


                v2f vert(appdata v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    float3 worldPosition = i.wPos;
                    float3 viewDirection = normalize(i.wPos - _WorldSpaceCameraPos);

                    if(raymarchHit(worldPosition,viewDirection))
                        clip(-1);
                    return float4(1,1,1,1);
                    return (1-raymarchHit(worldPosition,viewDirection))*float4(1,1,1,1);

                }
            ENDCG
            }
            // 阴影 Pass：用于阴影贴图生成
            Pass
            {
                Name "ShadowCaster"
                Tags { "LightMode" = "ShadowCaster" }

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"


                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f {
                    float4 pos : SV_POSITION;    // Clip space
                    float3 wPos : TEXCOORD1;    // World position
                };

                float3 _Center;
                fixed _Radius;

                // 判断是否进入球内
                bool sphereHit(float3 p)
                {
                    return distance(p, _Center) < _Radius;
                }

                float sphereDistance(float3 p)
                {
                    return distance(p,_Center)-_Radius;
                }

                //光线步进
                bool raymarchHit(float3 position, float3 direction)
                {
                    float STEPS = 1;
                    float STEP_SIZE = 0.01;
                    for (int i = 0; i < STEPS; i++)
                    {
                        float distance = sphereDistance(position);
                        if (distance<STEP_SIZE)
                            return true;

                        position += direction * distance;
                    }
                    return false;
                }


                v2f vert(appdata v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    float3 worldPosition = i.wPos;
                    float3 viewDirection = normalize(i.wPos - _WorldSpaceCameraPos);

                    if(raymarchHit(worldPosition,viewDirection))
                        clip(-1);
                    return float4(1,1,1,1);
                }

                ENDCG
            }
        }

}
