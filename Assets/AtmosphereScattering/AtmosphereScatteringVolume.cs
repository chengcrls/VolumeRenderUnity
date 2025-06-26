using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("Custom/Atmosphere Scattering")]
public class AtmosphereScatteringVolume : VolumeComponent, IPostProcessComponent
{
    [Tooltip("启用大气散射")]
    public BoolParameter enable = new BoolParameter(false);

    [Header("大气散射基本参数")]
    [Tooltip("大气散射强度")]
    public FloatParameter scatteringIntensity = new FloatParameter(1.0f);
    
    [Tooltip("瑞利散射系数")]
    public Vector3Parameter rayleighScattering = new Vector3Parameter(new Vector3(5.8e-6f, 13.5e-6f, 33.1e-6f));
    
    [Tooltip("米氏散射系数")]
    public FloatParameter mieScattering = new FloatParameter(21e-6f);
    
    [Tooltip("米氏散射各向异性参数")]
    public FloatParameter mieAnisotropy = new FloatParameter(0.758f);
    
    [Header("大气层参数")]
    [Tooltip("大气层高度")]
    public FloatParameter atmosphereHeight = new FloatParameter(8500.0f);
    
    [Tooltip("瑞利散射高度")]
    public FloatParameter rayleighHeight = new FloatParameter(8500.0f);
    
    [Tooltip("米氏散射高度")]
    public FloatParameter mieHeight = new FloatParameter(1200.0f);
    
    [Tooltip("地球半径")]
    public FloatParameter earthRadius = new FloatParameter(6371000.0f);
    
    [Header("渲染设置")]
    [Tooltip("采样数量")]
    public IntParameter sampleCount = new IntParameter(16);
    
    [Tooltip("光线步长")]
    public IntParameter lightSampleCount = new IntParameter(8);

    public bool IsActive() => enable.value;

    public bool IsTileCompatible() => false;
} 