using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public Color color = Color.white;
    public bool displayOutline = true;

    void OnDrawGizmos()
    {
        if (displayOutline)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}
