using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom/SkyCloud")]
public class SkyCloudVolume : VolumeComponent, IPostProcessComponent
{
    public BoolParameter isActive = new BoolParameter(false);
    public Texture3DParameter cloudBorder = new Texture3DParameter(null);
    public Texture3DParameter cloudDetail = new Texture3DParameter(null);
    public Texture3DParameter cloudSDF = new Texture3DParameter(null);

    public Texture2DParameter sinTexture = new Texture2DParameter(null);

    public Texture3DParameter cloudLightInfo1 = new Texture3DParameter(null);
    public Texture3DParameter cloudLightInfo2 = new Texture3DParameter(null);
    public Texture3DParameter cloudLightInfo3 = new Texture3DParameter(null);

    public Texture3DParameter worlyNoise = new Texture3DParameter(null);

    public Vector3Parameter boundMin = new Vector3Parameter(Vector3.zero);
    public Vector3Parameter boundMax = new Vector3Parameter(Vector3.zero);

    public Vector3Parameter lightColor1 = new Vector3Parameter(Vector3.zero);
    public Vector3Parameter lightColor2 = new Vector3Parameter(Vector3.zero);
    public Vector4Parameter lightParams = new Vector4Parameter(Vector4.zero);
    public Vector3Parameter cloudEmission = new Vector3Parameter(Vector3.zero);

    public Vector3Parameter atmosphereCenter = new Vector3Parameter(Vector3.zero);
    public Vector4Parameter NoiseParams1 = new Vector4Parameter(Vector4.zero);
    public Vector4Parameter NoiseParams2 = new Vector4Parameter(Vector4.zero);
    
    // 实现IPostProcessComponent接口
    public bool IsActive() => isActive.value;
    public bool IsTileCompatible() => false;
} 