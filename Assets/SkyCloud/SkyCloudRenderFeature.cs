using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;

public class SkyCloudRenderFeature : ScriptableRendererFeature
{
    class SkyCloudRenderPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private RTHandle _cloudDepthTextureDiv8;
        private RTHandle _cloudDepthTextureDiv4;
        private RTHandle _downSampleDepth;
        private RTHandle _cloudRenderTexture;
        private RTHandle _inputHandle;
        private SkyCloudVolume _volume;
        
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(nameof(SkyCloudRenderFeature));
        
        public void SetInput(RTHandle src)
        {
            // The Renderer Feature uses this variable to set the input RTHandle.
            _inputHandle = src;
        }
        public SkyCloudRenderPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _volume = VolumeManager.instance.stack.GetComponent<SkyCloudVolume>();
            if (_volume == null || !_volume.IsActive())
                return;
            var descCloudDepthDiv8=cameraTextureDescriptor;
            descCloudDepthDiv8.depthBufferBits = 0;
            descCloudDepthDiv8.msaaSamples = 1;
            descCloudDepthDiv8.width = cameraTextureDescriptor.width / 8;
            descCloudDepthDiv8.height = cameraTextureDescriptor.height / 8;
            descCloudDepthDiv8.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudDepthTextureDiv8, descCloudDepthDiv8, name: "cloudDepthTextureDiv8");

            var descCloudDepthDiv4=cameraTextureDescriptor;
            descCloudDepthDiv4.depthBufferBits = 0;
            descCloudDepthDiv4.msaaSamples = 1;
            descCloudDepthDiv4.width = cameraTextureDescriptor.width / 4;
            descCloudDepthDiv4.height = cameraTextureDescriptor.height / 4;
            descCloudDepthDiv4.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudDepthTextureDiv4, descCloudDepthDiv4, name: "cloudDepthTextureDiv4");
            
            var descDownSampleDepth=cameraTextureDescriptor;
            descDownSampleDepth.depthBufferBits = 0;
            descDownSampleDepth.msaaSamples = 1;
            descDownSampleDepth.width = cameraTextureDescriptor.width / 4;
            descDownSampleDepth.height = cameraTextureDescriptor.height / 4;
            descDownSampleDepth.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _downSampleDepth, descDownSampleDepth, name: "downSampleDepth");

            var descCloudRender=cameraTextureDescriptor;
            descCloudRender.depthBufferBits = 0;
            descCloudRender.msaaSamples = 1;
            descCloudRender.width = cameraTextureDescriptor.width;
            descCloudRender.height = cameraTextureDescriptor.height;
            descCloudRender.colorFormat = RenderTextureFormat.ARGBHalf;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudRenderTexture, descCloudRender, name: "cloudRenderTexture");
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (_volume == null || !_volume.IsActive())
                return;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            int screenWidth = descriptor.width;
            int screenHeight = descriptor.height;
            CommandBuffer cmd = CommandBufferPool.Get(nameof(SkyCloudRenderFeature));
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                _material.SetTexture("_cloudBorder",_volume.cloudBorder.value);
                _material.SetTexture("_sinTexture",_volume.sinTexture.value);
                _material.SetTexture("_cloudSDF",_volume.cloudSDF.value);
                _material.SetTexture("_cloudDetail",_volume.cloudDetail.value);
                _material.SetTexture("_worlyNoise",_volume.worlyNoise.value);
                _material.SetTexture("_cloudLightInfo1",_volume.cloudLightInfo1.value);
                _material.SetTexture("_cloudLightInfo2",_volume.cloudLightInfo2.value);
                _material.SetTexture("_cloudLightInfo3",_volume.cloudLightInfo3.value);
                _material.SetVector("_boundMin",_volume.boundMin.value);
                _material.SetVector("_boundMax",_volume.boundMax.value);
                _material.SetVector("_lightColor1",_volume.lightColor1.value);
                _material.SetVector("_lightColor2",_volume.lightColor2.value);
                _material.SetVector("_cloudEmission",_volume.cloudEmission.value);
                _material.SetVector("_NoiseParams1",_volume.NoiseParams1.value);
                // 使用随时间变化的偏移
                float time = Time.time;
                // 让xyzOffset始终朝一个方向移动（例如正对角线方向）
                Vector4 xyzOffset = new Vector4(
                    time * 7f,
                    time * 7f,
                    time * 5f,
                    0.0f
                );
                xyzOffset+=_volume.NoiseParams2.value;
                _material.SetVector("_NoiseParams2",xyzOffset);
                _material.SetVector("_AtmosphereCenter",_volume.atmosphereCenter.value);
                _material.SetVector("_LightParams",_volume.lightParams.value);
                var cam = renderingData.cameraData.camera;
                float nearPlane = 1.0f;//cam.nearClipPlane; 这里设置为1，和光遇的设置保持一致，不过这样设置的原因是什么？
                float fov = cam.fieldOfView * Mathf.Deg2Rad;
                float aspect = cam.aspect;
    
                // 计算近平面的半高和半宽
                float halfHeight = nearPlane * Mathf.Tan(fov * 0.5f);
                float halfWidth = halfHeight * aspect;
    
                // 获取相机的右向量和上向量
                Vector3 cameraRight = cam.transform.right;
                Vector3 cameraUp = cam.transform.up;
                Vector3 cameraForward = cam.transform.forward;
    
                // 构建近平面向量（从中心到边缘）
                Vector3 rightVector = cameraRight * halfWidth * 2;
                Vector3 upVector = cameraUp * halfHeight * 2;
                Vector3 bottomLeftPoint = nearPlane*cameraForward+(-rightVector-upVector)/2;
                _material.SetVector("_cameraRight",rightVector);
                _material.SetVector("_cameraUp",upVector);
                _material.SetVector("_bottomLeftPoint",bottomLeftPoint);
                RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, _downSampleDepth,_material,0);
                cmd.DisableShaderKeyword("_HIGH_RES_RENDER");
                Blitter.BlitCameraTexture(cmd, _downSampleDepth, _cloudDepthTextureDiv8,_material,1);
                cmd.EnableShaderKeyword("_HIGH_RES_RENDER");
                _material.SetTexture("_depthTexture",_downSampleDepth);
                Blitter.BlitCameraTexture(cmd, _cloudDepthTextureDiv8, _cloudDepthTextureDiv4,_material,1);
                Blitter.BlitCameraTexture(cmd, _cloudDepthTextureDiv4, _cloudRenderTexture,_material,2);
                Blitter.BlitCameraTexture(cmd, _cloudRenderTexture, cameraColorTarget,_material,3);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            //_tempTexture?.Release();
        }
        public void Dispose()
        {
            _inputHandle?.Release();
            //_tempTexture?.Release();
        }
    }


    [System.Serializable]
    public class Settings
    {
        public Shader shader;
    }

    public Settings settings;
    private SkyCloudRenderPass _scriptablePass;
    private Material _material;

    /// <inheritdoc/>
    public override void Create()
    {
        if (settings.shader != null)
        {
            _material = new Material(settings.shader);
            _scriptablePass = new SkyCloudRenderPass(_material);
        }
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null)
            return;
        renderer.EnqueuePass(_scriptablePass);
    }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        //这里传入的话，就只会影响到game视图，如果是在renderpass中的excute中通过excute得到的rendertarget，editor和game视图都会被影响到
        _scriptablePass.SetInput(renderingData.cameraData.renderer.cameraColorTargetHandle);
    }
    
    protected override void Dispose(bool disposing)
    {
        _scriptablePass?.Dispose();
        _scriptablePass = null;
    }
}
