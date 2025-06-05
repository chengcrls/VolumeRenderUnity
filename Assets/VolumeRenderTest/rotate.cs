using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rotate : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //object以恒定速度旋转
        transform.Rotate(new Vector3(0, 1, 0), Time.deltaTime * 50);
    }
}
