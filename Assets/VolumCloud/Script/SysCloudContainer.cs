using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class SysCloudContainer : MonoBehaviour
{
    public Volume volume;
    private RayMarchingCloudVolume rayMarchingCloudVolume;

    void Start()
    {

    }
    void Update()
    {
        if (volume != null && volume.sharedProfile.TryGet<RayMarchingCloudVolume>(out var component))
        {
            rayMarchingCloudVolume = component;
        }
        else
        {
            Debug.LogWarning("RayMarchingCloudVolume not found in volume profile");
        }
        if (rayMarchingCloudVolume == null)
            return;
        Vector3 min = transform.position - transform.localScale / 2;
        Vector3 max = transform.position + transform.localScale / 2;

        rayMarchingCloudVolume.boundsMin.value = min;
        rayMarchingCloudVolume.boundsMin.overrideState = true;
        rayMarchingCloudVolume.boundsMax.value = max;
        rayMarchingCloudVolume.boundsMax.overrideState = true;
    }
}
