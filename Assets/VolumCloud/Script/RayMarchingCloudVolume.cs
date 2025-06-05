using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Custom/RayMarchingCloud")]
public class RayMarchingCloudVolume : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabledEffect = new BoolParameter(false);
    
    public Texture3DParameter cloudShape = new Texture3DParameter(null);
    public Texture3DParameter cloudDetail = new Texture3DParameter(null);
    public Texture2DParameter weatherMap = new Texture2DParameter(null);
    public Texture2DParameter blueNoise = new Texture2DParameter(null);
    public Texture2DParameter maskMap = new Texture2DParameter(null);
    
    public FloatParameter shapeTiling = new FloatParameter(1.0f);
    public FloatParameter detailTiling = new FloatParameter(1.0f);
    
    public ColorParameter colorA = new ColorParameter(Color.white);
    public ColorParameter colorB = new ColorParameter(Color.white);
    public FloatParameter ColorOffsetA = new FloatParameter(1.0f);
    public FloatParameter ColorOffsetB = new FloatParameter(1.0f);
    
    public FloatParameter lightAbsorptionTowardSun = new FloatParameter(0f);
    public FloatParameter lightAbsorptionThroughCloud = new FloatParameter(0f);
    
    public Vector4Parameter phaseParams = new Vector4Parameter(Vector4.zero);
    
    public FloatParameter densityOffset = new FloatParameter(1.0f);
    public FloatParameter densityMultiplier = new FloatParameter(1.0f);
    
    public FloatParameter Step = new FloatParameter(1.0f);
    public FloatParameter rayStep = new FloatParameter(1.0f);
    public FloatParameter rayOffsetStrength = new FloatParameter(1.0f);
    

    public Vector4Parameter shapeNoiseWeights = new Vector4Parameter(Vector4.zero);
    public FloatParameter detailWeights = new FloatParameter(1.0f);

    public ClampedFloatParameter heightWeights = new ClampedFloatParameter(1.0f,0,1);
    public FloatParameter detailNoiseWeight = new FloatParameter(0.0f);

    public FloatParameter cloudScale = new FloatParameter(1f);
    
    public Vector4Parameter Xy_Speed_zW_Warp = new Vector4Parameter(Vector4.zero);

    public Vector3Parameter boundsMin = new Vector3Parameter(Vector3.zero);
    public Vector3Parameter boundsMax = new Vector3Parameter(Vector3.zero);
    
    public ClampedIntParameter downSample = new ClampedIntParameter(1,1,16);
    public bool IsActive() => enabledEffect.value;
    public bool IsTileCompatible() => false;
}
