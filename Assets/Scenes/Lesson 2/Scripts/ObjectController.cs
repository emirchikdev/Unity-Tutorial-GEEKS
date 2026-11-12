using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectController : MonoBehaviour
{
    [SerializeField] private GameObject objectPrefab;

    private void Start()
    {
        CreateObj(objectPrefab, Vector3.zero);
    }

    private GameObject CreateObj(GameObject obj, Vector3 position)
    {
        var objFinal = Instantiate(obj, position, Quaternion.identity);
        return objFinal;
    }
}
