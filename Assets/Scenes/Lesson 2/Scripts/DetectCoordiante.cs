using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCoordiante : MonoBehaviour
{
    public GameObject targetObject;

    public Transform GetXYZ()
    {
        if (targetObject != null)
        {
            return targetObject.transform;
        }
        else
        {
            Debug.LogWarning("Target object is not assigned.");
            return null;
        }
    }
}
