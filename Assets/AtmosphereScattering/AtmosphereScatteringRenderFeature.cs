using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AtmosphereScatteringRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("着色器设置")]
        public Shader atmosphereShader = null;
        
        [Header("渲染设置")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    [SerializeField] private Settings settings = new Settings();
    private AtmosphereScatteringRenderPass atmospherePass;
    private Material atmosphereMaterial;

    class AtmosphereScatteringRenderPass : ScriptableRenderPass
    {
        private Material material;
        private AtmosphereScatteringVolume volumeComponent;
        private RTHandle _inputHandle;
        private RTHandle _tempTexture;
        
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(nameof(AtmosphereScatteringRenderFeature));

        public AtmosphereScatteringRenderPass(Material mat)
        {
            material = mat;
        }

        public void SetInput(RTHandle src)
        {
            _inputHandle = src;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            volumeComponent = VolumeManager.instance.stack.GetComponent<AtmosphereScatteringVolume>();
            if (volumeComponent == null || !volumeComponent.IsActive())
                return;

            var desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempTexture, desc, name: "AtmosphereTempTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (volumeComponent == null || !volumeComponent.IsActive() || material == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(nameof(AtmosphereScatteringRenderFeature));
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                // 设置着色器参数
                SetShaderProperties(material, volumeComponent, renderingData.cameraData);
                
                // 获取相机颜色目标渲染纹理
                RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, _tempTexture, material, 0);
                Blitter.BlitCameraTexture(cmd, _tempTexture, cameraColorTarget);
            }
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // 设置着色器属性
        private static void SetShaderProperties(Material material, AtmosphereScatteringVolume volume,
                                              CameraData cameraData)
        {
            // 大气散射参数
            material.SetFloat("_ScatteringIntensity", volume.scatteringIntensity.value);
            material.SetVector("_RayleighScattering", volume.rayleighScattering.value);
            material.SetFloat("_MieScattering", volume.mieScattering.value);
            material.SetFloat("_MieAnisotropy", volume.mieAnisotropy.value);
            
            // 大气层参数
            material.SetFloat("_AtmosphereHeight", volume.atmosphereHeight.value);
            material.SetFloat("_RayleighHeight", volume.rayleighHeight.value);
            material.SetFloat("_MieHeight", volume.mieHeight.value);
            material.SetFloat("_EarthRadius", volume.earthRadius.value);
            
            // 渲染参数
            material.SetInt("_SampleCount", volume.sampleCount.value);
            material.SetInt("_LightSampleCount", volume.lightSampleCount.value);
            
            // 相机参数
            Matrix4x4 inverseView = cameraData.camera.cameraToWorldMatrix;
            Matrix4x4 inverseProj = GL.GetGPUProjectionMatrix(cameraData.camera.projectionMatrix, true).inverse;
            Vector3 cameraPos = cameraData.camera.transform.position;
            
            material.SetMatrix("_InverseViewMatrix", inverseView);
            material.SetMatrix("_InverseProjectionMatrix", inverseProj);
            material.SetVector("_CameraPosition", cameraPos);
        }

        public void Dispose()
        {
            _inputHandle?.Release();
            _tempTexture?.Release();
        }
    }

    /// <inheritdoc/>
    public override void Create()
    {
        // 创建材质
        if (settings.atmosphereShader != null)
        {
            atmosphereMaterial = new Material(settings.atmosphereShader);
        }
        
        // 创建渲染Pass
        atmospherePass = new AtmosphereScatteringRenderPass(atmosphereMaterial);
        atmospherePass.renderPassEvent = settings.renderPassEvent;
    }

    // 在渲染器中注入渲染Pass
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (atmospherePass != null && atmosphereMaterial != null)
        {
            renderer.EnqueuePass(atmospherePass);
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        // 传入相机颜色目标，这样只会影响到game视图
        if (atmospherePass != null)
        {
            atmospherePass.SetInput(renderingData.cameraData.renderer.cameraColorTargetHandle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            atmospherePass?.Dispose();
            atmospherePass = null;
            
            if (atmosphereMaterial != null)
            {
                DestroyImmediate(atmosphereMaterial);
                atmosphereMaterial = null;
            }
        }
    }
}
