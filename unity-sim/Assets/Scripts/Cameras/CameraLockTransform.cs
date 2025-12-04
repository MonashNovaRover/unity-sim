using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CameraLockTransform : MonoBehaviour
{
    [SerializeField] public Transform _bind;

    private void Update()
    {
        transform.SetPositionAndRotation(_bind.position, _bind.rotation);
    }
}