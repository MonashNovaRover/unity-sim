using UnityEngine;

/// <summary>
/// ScriptableObject-based configuration profiles for common LiDAR sensors.
/// Create via Assets > Create > LiDAR > Config Profile
/// </summary>
[CreateAssetMenu(fileName = "LiDARProfile", menuName = "LiDAR/Config Profile", order = 1)]
public class LiDARConfigProfile : ScriptableObject
{
    [Header("Sensor Specifications")]
    [Tooltip("Name of the LiDAR model")]
    public string ProfileName = "Custom LiDAR";
    
    [Tooltip("Minimum detection range in meters")]
    public float RangeMin = 0.1f;
    
    [Tooltip("Maximum detection range in meters")]
    public float RangeMax = 100f;
    
    [Header("Field of View")]
    [Tooltip("Horizontal field of view in degrees")]
    [Range(0f, 360f)]
    public float FovH = 360f;
    
    [Tooltip("Vertical field of view in degrees")]
    [Range(0f, 180f)]
    public float FovV = 30f;
    
    [Header("Angular Resolution")]
    [Tooltip("Horizontal angular resolution in degrees")]
    [Range(0.08f, 5f)]
    public float AngularResH = 0.2f;
    
    [Tooltip("Vertical angular resolution in degrees")]
    [Range(0.08f, 5f)]
    public float AngularResV = 2f;
    
    [Header("Layer Filtering")]
    [Tooltip("Which layers the LiDAR can detect")]
    public LayerMask DetectionLayers = -1;
    
    /// <summary>
    /// Apply this profile's settings to a ScannerParams instance
    /// </summary>
    public void ApplyToParams(ref ScannerParams scannerParams)
    {
        // Don't override the LidarLink GameObject reference
        // GameObject LidarLink is set separately
        
        scannerParams.RangeMin = RangeMin;
        scannerParams.RangeMax = RangeMax;
        scannerParams.FovH = FovH;
        scannerParams.FovV = FovV;
        scannerParams.AngularResH = AngularResH;
        scannerParams.AngularResV = AngularResV;
        scannerParams.DetectionLayers = DetectionLayers;
    }
}