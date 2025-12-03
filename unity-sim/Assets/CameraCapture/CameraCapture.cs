// ...existing code...
using System;
using System.Collections;
using UnityEngine;

public class CameraCapture : MonoBehaviour
{
    [Header("Camera Components")]

    public bool Capture = true;
    CameraObject[] _cameraObjects;
    bool isCapturing;

    void Start()
    {
        _cameraObjects = GetComponentsInChildren<CameraObject>();

        if (_cameraObjects.Length == 0)
        {
            Debug.LogError("There are no camera objects to capture");
            this.enabled = false;
        }
    }

    IEnumerator CaptureSequence()
    {
        isCapturing = true;

        foreach (var camera in _cameraObjects)
        {
            yield return StartCoroutine(camera.CaptureCoroutine(bytes =>
            {
                SendImage(bytes, this.name);
            }));
        }

        isCapturing = false;
    }

    # region send

    void SendImage(byte[] data, string name)
    {
        Debug.Log($"Got bytes with length: {data.Length}, from {name}");
        try
        {
            // implement non-blocking send here
        }
        catch (Exception e)
        {
            Debug.LogError("TCP Error: " + e.Message);
        }
    }

    # endregion

    void Update()
    {
        if (!Capture) return;

        // don't start a new pass while one is running
        if (isCapturing) return;

        StartCoroutine(CaptureSequence());
    }
}
