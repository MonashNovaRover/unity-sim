using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraObject : MonoBehaviour
{
    [Header("Camera Components")]

    public bool Capture = true;
    Camera _cam;
    Texture2D _tex;
    RenderTexture _rt;
    bool isCapturing;

    [Header("Lock away")]
    [SerializeField] Transform _bind;

    # region setup
    private void Start() 
    {
        _cam = GetComponent<Camera>() ?? Camera.main;
        if (_cam == null)
        {
            Debug.LogError("No camera found for CameraCapture");
            this.enabled = false;
            return;
        }

        if (_rt == null)
        {
            if (_cam.targetTexture != null)
            {
                _rt = _cam.targetTexture;
            }
            else
            {
                _rt = new RenderTexture(Screen.width, Screen.height, 24);
                _rt.Create();
            }
        }

        if (_tex == null)
        {
            _tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
        }

        // ensure we always have a bind (this.transform always exists)
        if (_bind == null) _bind = this.transform;
    }

    # endregion

    public void StartCapture(Action<byte[]> onComplete)
    {
        StartCoroutine(CaptureCoroutine(onComplete));
    }

    public IEnumerator CaptureCoroutine(Action<byte[]> onComplete = null)
    {
        isCapturing = true;

        _cam.targetTexture = _rt;
        _cam.Render();
        RenderTexture.active = _rt;

        _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
        _tex.Apply();

        // one-frame delay to avoid GPU/CPU sync issues
        yield return null;

        byte[] imageBytes = _tex.EncodeToJPG(); // or EncodeToPNG()

        onComplete?.Invoke(imageBytes);

        isCapturing = false;
    }

    void Update()
    {
        if (_bind != null)
        {
            this.transform.position = _bind.position;
            this.transform.rotation = _bind.rotation;
        }
    }
}
