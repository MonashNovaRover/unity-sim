using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class LockTransform : MonoBehaviour
{
    [SerializeField] private Transform _bind;
    [SerializeField] private Vector3 _offset;
    private void Update()
    {
        transform.SetPositionAndRotation(_bind.position + _offset, _bind.rotation);
    }
}