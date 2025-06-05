using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom/SkyCloud")]
public class SkyCloudVolume : VolumeComponent, IPostProcessComponent
{
    public BoolParameter isActive = new BoolParameter(false);
    public Texture3DParameter cloudBorder = new Texture3DParameter(null);
    public Texture3DParameter noise = new Texture3DParameter(null);
    public Texture3DParameter cloudSDF = new Texture3DParameter(null);

    public Texture2DParameter uvNoise = new Texture2DParameter(null);

    public Vector3Parameter boundMin = new Vector3Parameter(Vector3.zero);
    public Vector3Parameter boundMax = new Vector3Parameter(Vector3.zero);
    
    // 实现IPostProcessComponent接口
    public bool IsActive() => isActive.value;
    public bool IsTileCompatible() => false;
} 