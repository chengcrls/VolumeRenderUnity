Shader "Hidden/Custom/AtmosphereScattering"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D_X(_CameraDepthTexture);
    
    CBUFFER_START(UnityPerMaterial)
        // 大气散射参数
        float _ScatteringIntensity;
        float3 _RayleighScattering;
        float _MieScattering;
        float _MieAnisotropy;
        
        // 大气层参数
        float _AtmosphereHeight;
        float _RayleighHeight;
        float _MieHeight;
        float _EarthRadius;
        
        // 太阳光照参数
        float _SunIntensity;
        float3 _SunColor;
        float3 _SunDirection;
        
        // 渲染参数
        int _SampleCount;
        int _LightSampleCount;
        
        // 摄像机参数
        float4x4 _InverseViewMatrix;
        float4x4 _InverseProjectionMatrix;
        float3 _CameraPosition;
    CBUFFER_END

    // 计算球体相交
    float2 RayIntersectSphere(float3 rayOrigin, float3 rayDirection, float3 sphereCenter, float sphereRadius)
    {
        float3 offset = rayOrigin - sphereCenter;
        float a = dot(rayDirection, rayDirection);
        float b = 2.0 * dot(offset, rayDirection);
        float c = dot(offset, offset) - sphereRadius * sphereRadius;
        float discriminant = b * b - 4.0 * a * c;
        
        if (discriminant > 0.0)
        {
            float sqrt_discriminant = sqrt(discriminant);
            float t1 = (-b - sqrt_discriminant) / (2.0 * a);
            float t2 = (-b + sqrt_discriminant) / (2.0 * a);
            return float2(t1, t2);
        }
        return float2(-1.0, -1.0);
    }

    // 瑞利散射相位函数
    float RayleighPhase(float cosTheta)
    {
        return 3.0 / (16.0 * PI) * (1.0 + cosTheta * cosTheta);
    }

    // 米氏散射相位函数 (Henyey-Greenstein)
    float MiePhase(float cosTheta, float g)
    {
        float g2 = g * g;
        return 3.0 / (8.0 * PI) * (1.0 - g2) / (2.0 + g2) * 
               (1.0 + cosTheta * cosTheta) / pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5);
    }

    // 计算密度比例
    float GetDensity(float height, float scaleHeight)
    {
        return exp(-height / scaleHeight);
    }

    // 计算光学深度
    float3 GetOpticalDepth(float3 rayOrigin, float3 rayDirection, float rayLength, int samples)
    {
        float3 atmosphereCenter = float3(0, -_EarthRadius, 0);
        float stepSize = rayLength / float(samples);
        float3 opticalDepth = 0.0;
        
        for (int i = 0; i < samples; i++)
        {
            float3 samplePos = rayOrigin + rayDirection * (float(i) + 0.5) * stepSize;
            float height = length(samplePos - atmosphereCenter) - _EarthRadius;
            
            if (height < 0.0) break;
            
            float rayleighDensity = GetDensity(height, _RayleighHeight);
            float mieDensity = GetDensity(height, _MieHeight);
            
            opticalDepth.xy += float2(rayleighDensity, mieDensity) * stepSize;
        }
        
        return opticalDepth;
    }

    // 主要大气散射计算
    float3 CalculateAtmosphereScattering(float3 rayOrigin, float3 rayDirection, float3 sunDirection)
    {
        // 与大气层相交检测
        float3 atmosphereCenter = float3(0, -_EarthRadius, 0);
        float2 atmosphereIntersection = RayIntersectSphere(rayOrigin, rayDirection, atmosphereCenter, _EarthRadius + _AtmosphereHeight);
        
        if (atmosphereIntersection.x < 0.0 && atmosphereIntersection.y < 0.0)
            return float3(0.0, 0.0, 0.0);
        
        // 计算射线起点和终点
        float rayStart = max(atmosphereIntersection.x, 0.0);
        float rayEnd = atmosphereIntersection.y;
        float rayLength = rayEnd - rayStart;
        
        if (rayLength <= 0.0)
            return float3(0.0, 0.0, 0.0);
        
        float stepSize = rayLength / float(_SampleCount);
        float3 totalRayleigh = 0.0;
        float3 totalMie = 0.0;
        
        float cosTheta = dot(rayDirection, sunDirection);
        float rayleighPhase = RayleighPhase(cosTheta);
        float miePhase = MiePhase(cosTheta, _MieAnisotropy);
        
        // 主要散射积分
        for (int i = 0; i < _SampleCount; i++)
        {
            float3 samplePos = rayOrigin + rayDirection * (rayStart + (float(i) + 0.5) * stepSize);
            float height = length(samplePos - atmosphereCenter) - _EarthRadius;
            
            if (height < 0.0) break;
            
            float rayleighDensity = GetDensity(height, _RayleighHeight);
            float mieDensity = GetDensity(height, _MieHeight);
            
            // 计算到太阳的光学深度
            float2 sunIntersection = RayIntersectSphere(samplePos, sunDirection, atmosphereCenter, _EarthRadius + _AtmosphereHeight);
            float sunRayLength = sunIntersection.y;
            
            float3 sunOpticalDepth = GetOpticalDepth(samplePos, sunDirection, sunRayLength, _LightSampleCount);
            float3 viewOpticalDepth = GetOpticalDepth(rayOrigin, rayDirection, (float(i) + 0.5) * stepSize, i + 1);
            
            float3 totalOpticalDepth = sunOpticalDepth + viewOpticalDepth;
            
            float3 rayleighExtinction = _RayleighScattering * totalOpticalDepth.x;
            float3 mieExtinction = _MieScattering * totalOpticalDepth.y;
            float3 extinction = rayleighExtinction + mieExtinction;
            
            float3 transmittance = exp(-extinction);
            
            totalRayleigh += rayleighDensity * transmittance * stepSize;
            totalMie += mieDensity * transmittance * stepSize;
        }
        
        float3 scattering = rayleighPhase * _RayleighScattering * totalRayleigh + 
                           miePhase * _MieScattering * totalMie;
        float3 sunColor = GetMainLight().color;
        
        return scattering * sunColor * _ScatteringIntensity;
    }

    // 从屏幕坐标重建世界坐标
    float3 GetWorldPosition(float2 screenUV, float depth)
    {
        float4 ndc = float4(screenUV * 2.0 - 1.0, depth, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
        ndc.y = -ndc.y;
        #endif
        
        float4 worldPos = mul(_InverseViewMatrix, mul(_InverseProjectionMatrix, ndc));
        return worldPos.xyz / worldPos.w;
    }

    float4 FragAtmosphereScattering(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 screenUV = input.texcoord;
        float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV);
        
        // 采样深度
        float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, screenUV).r;
        
        // 重建世界坐标和方向
        float3 worldPos = GetWorldPosition(screenUV, depth);
        float3 rayDirection = normalize(worldPos - _CameraPosition);

        //获取主光源方向
        float3 sunDirection = GetMainLight().direction;
        
        // 计算大气散射
        float3 scattering = CalculateAtmosphereScattering(_CameraPosition, rayDirection, sunDirection);
        
        // 应用散射到最终颜色
        color.rgb += scattering;
        
        return float4(scattering,1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "AtmosphereScattering"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAtmosphereScattering
            ENDHLSL
        }
    }

    Fallback Off
} 