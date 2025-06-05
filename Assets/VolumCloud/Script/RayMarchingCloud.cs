using System;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RayMarchingCloud : ScriptableRendererFeature
{
    class RayMarchingCloudPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private RTHandle _tempTexture;
        private RTHandle _downSampleDepthTexture;
        private RTHandle _inputHandle;
        private RayMarchingCloudVolume _volume;
        
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(nameof(RayMarchingCloud));
        
        public void SetInput(RTHandle src)
        {
            // The Renderer Feature uses this variable to set the input RTHandle.
            _inputHandle = src;
        }
        public RayMarchingCloudPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            _volume = VolumeManager.instance.stack.GetComponent<RayMarchingCloudVolume>();
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_volume == null || !_volume.IsActive())
                return;
            var desc=cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.width = cameraTextureDescriptor.width/_volume.downSample.value;
            desc.height = cameraTextureDescriptor.height/_volume.downSample.value;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempTexture, desc, name: "tempTexture");

            var descDepth = cameraTextureDescriptor;
            descDepth.depthBufferBits = 0;
            descDepth.msaaSamples = 1;
            descDepth.colorFormat = RenderTextureFormat.RFloat;
            descDepth.width = cameraTextureDescriptor.width/_volume.downSample.value;
            descDepth.height = cameraTextureDescriptor.height/_volume.downSample.value;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _downSampleDepthTexture, desc, name: "downSampleDepthTexture");
            //ConfigureTarget(_tempTexture);
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (_volume == null || !_volume.IsActive())
                return;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            int screenWidth = descriptor.width;
            int screenHeight = descriptor.height;
            CommandBuffer cmd = CommandBufferPool.Get(nameof(RayMarchingCloud));
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                _material.SetVector("_boundsMin", _volume.boundsMin.value);
                _material.SetVector("_boundsMax", _volume.boundsMax.value);
                _material.SetVector("_phaseParams", _volume.phaseParams.value);
                _material.SetVector("_shapeNoiseWeights", _volume.shapeNoiseWeights.value);

                _material.SetFloat("_rayOffsetStrength",_volume.rayOffsetStrength.value);
                
                _material.SetFloat("_shapeTiling", _volume.shapeTiling.value);
                _material.SetFloat("_detailTiling", _volume.detailTiling.value);
                
                _material.SetFloat("_detailWeights", _volume.detailWeights.value);
                _material.SetFloat("_detailNoiseWeight", _volume.detailNoiseWeight.value);
                _material.SetFloat("_heightWeights",_volume.heightWeights.value);
                
                _material.SetFloat("_lightAbsorptionTowardSun",_volume.lightAbsorptionTowardSun.value);
                _material.SetFloat("_lightAbsorptionThroughCloud",_volume.lightAbsorptionThroughCloud.value);
                
                _material.SetFloat("_step",_volume.Step.value);
                _material.SetFloat("_rayStep",_volume.rayStep.value);
                
                _material.SetTexture("cloudShape", _volume.cloudShape.value);
                _material.SetTexture("cloudDetail", _volume.cloudDetail.value);
                _material.SetTexture("weatherMap", _volume.weatherMap.value);
                _material.SetTexture("BlueNoise", _volume.blueNoise.value);
                _material.SetTexture("maskMap", _volume.maskMap.value);
                Vector4 blueNoseST = new Vector4(
                    screenWidth / (float)_volume.blueNoise.value.width,
                    screenHeight / (float)_volume.blueNoise.value.height,0,0);
                _material.SetVector("_BlueNoiseST", blueNoseST);
                
                _material.SetVector("_colA", _volume.colorA.value);
                _material.SetVector("_colB", _volume.colorB.value);
                _material.SetFloat("_colorOffset1",_volume.ColorOffsetA.value);
                _material.SetFloat("_colorOffset2",_volume.ColorOffsetB.value);

                _material.SetFloat("_densityOffset",_volume.densityOffset.value);
                _material.SetFloat("_densityMultiplier",_volume.densityMultiplier.value);
                
                _material.SetVector("_xy_Speed_zw_Warp",_volume.Xy_Speed_zW_Warp.value);
                
                RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, _downSampleDepthTexture,_material,1);
                Blitter.BlitCameraTexture(cmd, _downSampleDepthTexture, _tempTexture, _material, 0);
                Blitter.BlitCameraTexture(cmd, _tempTexture, cameraColorTarget, _material, 2);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            //context.Submit();
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
    private RayMarchingCloudPass _scriptablePass;
    private Material _material;

    /// <inheritdoc/>
    public override void Create()
    {
        if (settings.shader != null)
        {
            _material = new Material(settings.shader);
            //_material = CoreUtils.CreateEngineMaterial(settings.shader);
            _scriptablePass = new RayMarchingCloudPass(_material);
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


