Shader "Hidden/PostProcessing/ColorTint"
{
    HLSLINCLUDE
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    #define STEP_SIZE 0.1
    #define STEP_NUM 100
    float4 _boundsMin;
    float4 _boundsMax;
    float cloudScale;
    float _lightAbsorptionTowardSun;
    float _lightAbsorptionThroughCloud;
    float _shapeTiling;
    float _detailTiling;
    float _detailWeights;
    float _detailNoiseWeight;
    
    float _step;
    float _rayStep;
    
    float4 _colA;
    float4 _colB;
    float _colorOffset1;
    float _colorOffset2;
    
    float4 _BlueNoiseST;
    float4 _phaseParams;
    float4 _shapeNoiseWeights;
    float4 _xy_Speed_zw_Warp;
    float _rayOffsetStrength;

    float _densityMultiplier;
    float _densityOffset;
    float _heightWeights;
    
    TEXTURE3D(cloudShape);
    SAMPLER(sampler_cloudShape);
    TEXTURE3D(cloudDetail);
    SAMPLER(sampler_cloudDetail);
    TEXTURE2D(DownsampleColor);
    SAMPLER(sampler_DownsampleColor);

    TEXTURE2D(weatherMap);
    SAMPLER(sampler_weatherMap);
    TEXTURE2D(maskMap);
    SAMPLER(sampler_maskMap);

    TEXTURE2D(BlueNoise);
    SAMPLER(sampler_BlueNoise);
    
    float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
    {
       return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
    }
    
    float sampleDensity(float3 rayPos) 
    {
        float4 boundsCentre = (_boundsMax + _boundsMin) * 0.5;
        float3 size = _boundsMax - _boundsMin;
        float speedShape = _Time.y * _xy_Speed_zw_Warp.x;
        float speedDetail = _Time.y * _xy_Speed_zw_Warp.y;

        float3 uvwShape  = rayPos * _shapeTiling + float3(speedShape, speedShape * 0.2,0);
        float3 uvwDetail = rayPos * _detailTiling + float3(speedDetail, speedDetail * 0.2,0);

        float2 uv = (size.xz * 0.5f + (rayPos.xz - boundsCentre.xz) ) /max(size.x,size.z);

        float4 maskNoise = SAMPLE_TEXTURE2D_LOD(maskMap, sampler_maskMap, uv + float2(speedShape * 0.5, 0),0);
        float4 weather = SAMPLE_TEXTURE2D_LOD(weatherMap,sampler_weatherMap, uv + float2(speedShape * 0.4, 0), 0);

        float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(cloudShape, sampler_cloudShape,uvwShape + (maskNoise.r * _xy_Speed_zw_Warp.z * 0.1),0);
        float4 detailNoise = SAMPLE_TEXTURE3D_LOD(cloudDetail, sampler_cloudDetail, uvwDetail + (shapeNoise.r * _xy_Speed_zw_Warp.w * 0.1), 0);

        //边缘衰减
        const float containerEdgeFadeDst = 10;
        float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - _boundsMin.x, _boundsMax.x - rayPos.x));
        float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - _boundsMin.z, _boundsMax.z - rayPos.z));
        float edgeWeight = min(dstFromEdgeZ, dstFromEdgeX) / containerEdgeFadeDst;

        float gMin = remap(weather.x, 0, 1, 0.1, 0.6);
        float gMax = remap(weather.x, 0, 1, gMin, 0.9);
        float heightPercent = (rayPos.y - _boundsMin.y) / size.y;
        float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1, gMax, 0, 1));
        float heightGradient2 = saturate(remap(heightPercent, 0.0, weather.r, 1, 0)) * saturate(remap(heightPercent, 0.0, gMin, 0, 1));
        heightGradient = saturate(lerp(heightGradient, heightGradient2,_heightWeights));

        heightGradient *= edgeWeight;

        float4 normalizedShapeWeights = _shapeNoiseWeights / dot(_shapeNoiseWeights, 1);
        float shapeFBM = dot(shapeNoise, normalizedShapeWeights) * heightGradient;
        float baseShapeDensity = shapeFBM + _densityOffset * 0.01;


        if (baseShapeDensity > 0)
        {
            float detailFBM = pow(detailNoise.r, _detailWeights);
            float oneMinusShape = 1 - baseShapeDensity;
            float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
            float cloudDensity = baseShapeDensity - detailFBM * detailErodeWeight * _detailNoiseWeight;

            return saturate(cloudDensity * _densityMultiplier);
        }
        return 0;
    }
    
    // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
    float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
        // Adapted from: http://jcgt.org/published/0007/03/04/
        float3 t0 = (boundsMin - rayOrigin) * invRaydir;
        float3 t1 = (boundsMax - rayOrigin) * invRaydir;
        float3 tmin = min(t0, t1);
        float3 tmax = max(t0, t1);
        
        float dstA = max(max(tmin.x, tmin.y), tmin.z);
        float dstB = min(tmax.x, min(tmax.y, tmax.z));

        // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
        // dstA is dst to nearest intersection, dstB dst to far intersection

        // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
        // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

        // CASE 3: ray misses box (dstA > dstB)

        float dstToBox = max(0, dstA);
        float dstInsideBox = max(0, dstB - dstToBox);
        return float2(dstToBox, dstInsideBox);
    }

    float3 lightMarch(float3 position ,float dstTravelled)
    {
        Light light=GetMainLight();
        float3 dirToLight = light.direction;

        //灯光方向与边界框求交，超出部分不计算
        float dstInsideBox = rayBoxDst(_boundsMin, _boundsMax, position, 1 / dirToLight).y;
        float stepSize = dstInsideBox / 8;
        float totalDensity = 0;

        for (int step = 0; step < 8; step++) { //灯光步进次数
            position += dirToLight * stepSize; //向灯光步进
            //totalDensity += max(0, sampleDensity(position) * stepSize);                     totalDensity += max(0, sampleDensity(position) * stepSize);
            totalDensity += max(0, sampleDensity(position));

        }
        float transmittance = exp(-totalDensity * _lightAbsorptionTowardSun);

        //将重亮到暗映射为 3段颜色 ,亮->灯光颜色 中->ColorA 暗->ColorB
        float3 cloudColor = lerp(_colA, light.color, saturate(transmittance * _colorOffset1));
        cloudColor = lerp(_colB, cloudColor, saturate(pow(transmittance * _colorOffset2, 3)));
        return cloudColor;
        //return _darknessThreshold + transmittance * (1 - _darknessThreshold) * cloudColor;
    }

    // Henyey-Greenstein
    float hg(float a, float g) {
        float g2 = g * g;
        return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
    }

    float phase(float a) {
        float blend = .5;
        float hgBlend = hg(a, _phaseParams.x) * (1 - blend) + hg(a, -_phaseParams.y) * blend;
        return _phaseParams.z + hgBlend * _phaseParams.w;
    }
    
    half4 Frag(Varyings input) : SV_Target
    {
        
        float2 UV = input.texcoord;
        //half4 originColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, UV);

        float depthUV = ClampAndScaleUVForBilinear(UnityStereoTransformScreenSpaceTex(UV), _CameraDepthTexture_TexelSize.xy);

        #if UNITY_REVERSED_Z
            real depth = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, UV).r;
        #else
            // Adjust Z to match NDC for OpenGL ([-1, 1])
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, depthUV).r);
        #endif

        // Reconstruct the world space positions.
        float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
        //return worldPos;
        float3 cameraPos = _WorldSpaceCameraPos;
        float3 worldViewDir = normalize(worldPos.xyz - cameraPos.xyz);

        float2 rayToContainerInfo = rayBoxDst(_boundsMin, _boundsMax,cameraPos,1.0/worldViewDir);
        float dstToBox = rayToContainerInfo.x;
        float dstInsideBox = rayToContainerInfo.y;
        
        // 与云云容器的交汇点
        float3 rayPos = _WorldSpaceCameraPos;
        float3 entryPoint = rayPos + worldViewDir * dstToBox;

        //相机到物体的距离 - 相机到容器的距离
        float cameraToObjectDistance = length(worldPos - cameraPos);
        float dstLimit = min(cameraToObjectDistance-dstToBox,dstInsideBox);

        //添加抖动
        float blueNoise = SAMPLE_TEXTURE2D(BlueNoise,sampler_BlueNoise,input.texcoord*_BlueNoiseST.xy);

        //向灯光方向的散射更强一些
        Light light = GetMainLight();
        float cosAngle = dot(worldViewDir, light.direction);
        float3 phaseVal = phase(cosAngle);

        float dstTravelled = blueNoise.r * _rayOffsetStrength;
        float sumDensity = 1;
        float3 lightEnergy = 0;
        const float sizeLoop = 512;
        float stepSize = exp(_step)*_rayStep;
        
        for (int j = 0; j < sizeLoop; j++)
        {
            if(dstTravelled < dstLimit)
            { 
                rayPos = entryPoint + (worldViewDir * dstTravelled);
                float density = sampleDensity(rayPos);
                if (density > 0)
                {
                    float3 lightTransmittance = lightMarch(rayPos, dstTravelled);
                    lightEnergy += density * stepSize * sumDensity * lightTransmittance * phaseVal;
                    sumDensity *= exp(-density * stepSize * _lightAbsorptionThroughCloud);
                }
            }
            dstTravelled += stepSize;
        }

		return float4(lightEnergy, sumDensity);

    }

    float DownsampleDepth(Varyings input) : SV_Target
    {
        float2 texelSize = 0.5 * _CameraDepthTexture_TexelSize.xy;
        float2 taps[4]={float2(input.texcoord+float2(-1,-1)*texelSize),
                        float2(input.texcoord+float2(-1,1)*texelSize),
                        float2(input.texcoord+float2(1,-1)*texelSize),
                        float2(input.texcoord+float2(1,1)*texelSize)};
        float depth1= SampleSceneDepth(taps[0]);
        float depth2= SampleSceneDepth(taps[1]);
        float depth3= SampleSceneDepth(taps[2]);
        float depth4= SampleSceneDepth(taps[3]);

        float result = max(depth1,max(depth2,max(depth3,depth4)));
        return result;
    }

    float4 FragCombine(Varyings input) : SV_Target
    {
        float4 cloudColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
        return cloudColor;
    }
    
    
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Pass
        {
            ZTest Off ZWrite Off Cull Off
            Name "VolumeCloudPass"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Name "DownSample"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DownsampleDepth
            ENDHLSL
        }

        Pass
        {
            ZTest Off ZWrite Off Cull Off
            Blend one SrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCombine

            ENDHLSL
        }
        
    }
}