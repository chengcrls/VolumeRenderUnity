using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GetTransform : MonoBehaviour
{
    // Start is called before the first frame update
    private static MaterialPropertyBlock _materialPropertyBlock;
    private Renderer _rend;
    void Start()
    {
        _rend = GetComponent<Renderer>();
        if(_materialPropertyBlock == null)
            _materialPropertyBlock = new MaterialPropertyBlock();
    }

    // Update is called once per frame
    void Update()
    {
        if(_materialPropertyBlock == null)
            _materialPropertyBlock = new MaterialPropertyBlock();
        _rend.GetPropertyBlock(_materialPropertyBlock);
        _materialPropertyBlock.SetVector("_Center", transform.position);
        _materialPropertyBlock.SetVector("boundsMax", transform.position+transform.lossyScale/2);
        _materialPropertyBlock.SetVector("boundsMin", transform.position-transform.lossyScale/2);
        _rend.SetPropertyBlock(_materialPropertyBlock);
    }
}
