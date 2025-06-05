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

    TEXTURE3D(cloudShape);
    SAMPLER(sampler_cloudShape);
    TEXTURE3D(cloudSDF);
    SAMPLER(sampler_cloudSDF);
    TEXTURE3D(noise);
    SAMPLER(sampler_noise);
    TEXTURE2D(uvNoise);
    SAMPLER(sampler_uvNoise);
    
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
        
        // 对应原始代码: int _8 = gl_VertexIndex;
        uint vertexIndex = INPUT.vertexID;
        
        bool isVertex0 = (vertexIndex == 0);
        float x = isVertex0 ? -3.0 : 1.0;

        bool isVertex2 = (vertexIndex == 2);
        float y = isVertex2 ? 3.0 : -1.0;

        output.positionHCS = float4(x, y, 0.0, 1.0);

        output.texcoord = (output.positionHCS.xy * 0.5) + 0.5;

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
    half4 Frag(MyVaryings input) : SV_Target
    {
        float3 viewRay = input.viewRay.xyz;
        float inverseViewRaySize = 1/sqrt(dot(viewRay.xyz,viewRay.xyz));
        viewRay*=inverseViewRaySize;
        //return half4(viewRay,1);
        float minT = 0.1/inverseViewRaySize;
        float4 depth = GATHER_RED_TEXTURE2D(_BlitTexture,sampler_PointClamp, input.texcoord);
        float minDepth = max(min(min(depth.x,depth.y),min(depth.z,depth.w)),1.0000000116860974230803549289703e-07);
        float _596 = 1.0 / minDepth;
        float maxT = ((0.1 * _596) / inverseViewRaySize) + 10.0;

        float2 rayToContainerInfo = rayBoxDst(_boundMin, _boundMax,_WorldSpaceCameraPos,1.0/viewRay,minT,maxT);

        // float distance=rayToContainerInfo.x;
        // float stepSize = rayToContainerInfo.y/128;
        // half4 finalColor = half4(0,0,0,0);
        // UNITY_LOOP
        // for (int i=0;i<128;++i)
        // {
        //     if (distance<rayToContainerInfo.x+rayToContainerInfo.y)
        //     {
        //         float3 rayPos = _WorldSpaceCameraPos+viewRay*distance;
        //         float3 uvw = floor((rayPos-_boundMin)/8)/(_boundMax-_boundMin)*float3(8,8,8);
        //         float2 border = SAMPLE_TEXTURE3D(cloudShape,sampler_cloudShape,uvw).xy;
        //         if (border.x+border.y==0)
        //         {
        //             distance+=stepSize;
        //         }else
        //         {
        //             finalColor.xy = border*10;
        //             break;
        //         }
        //     }
        // }
        // return finalColor;


        
        bool _671=false;
        float _672;
        bool complete = false;
        half4 cloudColor=half4(0,0,0,0);
        do
        {
            float distance = rayToContainerInfo.x;
            float stepSize;
            float3 boundSize=_boundMax-_boundMin;
            UNITY_LOOP
            while(distance < rayToContainerInfo.y)
            {
                do
                {
                    float3 rayPos = _WorldSpaceCameraPos + viewRay * distance;
                    float2 uv1 = float2((0.093249998986721038818359375 * 309.88) + (0.12999999523162841796875 * (rayPos.y + (0.20000000298023223876953125 * rayPos.x))), (0.18649999797344207763671875 * 309.88) + (0.064999997615814208984375 * ((-rayPos.z) + (0.20000000298023223876953125 * rayPos.y))));
                    float2 uv2 = float2((0.3729999959468841552734375 * 309.88) + (0.0324999988079071044921875 * ((-rayPos.x) + (0.20000000298023223876953125 * rayPos.z))), (1.3519999980926513671875 * 309.88) + (rayPos.y * 0.20000000298023223876953125));
                    uv1 *= 0.159155070781707763671875;
                    float2 _897 = SAMPLE_TEXTURE2D(uvNoise, sampler_uvNoise, uv1).xy;
                    float2 _812 = _897;
                    uv2 *= 0.159155070781707763671875;
                    float2 _905 = SAMPLE_TEXTURE2D(uvNoise, sampler_uvNoise, uv2).xy;
                    float2 _814 = _905;
                    float _816 = ((0.25 * _812.x) + (0.5 * _812.y)) + (1.0 * _814.x);
                    rayPos.y += ((0.2 * (_816 - 1.0)) * 2.0);
                    float _817 = ((-0.2) * 0.300000011920928955078125) * _814.y;
  
                    float _728 = _817;
                    float3 _730 = (rayPos - _boundMin) * (1/16.0);
                    float3 _731 = floor(_730);
                    float3 uvw = (_731 + float3(0.5,0.5,0.5)) * (float3(16,16,16) / boundSize);
                    float2 cloudBorder = SAMPLE_TEXTURE3D_LOD(cloudShape,sampler_cloudShape,uvw,0).xy;
                    if(cloudBorder.x+cloudBorder.y==0)
                    {
                        float value = SAMPLE_TEXTURE2D_LOD(cloudSDF,sampler_cloudSDF,uvw,0).x*0.800000011920928955078125;
                        if (value==0)complete=true;
                        //distance+=value;
                        stepSize = value;
                        break;
                    }
                    // else{
                    //     //cloudColor=half4(cloudBorder.xy,0,0)*10;
                    //     complete = true;
                    //     break;
                    // }
                    float3 _735 = ((_730 - _731) * (1.0 - 0.2)) + float3(0.5 * 0.2,0.5*0.2,0.5*0.2);
                    float3 _736 = float3(((cloudBorder * 255.0) + _735.xy) * 0.0068, _735.z);
                    _728 += (SAMPLE_TEXTURE2D(noise, sampler_noise, _736).x * 5.0);
                    stepSize = _728;
                    break;
                }while (false);
                float _674 = stepSize;
                distance += _674;
                float _676 = abs(distance) * input.viewRay.w;
                _676 += (2.5 * 1.39999997615814208984375);
                 if (_674 <= _676)
                 {
                     float _677 = distance - _676;
                     float _913 = max(_677, 0.0);
                     _671 = true;
                     _672 = _913;
                     break;
                 }
                // if (complete)
                //     break;
            }
            if (_671)break;
            _671=true;
            _672 = distance;
        }while (false);
        float _498 = _672;
        bool _511 = _498 < rayToContainerInfo.y;
        float _518;
         if (_511)
         {
             float _528 = max((_498*inverseViewRaySize) / 0.1, 1.0000000116860974230803549289703e-07);
             float _917 = 1.0 / _528;
             _518 = _917;
         }
         else
         {
             _518 = 0.0;
         }
		return _518;
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
            #pragma vertex MyVert
            #pragma fragment Frag
            #pragma target 4.5
            ENDHLSL
        }
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
    }
}
