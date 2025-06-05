Shader "Hidden/PostProcessing/volumCloud"
{
    HLSLINCLUDE
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    #define STEP_SIZE 0.2
    float4 boundsMin;
    float4 boundsMax;
    float cloudScale;
    float lightAbsorption;
    float _shapeTiling;
    float _detailTiling;
    float _detailWeights;
    float _detailNoiseWeight;
    float4 colorA;
    float4 colorB;
    float4 _BlueNoiseST;
    float4 _phaseParams;
    float4 _shapeNoiseWeights;
    float _rayOffsetStrength;

    float _CloudTypeOffset;
    
    TEXTURE3D(cloudShape);
    SAMPLER(sampler_cloudShape);
    TEXTURE3D(cloudDetail);
    SAMPLER(sampler_cloudDetail);

    TEXTURE2D(weatherMap);
    SAMPLER(sampler_weatherMap);

    TEXTURE2D(_BlueNoise);
    SAMPLER(sampler_BlueNoise);

    // inCloudMinMax的x,y分量分别代表云层最低高度与最高高度
    float GetHeightFractionForPoint( float3 inPosition, float2 inCloudMinMax )
    {
        float height_fraction = (inPosition.y - inCloudMinMax.x) / (inCloudMinMax.y - inCloudMinMax.x);
        return saturate(height_fraction);
    }
    float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
    {
       return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
    }
    float GetDensityHeightGradientForPoint(in float RelativeHeight, in float CloudType)
    {
        CloudType = remap(CloudType, 0, 1, 0, 1);
        RelativeHeight = clamp(RelativeHeight, 0.0,1.0);
        
        float Cumulus = max(0.0, remap(RelativeHeight, 0.01, 0.3, 0.0, 1.0) * remap(RelativeHeight, 0.6, 0.95, 1.0, 0.0));
        float Stratocumulus = max(0.0, remap(RelativeHeight, 0.0, 0.25, 0.0, 1.0) * remap(RelativeHeight, 0.3, 0.65, 1.0, 0.0));
        float Stratus = max(0.0, remap(RelativeHeight, 0, 0.1, 0.0, 1.0) * remap(RelativeHeight, 0.2, 0.3, 1.0, 0.0));
        
        float a = lerp(Stratus, Stratocumulus, clamp(CloudType * 2.0, 0.0, 1.0));
        float b = lerp(Stratocumulus, Cumulus, clamp((CloudType - 0.5) * 2.0, 0.0, 1.0));
        return lerp(a, b, CloudType);
    }
    // 采样低频噪声，硬件3D采样, Talk中的原型方案
    float SampleLowFrequencyNoises(float3 p, float mip_level)
    {
        float4 low_frequency_noises = SAMPLE_TEXTURE3D_LOD(cloudShape,  sampler_cloudShape, p*_shapeTiling, 0);

        // 从低频 Worley 噪声中构建一个 fBm，可用于为低频 Perlin-Worley 噪声添加细节
        // 这主要用于改善连贯的Perlin-worley噪声，使其产生孤立的岛状云
        float low_freq_fBm = ( low_frequency_noises.g * 0.625 ) + ( low_frequency_noises.b * 0.25 ) + ( low_frequency_noises.a * 0.125 );

        // 通过使用由 Worley 噪声构成的低频 FBM 对其进行膨胀来定义基本云形状。
        float base_cloud = remap( low_frequency_noises.r, -(1-low_freq_fBm), 1.0, 0.0, 1.0 );

        return base_cloud;
    }
    // Talk中的原型，硬件3D采样高频噪声
    float SampleHighFrequencyNoises(float3 p, float mip_level)
    {
        // 参考SIG 2015中的做法，使用Curl Noise为云增加一些湍流感
        //float2 curl_noise = tex2Dlod(tCloud2DNoiseTexture,  sCloud2DNoiseSampler,  float4 (float2(p.x, p.y), 0.0, 1.0).rg;
        //p.xy += curl_noise.rg * (1.0 - height_fraction) * 200.0;

        // 采样高频噪声
        float3 high_frequency_noises = SAMPLE_TEXTURE3D_LOD(cloudDetail,  sampler_cloudDetail,  p * _detailTiling, 0).rgb;

        // 构建高频worley噪声FBM
        float high_freq_fBm = ( high_frequency_noises.r * 0.625 ) + ( high_frequency_noises.g * 0.25 ) + ( high_frequency_noises.b * 0.125 );

        return high_freq_fBm;
    }
    float3 SampleWeather(float3 pos)
    {
        float4 boundsCentre = (boundsMax + boundsMin) * 0.5;
        float3 size = boundsMax - boundsMin;
        
        float2 uv = (size.xz * 0.5f + (pos.xz - boundsCentre.xz) ) /max(size.x,size.z);
        
        float3 weatherData = SAMPLE_TEXTURE2D(weatherMap,sampler_weatherMap,uv);
        return weatherData;
    }

    float SampleCloudDensity(float3 p, float3 weather_data, int mipLevel, bool doCheaply)
    { 

        float height_fraction = GetHeightFractionForPoint(p, float2(boundsMin.y,boundsMax.y));
        
        float3 wind_direction = float3(1,0,0);
        float cloud_speed = 10; // PPT中给的是10

        // cloud_top offset ，用于偏移高处受风力影响的程度
        float cloud_top_offset = 500.0;

        // 为采样位置增加高度梯度，风向影响;
        //p += height_fraction * wind_direction * cloud_top_offset;

        // 增加一些沿着风向的时间偏移
        //p += (wind_direction + float3(0.0, 0.1, 0.0)) * _Time.y * cloud_speed;

        // 这个函数封装了Perlin-Worley Noise与3层频率递增Worley Noise的采样与FBM；
        float base_cloud = SampleLowFrequencyNoises(p, mipLevel);
        //return base_cloud;
        // 依据高度梯度模型计算云属，按照SIG 2015的设定，云图的B通道存储了CloudType
        float density_height_gradient = GetDensityHeightGradientForPoint(height_fraction, weather_data.b);

        // 现在已经可以通过高度-密度模型塑造云的大型了
        float noise = base_cloud * density_height_gradient;
//return noise;
        // 按照原文设定，云图的R通道存储了天穹内的覆盖范围，随着Coverage逐渐增大天穹逐渐被云层覆盖
        float coverage = weather_data.r;

        //return noise*coverage;

        // 这次Remap用于将云向高度方向拉伸以模拟2015年失败的“积雨云”，它们有着垂直的塔状分布
        // 这虽然没“铁砧”形自然，但看去来很酷
        float cloud_coverage = pow(coverage, remap(height_fraction, 0.7, 0.8, 1.0, lerp(1.0, 0.5, 0.5)));
        
        // 对最终的noise与Coverage做Remap,也可以在PPT中找到对应说明
        float cloud_with_coverage  = remap(noise, cloud_coverage, 1.0, 0.0, 1.0); 

        cloud_with_coverage *= cloud_coverage;
        float final_cloud = cloud_with_coverage;

        if(doCheaply)
        {   
            // 采样高频噪声并构建FBM
            float high_freq_fBm = SampleHighFrequencyNoises(p, mipLevel);

            // 获取height_fraction用于在高度上混合噪声
            float height_fraction  = GetHeightFractionForPoint(p, float2(boundsMin.y,boundsMax.y));

            // 依据高度从纤细的形状过渡到波浪形状
            float high_freq_noise_modifier = lerp(high_freq_fBm, 1.0 - high_freq_fBm, saturate(height_fraction * 10.0));
            
            // 根据SIG 2017中的做法，使用Remap将扭曲的高频worley噪声用于侵蚀基础云的形状以塑造细节
            final_cloud = remap(cloud_with_coverage, saturate(high_freq_noise_modifier*0.5), 1.0, 0.0, 1.0);
        }
        
        return final_cloud;
    }
    // Beer定律
    float BeerLambert(float sampleDensity, float precipitation)
    {
        return exp(sampleDensity * precipitation);
    }

    // SIG 2015中的“糖粉效果”
    float PowderEffect(float sampleDensity, float cos_angle)
    {
        float powd = 1.0 - exp(-sampleDensity * 2.0);
        return lerp(1.0, powd, saturate((-cos_angle * 0.5) + 0.5)); // [-1,1]->[0,1]
    }

    // HG方程
    float HenyeyGreenstein(float cos_angle, float eccentricity)
    {
        float g2 = eccentricity * eccentricity;
        return ((1.0 - g2) / pow((1.0 + g2 - 2.0 * g2 * cos_angle), 1.5)) / (4.0 * PI);
    }
    float hg(float a, float g) {
        float g2 = g * g;
        return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
    }

    float phase(float a) {
        float blend = .5;
        float hgBlend = hg(a, _phaseParams.x) * (1 - blend) + hg(a, -_phaseParams.y) * blend;
        return _phaseParams.z + hgBlend * _phaseParams.w;
    }

    float LightEnergy(float CosTheta, float densitySample, float eccentricity, float precipitation)
    {
        return 2 * BeerLambert(densitySample, precipitation) * PowderEffect(densitySample, CosTheta) * phase(CosTheta);
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
    
    float3 lightMarch(float3 position,float3 weather_data, float cosTheta, float sumDensity)
    {
        Light mainLight = GetMainLight();
        float3 lightDir = normalize(mainLight.direction);
        float dstInsideBox = rayBoxDst(boundsMax,boundsMax,position,1/lightDir).y;
        float stepSize = dstInsideBox/8;
        float totalDensity = 0;

        UNITY_LOOP
        for(int step = 0;step < 8;step++)
        {
            position +=lightDir*stepSize;
            if (sumDensity<0.3)
            {
                totalDensity+=SampleCloudDensity(position, weather_data, 0, false);
            }
            else
            {
                totalDensity+=SampleCloudDensity(position, weather_data, 0, true);
            }
        }
        float cloudColor = LightEnergy(cosTheta,totalDensity, 0.9999, 0.01);
        return cloudColor;
    }
    half4 Frag(Varyings input) : SV_Target
    {
        
        float2 UV = input.texcoord;
        half4 originColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, UV);

        // Sample the depth from the Camera depth texture.
        #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(UV);
        #else
            // Adjust Z to match NDC for OpenGL ([-1, 1])
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
        #endif

        // Reconstruct the world space positions.
        float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
        //return worldPos;
        float3 cameraPos = _WorldSpaceCameraPos;
        float3 worldViewDir = normalize(worldPos.xyz - cameraPos.xyz);

        float2 rayToContainerInfo = rayBoxDst(boundsMin, boundsMax,cameraPos,1.0/worldViewDir);
        float dstToBox = rayToContainerInfo.x;
        float dstInsideBox = rayToContainerInfo.y;
        
        float cameraToObjectDistance = length(worldPos - cameraPos);
        float dstLimit = min(cameraToObjectDistance-dstToBox,dstInsideBox);
        
        //与云容器的交汇点
        float3 entryPoint = cameraPos + worldViewDir * dstToBox;

        //添加抖动
        float blueNoise=SAMPLE_TEXTURE2D(_BlueNoise,sampler_BlueNoise,input.texcoord*_BlueNoiseST.xy).r;

        //向灯光方向的散射更强一些
        Light light = GetMainLight();
        float cosAngle = dot(worldViewDir, light.direction.xyz);

        float dstTravelled = blueNoise*_rayOffsetStrength;
        float3 rayPos = entryPoint;
        
        float cloud_test = 1.0;
        float cloud_lighting = 0.0;
        int zero_density_sample_count = 0;
        float sampled_density_previous = -1.0;
        float alpha = 0.0;

        float minus =1;
        //开始步进主循环
        if(dstTravelled<dstLimit)
        {
            UNITY_LOOP
            for (int i = 0; i <512 && dstTravelled<dstLimit; i++)
            {
                dstTravelled+=STEP_SIZE;
                float3 weather_data=SampleWeather(rayPos);
                // cloud_test为0表示在云外，>0表示在云内，云内进行全采样，doCheap设置为false
                float sampled_density = SampleCloudDensity(rayPos, weather_data, 0, false);
                // 累加密度
                alpha += sampled_density;
                // if (sampled_density>0)
                //     return sampled_density;
                minus*=exp(-sampled_density*STEP_SIZE);
                // 圆锥采样并计算光照
                cloud_lighting += lightMarch(rayPos,weather_data,cosAngle, alpha)*minus*STEP_SIZE*sampled_density;    
                // 步进计数
                rayPos = entryPoint + worldViewDir*dstTravelled;
                
            }
        }
        //return cloud_lighting;
        //originColor.rgb*=1-alpha;
        originColor.rbg+=cloud_lighting;
        return originColor;
    }
    
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        Pass
        {
            Name "ColorTintPass"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}