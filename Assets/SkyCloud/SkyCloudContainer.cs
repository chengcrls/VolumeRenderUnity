using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class SkyCloudContainer : MonoBehaviour
{
    public Volume volume;
    private SkyCloudVolume skyCloudVolume;

    public Color color = Color.white;
    public bool displayOutline = true;

    void Start()
    {

    }
    void Update()
    {
        if (volume != null && volume.sharedProfile.TryGet<SkyCloudVolume>(out var component))
        {
            skyCloudVolume = component;
        }
        else
        {
            Debug.LogWarning("SkyCloudVolume not found in volume profile");
        }
        if (skyCloudVolume == null)
            return;
        Vector3 min = transform.position - transform.localScale / 2;
        Vector3 max = transform.position + transform.localScale / 2;

        skyCloudVolume.boundMin.value = min;
        skyCloudVolume.boundMin.overrideState = true;
        skyCloudVolume.boundMax.value = max;
        skyCloudVolume.boundMax.overrideState = true;
    }

    void OnDrawGizmos()
    {
        if (displayOutline)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}
