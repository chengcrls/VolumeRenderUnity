using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SkyCloudRenderFeature : ScriptableRendererFeature
{
    class SkyCloudRenderPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private RTHandle _tempTexture;
        private RTHandle _depthTexture;
        private RTHandle _downSampleDepthTexture;
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
            var desc=cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
             desc.width = cameraTextureDescriptor.width / 8;
             desc.height = cameraTextureDescriptor.height / 8;
            desc.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempTexture, desc, name: "tempTexture");
            
            var descDepth=cameraTextureDescriptor;
            descDepth.depthBufferBits = 0;
            descDepth.msaaSamples = 1;
            descDepth.width = cameraTextureDescriptor.width / 4;
            descDepth.height = cameraTextureDescriptor.height / 4;
            descDepth.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _depthTexture, desc, name: "depthTexture");
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
                _material.SetTexture("cloudShape",_volume.cloudBorder.value);
                _material.SetTexture("uvNoise",_volume.uvNoise.value);
                _material.SetTexture("cloudSDF",_volume.cloudSDF.value);
                _material.SetTexture("noise",_volume.noise.value);
                _material.SetVector("_boundMin",_volume.boundMin.value);
                _material.SetVector("_boundMax",_volume.boundMax.value);
                var cam = renderingData.cameraData.camera;
                float nearPlane = 1.0f;//cam.nearClipPlane; 这里设置为1，和光遇的设置保持一致，不过这样设置的原因是什么？
                float fov = cam.fieldOfView * Mathf.Deg2Rad;
                float aspect = cam.aspect;
    
                // 计算近平面的半高和半宽
                float halfHeight = nearPlane * Mathf.Tan(fov * 0.5f);
                float halfWidth = halfHeight * aspect;
    
                // 获取相机的右向量和上向量
                Vector3 cameraRight = cam.transform.right;
                Vector3 cameraUp = -cam.transform.up;
                Vector3 cameraForward = cam.transform.forward;
    
                // 构建近平面向量（从中心到边缘）
                Vector3 rightVector = cameraRight * halfWidth * 2;
                Vector3 upVector = cameraUp * halfHeight * 2;
                Vector3 bottomLeftPoint = nearPlane*cameraForward+(-rightVector-upVector)/2;
                _material.SetVector("_cameraRight",rightVector);
                _material.SetVector("_cameraUp",upVector);
                _material.SetVector("_bottomLeftPoint",bottomLeftPoint);
                RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, _depthTexture,_material,1);
                Blitter.BlitCameraTexture(cmd, _depthTexture, _tempTexture,_material,0);
                Blitter.BlitCameraTexture(cmd, _tempTexture, cameraColorTarget);
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
