Shader "Unlit/SkyCloud"
{
    HLSLINCLUDE
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    
    float3 _boundMin;
    float3 _boundMax;
    float3 _cameraRight;
    float3 _cameraUp;
    float3 _bottomLeftPoint;
    float3 _cameraForward;
    float3 _lightColor1;
    float3 _lightColor2;
    float4 _LightParams;
    float3 _cloudEmission;
    float4 _NoiseParams1;
    float4 _NoiseParams2;
    float3 _AtmosphereCenter;

    TEXTURE2D(_depthTexture);
    TEXTURE3D(_cloudBorder);
    SAMPLER(sampler_cloudBorder);
    TEXTURE3D(_cloudSDF);
    SAMPLER(sampler_cloudSDF);
    TEXTURE3D(_cloudDetail);
    SAMPLER(sampler_cloudDetail);
    TEXTURE2D(_sinTexture);
    SAMPLER(sampler_sinTexture);
    TEXTURE3D(_worlyNoise);
    SAMPLER(sampler_worlyNoise);
    TEXTURE3D(_cloudLightInfo1);
    SAMPLER(sampler_cloudLightInfo1);
    TEXTURE3D(_cloudLightInfo2);
    SAMPLER(sampler_cloudLightInfo2);
    TEXTURE3D(_cloudLightInfo3);
    SAMPLER(sampler_cloudLightInfo3);
    struct MyAttributes
    {
        uint vertexID : SV_VertexID;
    };
    struct MyVaryings
    {
        float4 positionHCS : SV_POSITION; 
        float2 texcoord : TEXCOORD0;      
        float4 viewRay : TEXCOORD1;       
    };
    MyVaryings MyVert(MyAttributes INPUT)
    {
        MyVaryings output;
        // uint vertexIndex = INPUT.vertexID;
        //
        // bool isVertex0 = (vertexIndex == 0);
        // float x = isVertex0 ? -3.0 : 1.0;
        //
        // bool isVertex2 = (vertexIndex == 2);
        // float y = isVertex2 ? 3.0 : -1.0;
        //
        // output.positionHCS = float4(x, y, 0.0, 1.0);
        //
        // output.texcoord = (output.positionHCS.xy * 0.5) + 0.5;
        output.positionHCS = GetFullScreenTriangleVertexPosition(INPUT.vertexID);
        output.texcoord  = GetFullScreenTriangleTexCoord(INPUT.vertexID);

        float2 uv = output.texcoord;
        
        float3 viewDirection = _bottomLeftPoint + (_cameraRight * uv.x) + (_cameraUp * uv.y);
        output.viewRay.xyz = viewDirection;
        
        float diagonalLength = length(_cameraRight + _cameraUp);
        float screenDiagonal = length(_ScreenParams.xy);
        output.viewRay.w = (0.5 * diagonalLength) / screenDiagonal;
        
        return output;
    }
    // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
    float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir, float minT, float maxT) {
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

        float dstToBox = max(minT, dstA);
        float dstInsideBox = min(maxT, dstB);
        return float2(dstToBox, dstInsideBox);
    }
    float2 sineLookup( float2 uv )
    {
        uv *= 1.0 / 6.28318;
        return SAMPLE_TEXTURE2D(_sinTexture, sampler_sinTexture, uv ).xy;
    }
    float calculateUndulation(inout float3 samplePos )
    {
        float2 s1 = float2( .25 * 0.373 * _Time.y + 1.0 * 0.13 * ( samplePos.y + 0.2 * samplePos.x ),
                                0.5 * 0.373 * _Time.y + 0.5 * 0.13 * (-samplePos.z + 0.2 * samplePos.y) );
        
        float2 s2 = float2( 1.0 * 0.373 * _Time.y + .25 * 0.13 * (-samplePos.x + 0.2 * samplePos.z),
                                 1.352 * _Time.y + samplePos.y * 0.2 );
        
        float2 sine1 = sineLookup( s1 );
        float2 sine2 = sineLookup( s2 );
        float undulate = 0.25 * sine1.x + 0.5 * sine1.y + 1.0 * sine2.x;

        samplePos.y += 0.2 * ( undulate - 1.0 ) * 2.0;

        return -0.2 * 0.3 * sine2.y;
    }
    // 计算光线与包围盒相交
    float2 rayBoxIntersection(float3 rayPos, float3 rayDir, float3 boxMin, float3 boxSize) {
        float3 boundsMin = boxMin + float3(4,4,4)  - rayPos;
        float3 boundsMax = (boundsMin + boxSize) - 2 * float3(4,4,4);
        
        float3 invRayDir = float3(1.0,1.0,1.0) / rayDir;
        float3 t1 = boundsMin * invRayDir;
        float3 t2 = boundsMax * invRayDir;
        
        float3 tMin = min(t1, t2);
        float3 tMax = max(t1, t2);
        
        float tNear = max(tMin.x, max(tMin.y, tMin.z));
        float tFar = min(tMax.x, min(tMax.y, tMax.z));
        
        return float2(tNear, tFar);
    }
    // 采样云密度
    float sampleCloudDensity(float3 worldPos) {
        // 添加风力扰动
        float3 pos = worldPos;
        
        float windDensity = calculateUndulation(pos);
        
        // 转换为纹理坐标
        float3 texCoords = (pos - _boundMin) * 0.0625;
        float3 floorCoords = floor(texCoords);
        float3 shapeUV = (floorCoords + float3(0.5,0.5,0.5)) * (float3(16.0,16.0,16.0) / (_boundMax-_boundMin));
        
        // 采样形状纹理
        float2 shapeData = SAMPLE_TEXTURE3D(_cloudBorder,sampler_cloudBorder, shapeUV).xy;
        
        // 如果没有形状数据，返回细节纹理
        if ((shapeData.x + shapeData.y) == 0.0) {
            return SAMPLE_TEXTURE3D(_cloudSDF,sampler_cloudSDF, shapeUV).x * 0.8;
        }
        
        // 计算最终采样坐标
        float3 fracCoords = ((texCoords - floorCoords) * (1.0 - 0.2)) + float3(0.5,0.5,0.5)* 0.2;
        float3 finalUV = float3(((shapeData * 255.0) + fracCoords.xy) / 148.0, fracCoords.z);        

        // 采样最终密度并添加风力效果
        float noiseValue = SAMPLE_TEXTURE3D(_cloudDetail,sampler_cloudDetail, finalUV).x;
        windDensity = noiseValue * 5;
        
        return windDensity;
    }
    half4 Frag(MyVaryings input) : SV_Target
    {
        //标准化光线方向
        float4 viewRay = float4(input.viewRay.xyz, input.viewRay.w);
        float inverseRayLength = 1.0/sqrt(dot(viewRay.xyz, viewRay.xyz));
        viewRay.xyz = viewRay.xyz * inverseRayLength;
    
        #ifdef _HIGH_RES_RENDER
            float nearDepth = max(SAMPLE_TEXTURE2D(_BlitTexture,sampler_PointClamp, input.texcoord).r,1.0e-07);
            float nearDistance = _ProjectionParams.y / nearDepth/inverseRayLength;
            float farDepth = max(SAMPLE_TEXTURE2D(_depthTexture,sampler_PointClamp, input.texcoord).r,1.0e-07);
            float farDistance = _ProjectionParams.y / farDepth/inverseRayLength + 10.0;
        #else
            // 计算步进参数
            float nearDistance = _ProjectionParams.y / inverseRayLength;

            // 采样深度缓冲区
            float4 depthSample = GATHER_RED_TEXTURE2D(_BlitTexture,sampler_PointClamp, input.texcoord);
            float minDepth = min(min(depthSample.x, depthSample.y), min(depthSample.z, depthSample.w));
            float clampedDepth = max(minDepth, 1.0e-07);
            float depthScale = 1.0 / clampedDepth;
            float farDistance = ((_ProjectionParams.y * depthScale) / inverseRayLength) + 10.0;
        #endif
        
        // 计算光线与云边界的相交
        float3 rayStart = _WorldSpaceCameraPos;
        float2 intersection = rayBoxIntersection(rayStart, viewRay.xyz, _boundMin, _boundMax-_boundMin);
        
        float startDistance = max(nearDistance, intersection.x);
        float endDistance = min(farDistance, intersection.y);

        // 光线步进
        bool hitCloud = false;
        float finalDistance = 0.0;
        
        if (startDistance < endDistance) {
            float currentDistance = startDistance;
            UNITY_LOOP
            while (currentDistance < endDistance) {
                float3 currentPos = rayStart + (viewRay.xyz * currentDistance);
                float density = sampleCloudDensity(currentPos);
                
                currentDistance += density;
                
                // 检查步长是否过小（表示击中云层）
                float stepThreshold = abs(currentDistance) * viewRay.w;
                stepThreshold += (_NoiseParams1.w * 1.4);
                
                if (density <= stepThreshold) {
                    float adjustedDistance = currentDistance - stepThreshold;
                    finalDistance = max(adjustedDistance, 0.0);
                    hitCloud = true;
                    break;
                }
            }
            
            if (!hitCloud) {
                finalDistance = currentDistance;
            }
        }
        
        // 计算最终透明度
        float alpha = 0.0;
        if (hitCloud && finalDistance < endDistance) {
            float normalizedDistance = max((finalDistance * inverseRayLength) / _ProjectionParams.y, 0.00000001);
            alpha = 1.0 / normalizedDistance;
        }
        
        return alpha;
    }
    float DownsampleDepth(Varyings input) : SV_Target
    {
        float2 texelSize = 0.5 * _CameraDepthTexture_TexelSize.xy;
        float2 taps[4]={float2(input.texcoord+float2(-2,0)*texelSize),
                        float2(input.texcoord+float2(2,0)*texelSize),
                        float2(input.texcoord+float2(0,2)*texelSize),
                        float2(input.texcoord+float2(0,-2)*texelSize)};
        // 使用textureGather采样5个位置
        // textureGather返回2x2像素块的指定通道值
        float4 center = GATHER_RED_TEXTURE2D(_CameraDepthTexture,sampler_PointClamp, input.texcoord); // 偏移(0,0)
        float4 left = GATHER_RED_TEXTURE2D(_CameraDepthTexture,sampler_PointClamp, taps[0]); // 偏移(-2,0)
        float4 right = GATHER_RED_TEXTURE2D(_CameraDepthTexture,sampler_PointClamp, taps[1]); // 偏移(2,0)
        float4 up = GATHER_RED_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp,taps[2]); // 偏移(0,2)
        float4 down = GATHER_RED_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp,taps[3]); // 偏移(0,-2)
        
        // 找到所有采样值中的最小值
        float4 minValues = min(min(min(center, left), min(right, up)), down);
        
        // 找到4个分量中的最小值
        float finalMin = min(min(minValues.x, minValues.y), min(minValues.z, minValues.w));
        return finalMin;
    }

    // 生成伪随机数
    float2 hash32(float3 p) {
        float3 noise = frac(p * float3(443.897003173828125, 441.423004150390625, 437.19500732421875));
        float vaule = dot(noise, noise.yzx + float3(19.19,19.19,19.19));
        noise += float3(vaule,vaule,vaule);
        return frac((noise.xx + noise.yz) * noise.zy);
    }

    float hash31(float p)
    {
        float3 noise = frac(p * 443.897491455078125);
        float vaule = dot(noise,noise.yzx+float3(19.19,19.19,19.19));
        noise += float3(vaule,vaule,vaule);
        return frac((noise.x+noise.y)*noise.z);
    }

        // 计算云的步进大小
    float calculateStepSize(float3 worldPos, float windEffect) {
        // 将世界空间转换为网格坐标 (16x16x16)
        float3 gridPos = (worldPos - _boundMin) * 0.0625;
        float3 gridCell = floor(gridPos);
        float3 cellCenter = (gridCell + float3(0.5,0.5,0.5)) * (float3(16.0,16.0,16.0) / (_boundMax-_boundMin));
        
        float2 sdfData = SAMPLE_TEXTURE3D(_cloudBorder,sampler_cloudBorder, cellCenter).xy;
        
        // 如果不在云区域，使用SDF快速步进
        if ((sdfData.x + sdfData.y) == 0.0) {
            return SAMPLE_TEXTURE3D(_cloudSDF,sampler_cloudSDF, cellCenter).x * 0.8;
        }
        
        // 在云区域内，计算详细密度
        float3 cellOffset = ((gridPos - gridCell) * (1.0 - 0.2)) + float3(0.5,0.5,0.5)*0.2;
        float3 noiseCoord = float3(((sdfData * 255.0) + cellOffset.xy) / 148.0, cellOffset.z);
        windEffect += SAMPLE_TEXTURE3D(_cloudDetail,sampler_cloudDetail, noiseCoord).x * 5.0;
        
        //光遇这里有一步来计算角色和云的交互，这里先不做
        float cloudCoverage = 0.0;
        
        // 添加Worley噪声细节
        float scale = 0.5;
        float3 worleyCoord = (((worldPos - _NoiseParams2.xyz) * scale) / 64.0) * 6.0;//这里的_boundMin是错误的
        float worleyNoise = _NoiseParams1.w * SAMPLE_TEXTURE3D(_worlyNoise,sampler_worlyNoise, worleyCoord).x;
        worleyNoise *= (1.0 + (cloudCoverage / 1.0));
        
        return (windEffect - worleyNoise) - cloudCoverage;
    }

    //计算云的光照
    half3 calculateLightScattering(float2 screenPos,float3 cloudPosition, float3 viewRay, float distance) {

        //Light light = GetMainLight();
        float3 lightDir = float3(0.127,-0.682,-0.721);
        float lightDotView = dot(viewRay,-lightDir);

        // 计算基础Worley噪声
        float baseScale = 0.47 * 0.5;
        float3 baseWorleyCoord = (((cloudPosition - _NoiseParams2.xyz) * baseScale) / 64.0) * 6.0;//这里的_boundMin是错误的
        float baseWorley = _NoiseParams1.w * SAMPLE_TEXTURE3D(_worlyNoise,sampler_worlyNoise, baseWorleyCoord).x;
        float lightIntensity = 3.0 + (baseWorley * 2.0);//这里是lightIntensity吗？
        
        // 光线步进计算阴影
        float shadowAccumulation = 0.0;
        float stepScale = 1.0;
        float stepDistance = 0.3125;//刚刚好是1/3.2,这个数值刚刚好是16/5也就是更细致划分体素时，体素的大小

        for (int i = 0; i < 5; i++) {
            float3 lightSamplePos = cloudPosition - (lightDir * stepDistance);
            float lightWindEffect = calculateUndulation(lightSamplePos);
            float lightStepSize = calculateStepSize(lightSamplePos, lightWindEffect);
            
            float shadowContribution = 0.05 - ((0.35 * stepScale) * lightStepSize);
            shadowAccumulation += (abs(_LightParams.w) * max(shadowContribution, 0.0));//这个0.1是什么
            
            stepDistance += stepDistance;
            stepScale = 2.0;
        }
        //shadowAccumulation =0;

        // 获取云的材质属性
        float3 gridPos = (cloudPosition - _boundMin) * 0.0625;
        float3 gridCell = floor(gridPos);
        float3 cellCenter = (gridCell + float3(0.5,0.5,0.5)) * (float3(16.0,16.0,16.0) / (_boundMax-_boundMin));
        float2 cloudShape = SAMPLE_TEXTURE3D(_cloudBorder,sampler_cloudBorder, cellCenter).xy;
        
        float3 cloudColor = float3(1.0,1.0,1.0);
        float density = 1.0;
        float roughness = 1.0;
        float emission = 0.0;
        float detailMask = 1.0;

        if ((cloudShape.x + cloudShape.y) != 0.0) {
            float3 cellOffset = ((gridPos - gridCell) * (1.0 - 1.0/3.0)) + float3(0.5,0.5,0.5) * 1.0/3.0;
            float3 detailCoord = float3(((cloudShape * 255.0) + cellOffset.xy) / 148.0, cellOffset.z);
            
            detailMask = SAMPLE_TEXTURE3D(_cloudLightInfo1,sampler_cloudLightInfo1, detailCoord).x;
            float4 detail2 = SAMPLE_TEXTURE3D(_cloudLightInfo2,sampler_cloudLightInfo2, detailCoord);
            float4 detail3 = SAMPLE_TEXTURE3D(_cloudLightInfo3,sampler_cloudLightInfo3, detailCoord);
            
            cloudColor = (detail2.xyz * detail2.xyz) / detail2.w;
            density = detail3.x;
            roughness = detail3.y * detail3.y;
            emission = detail3.w * exp2((255.0 * detail3.z) - 128.0);
        }
        // 应用距离和噪声调制
        float distanceScale = 3.7 * 0.5;
        float3 distanceWorleyCoord = (((cloudPosition - _NoiseParams2.xyz) * distanceScale) / 64.0) * 6.0;
        float distanceWorley = SAMPLE_TEXTURE3D(_worlyNoise,sampler_worlyNoise, distanceWorleyCoord).x;
        
        float luminance = dot(cloudColor, float3(0.2126, 0.7152, 0.0722));
        cloudColor = lerp(cloudColor, luminance, float(_LightParams.w < 0.0));//这里的0.1表示什么意思
        
        float densityModulation = 1.0 + ((distanceWorley - 0.5) / (1.5 + (distance * 0.03)));
        density = pow(density, densityModulation);

        // 计算最终颜色
        float3 baseColor = cloudColor * lerp(_lightColor2, _lightColor1, density);
        baseColor += (_cloudEmission * emission);

        //计算角色和云的交互
        float2 cloudCoverage = 0.0;

        float coverageEffect = 1.0 + (cloudCoverage.x / 1.0);
                
        // Worley噪声调制
        float finalScale = 1.0 * 0.5;
        float3 finalWorleyCoord = (((cloudPosition - _NoiseParams2.xyz) * finalScale) / 64.0) * 6.0;
        float finalWorley = SAMPLE_TEXTURE3D(_worlyNoise,sampler_worlyNoise, finalWorleyCoord).x;
        finalWorley = lerp(1.0, finalWorley, detailMask);
        shadowAccumulation *= detailMask;
        
        // 相位函数和散射计算
        float phaseG = lerp(0.75, 0.5 + (0.5 * finalWorley), 0.2 + (0.8 * roughness));
        float extinction = exp2((-0.65) * lightIntensity);
        float scattering = 5.0 * exp2(((-3.0) * (1.0 + extinction)) * (0.5 + (0.5 * finalWorley)));
        float shadowEffect = coverageEffect * exp2((-2.75) * sqrt(shadowAccumulation));
        
        // Henyey-Greenstein相位函数
        float phaseFunction = lerp(0.2, 0.005, roughness * shadowEffect);
        float hg = phaseFunction / (phaseFunction - (lightDotView - 1.0));
        hg /= (phaseFunction * log((2.0 / phaseFunction) + 1.0));
        float finalPhase = (5.0 * hg) * shadowEffect;
        
        // 组合最终光照
        float3 ambientLight = (baseColor * phaseG) * (1.0 + ((1.5 * extinction) * scattering) + (_lightColor1 * ((0.5 * density) * shadowEffect)));
        float3 directLight = (_lightColor1 * roughness) * ((shadowEffect * scattering) + ((0.5 + (0.5 * roughness)) * finalPhase));
        float3 finalColor = _lightColor1 * (0 + ambientLight);
        // 添加角色对云的影响
        finalColor += ((finalColor * float3(3.0, 0.4, 0.1)) * cloudCoverage.y);
        
        // // === 添加程序化噪声增强效果 ===
        // float cloudNoiseIntensity = 0.1; // 云朵噪声强度控制
        // if (cloudNoiseIntensity > 0.0)
        // {
        //     // 创建基于屏幕坐标和时间的3D采样位置
        //     float3 noiseSamplePos = float3(screenPos * 0.01, _Time.y * 0.1);
            
        //     // 多频率噪声生成矩阵 - 用于创建不同尺度的噪声
        //     float3x3 noiseMatrix = float3x3(
        //         0.003525462932884693, 0.008127314969897270, 0.016259258612990379,  // 低频噪声
        //         0.035004630684852600, 0.069701388478279114, 0.141719907522201538,  // 中频噪声
        //         0.311381936073303223, 0.620134234428405762, 1.243317127227783203   // 高频噪声
        //     );
            
        //     // 第一阶段：基础噪声计算
        //     float3 baseNoise = frac(mul(noiseMatrix, noiseSamplePos));
            
        //     // 第二阶段：使用哈希函数生成高质量伪随机数
        //     float3 cloudNoise = frac((baseNoise * 3571.0) * frac((baseNoise * 3571.0) * baseNoise));
            
        //     // 计算噪声出现的概率阈值（平方衰减确保稀疏分布）
        //     float noiseThreshold = (0.05 * cloudNoiseIntensity) * cloudNoiseIntensity;
            
        //     // 只有当随机值小于阈值时才应用噪声效果
        //     if (cloudNoise.z < noiseThreshold)
        //     {
        //         // 计算噪声贡献度，使用反向通道顺序增加变化
        //         float3 noiseContribution = ((cloudNoise.zyx + float3(0.5,0.5,0.5)) * 50.0) / max(4.0, distance * 0.01);
                
        //         // 应用噪声到最终颜色
        //         // 结合光照信息和散射效果
        //         float3 lightScattering = ambientLight * scattering;
        //         float3 directScattering = directLight * finalPhase;
        //         float3 combinedScattering = lightScattering + directScattering;
                
        //         finalColor += noiseContribution * combinedScattering * cloudNoiseIntensity;
        //     }
        // }
        return finalColor;
    }

    // ACES色调映射函数
    half3 ACESTonemapping(half3 color) {
        const float a = 2.51;
        const float b = 0.03;
        const float c = 2.43;
        const float d = 0.59;
        const float e = 0.14;
        
        color = saturate((color * (a * color + b)) / (color * (c * color + d) + e));
        return color;
    }
    
    // Reinhard色调映射函数（备选）
    half3 ReinhardTonemapping(half3 color) {
        return color / (1.0 + color);
    }
    
    // 色调映射片段着色器
    half4 ToneMappingFrag(Varyings input) : SV_Target
    {
        // 采样HDR颜色
        half4 hdrColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, input.texcoord);
        
        // 应用ACES色调映射
        half3 ldrColor = ACESTonemapping(hdrColor.rgb);
        
        // 可选：添加伽马校正
        ldrColor = pow(ldrColor, 4.2);
        
        return half4(ldrColor, hdrColor.a);
    }

    half4 RenderCloud(MyVaryings input) : SV_Target
    {
        //标准化光线方向
        float4 viewRay = float4(input.viewRay.xyz, input.viewRay.w);
        float inverseRayLength = 1.0/sqrt(dot(viewRay.xyz, viewRay.xyz));
        viewRay.xyz = viewRay.xyz * inverseRayLength;

        // 取得深度范围
        float nearDepth = max(SAMPLE_TEXTURE2D(_BlitTexture,sampler_PointClamp, input.texcoord).r,1.0e-07);
        float nearDistance = _ProjectionParams.y / nearDepth/inverseRayLength;
        float farDepth = max(SAMPLE_TEXTURE2D(_depthTexture,sampler_PointClamp, input.texcoord).r,1.0e-07);
        float farDistance = _ProjectionParams.y / farDepth/inverseRayLength + 10.0;

        //添加抖动以减少锯齿
        float3 noiseInput = float3(viewRay.x, viewRay.y + viewRay.z, _Time.y);
        float2 dither = hash32(noiseInput);
        float2 jitter = dither.xy * 0.05;

        float3 cameraRight = float3(_cameraRight.x, _cameraRight.y, _cameraRight.z);
        float3 cameraUp = float3(_cameraUp.x, _cameraUp.y, _cameraUp.z);
        float3 jitteredCameraPos = _WorldSpaceCameraPos + (cameraRight * jitter.x) + (cameraUp * jitter.y);

        // 添加随机偏移到起始距离
        float3 randomInput = float3(viewRay.x, viewRay.y + viewRay.z, _Time.y);
        float randomValues = hash31(randomInput);
        nearDistance += (randomValues * 0.25);

        // 计算光线与AABB的相交
        float2 intersection = rayBoxIntersection(jitteredCameraPos, viewRay.xyz, _boundMin, _boundMax-_boundMin);
        float2 rayRange = float2(max(nearDistance,intersection.x),min(farDistance,intersection.y));

        // 光线步进
        bool hitCloud = false;
        float finalDistance = 0.0;
        
        if (rayRange.x < rayRange.y) {
            float currentDistance = rayRange.x;
            UNITY_LOOP
            while (currentDistance < rayRange.y) {
                float3 currentPos = jitteredCameraPos + (viewRay.xyz * currentDistance);
                float windEffect = calculateUndulation(currentPos);
                float stepSize = calculateStepSize(currentPos,windEffect);
                
                currentDistance += stepSize;
                float minStep = abs(currentDistance) * viewRay.w;
                if (stepSize <= minStep) {
                    finalDistance = max(0.5,currentDistance);
                    hitCloud = true;
                    break;
                }
            }
            
            if (!hitCloud) {
                finalDistance = currentDistance;
            }
        }
        // 计算最终透明度
        float alpha = 0.0;
        if (hitCloud && finalDistance < rayRange.y) {
            float normalizedDistance = max((finalDistance * inverseRayLength) / _ProjectionParams.y, 0.00000001);
            alpha = 1.0 / normalizedDistance;
        }
        
        //return alpha*100;

        //计算最终颜色
        half3 finalColor = half3(0.0,0.0,0.0);
        if (hitCloud && finalDistance < rayRange.y) {
            float3 cloudPosition = jitteredCameraPos + (viewRay.xyz * finalDistance);
            finalColor = calculateLightScattering(input.positionHCS.xy, cloudPosition, viewRay.xyz, finalDistance);
        }

        // 大气散射效果
        if (hitCloud) {
            float sceneDepth = max(SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_PointClamp, input.texcoord).x, 1e-7);
            float sceneDistance = _ProjectionParams.y / sceneDepth;
            
            float3 atmosphereCenter = _AtmosphereCenter;
            float atmosphereRadius = 2.0;
            float2 atmosphereRange = float2(finalDistance * inverseRayLength, sceneDistance);
            
            // 计算大气层相交
            float3 toCenter = _WorldSpaceCameraPos - atmosphereCenter;
            float b = dot(viewRay.xyz, toCenter);
            float c = dot(toCenter, toCenter) - (atmosphereRadius * atmosphereRadius);
            float discriminant = sqrt(max(0.0, (b * b) - c));
            float t1_atm = (-b + discriminant);
            float t2_atm = (-b - discriminant);
            float2 atmosphereIntersection = float2(min(t1_atm, t2_atm), max(t1_atm, t2_atm));
            atmosphereIntersection = clamp(atmosphereIntersection, atmosphereRange.x, atmosphereRange.y);
            
            float atmosphereThickness = (atmosphereIntersection.y - atmosphereIntersection.x) * 0.3;
            
            // 应用大气散射
            float3 scatteredColor = finalColor + ((finalColor * float3(1.0, 0.24 + ((atmosphereThickness * atmosphereThickness * atmosphereThickness) * 0.15), 0.0625)) * atmosphereThickness * atmosphereThickness);
            finalColor = scatteredColor;
            return half4(finalColor,1.0);
        }
        return half4(finalColor,0.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Pass
        {
            ZTest Off ZWrite Off Cull Off
            Name "filter"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex Vert
            #pragma fragment DownsampleDepth
            #pragma target 4.5
            ENDHLSL
        }
        Pass
        {
            ZTest Off ZWrite Off Cull Off
            //Blend One One
            Name "VolumeCloudPass"

            HLSLPROGRAM
            #pragma shader_feature _HIGH_RES_RENDER
            #pragma enable_d3d11_debug_symbols
            #pragma vertex MyVert
            #pragma fragment Frag
            #pragma target 4.5
            ENDHLSL
        }
        Pass
        {
            ZTest Off ZWrite Off Cull Off
            Name "RenderCloud"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex MyVert
            #pragma fragment RenderCloud
            #pragma target 4.5
            ENDHLSL
        }
        Pass
        {
            ZTest Off ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            Name "ToneMapping"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex Vert
            #pragma fragment ToneMappingFrag
            #pragma target 4.5
            ENDHLSL
        }
    }
}
