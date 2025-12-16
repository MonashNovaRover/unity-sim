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
    
    [Header("Realism Settings")]
    [Tooltip("Measurement noise standard deviation in meters")]
    [Range(0f, 0.1f)]
    public float NoiseStdDev = 0.02f;
    
    [Tooltip("Probability of random point dropout (0-1)")]
    [Range(0f, 0.1f)]
    public float DropoutProbability = 0.01f;
    
    [Tooltip("Number of rays per beam for divergence simulation")]
    [Range(1, 5)]
    public int RaysPerBeam = 1;
    
    [Tooltip("Beam divergence angle in degrees")]
    [Range(0f, 2f)]
    public float BeamDivergence = 0.3f;
    
    [Tooltip("Use material properties to calculate intensity")]
    public bool UseMaterialBasedIntensity = true;
    
    [Tooltip("Default reflectivity for materials")]
    [Range(0f, 1f)]
    public float DefaultReflectivity = 0.5f;
    
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
        scannerParams.NoiseStdDev = NoiseStdDev;
        scannerParams.DropoutProbability = DropoutProbability;
        scannerParams.RaysPerBeam = RaysPerBeam;
        scannerParams.BeamDivergence = BeamDivergence;
        scannerParams.UseMaterialBasedIntensity = UseMaterialBasedIntensity;
        scannerParams.DefaultReflectivity = DefaultReflectivity;
        scannerParams.DetectionLayers = DetectionLayers;
    }
}