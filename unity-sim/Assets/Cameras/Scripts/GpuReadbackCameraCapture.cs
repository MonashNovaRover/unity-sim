using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A simple, inefficient (because we read back from the GPU) component to capture frames from a camera.
/// We should ideally move away from this, and towards a solution which allows gpu memory to somehow be passed between
/// Unity and GStreamer.
/// </summary>
[RequireComponent(typeof(Camera))]
public class GpuReadbackCameraCapture : MonoBehaviour
{
    [Header("Camera & RenderTexture Settings")]
    private Camera sourceCamera;
    private RenderTexture renderTexture;

    [Header("Frame Capture Settings")]
    public int width = 1280;
    public int height = 720;
    public float framerate = 1f;
    
    /// Callback for frame data in the form (byte[] data, width, height)
    public Action<byte[], int, int> OnFrameCaptured;  

    private float _frameTimer = 0f;

    void OnEnable()
    {
        // Create and assign RenderTexture if not already assigned
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();
        }

		renderTexture = new RenderTexture(width, height, (int)framerate);
		renderTexture.Create();

		sourceCamera = GetComponent<Camera>();
		sourceCamera.targetTexture = renderTexture;
    }

    private void LateUpdate()
    {
        _frameTimer += Time.deltaTime;
        if (_frameTimer < 1f / framerate) 
            return;
        
        CaptureFrame();
        _frameTimer = 0f;
    }

    private void CaptureFrame()
    {
        sourceCamera.Render();
        AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, OnCompleteReadback);
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("GPU Readback failed.");
            return;
        }

        var rawData = request.GetData<byte>();

        // Make a copy into managed memory (for ROS 2 publishing)
        var imageData = new byte[rawData.Length];
        rawData.CopyTo(imageData);

        // Trigger callback with image data
        OnFrameCaptured?.Invoke(imageData, renderTexture.width, renderTexture.height);
    }    
}
