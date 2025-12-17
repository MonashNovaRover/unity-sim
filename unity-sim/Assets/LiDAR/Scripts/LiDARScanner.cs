using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using RosUtils;

/// <summary>
/// Core LiDAR scanning engine that performs raycasting and generates ROS PointCloud2 messages.
/// Simulates realistic LiDAR behavior including:
/// - Beam divergence (laser beams spread out over distance)
/// - Gaussian measurement noise
/// - Random point dropouts
/// - Material-based intensity calculation
/// Uses Unity's Job System for efficient batch raycasting.
/// </summary>
public class LiDARScanner
{
    private ScannerParams _p;
    private int _measurementsPerScanH, _measurementsPerScanV;
    private float[] _scanArrayH, _scanArrayV;
    private string _frameId;
    uint _numPoints, _raw_data_len;
    byte[] _raw_data;
    private Vector3[] _divergenceOffsets;
    
    /// <summary>
    /// Initializes the LiDAR scanner with the given parameters.
    /// Pre-calculates scan angles and allocates memory for efficient scanning.
    /// </summary>
    public LiDARScanner(ScannerParams p)
    {
        _p = p;
            
        _frameId = p.LidarLink.name;

        // Calculate scan angle ranges
        float scanAngStartH = -p.FovH / 2;
        float scanAngEndH = p.FovH / 2;
        float scanAngStartV = -p.FovV / 2;
        float scanAngEndV = p.FovV / 2;

        // Calculate number of measurements based on angular resolution
        _measurementsPerScanH = Mathf.FloorToInt((scanAngEndH - scanAngStartH) / p.AngularResH) + 1;
        _measurementsPerScanV = Mathf.FloorToInt((scanAngEndV - scanAngStartV) / p.AngularResV) + 1;
        
        // For 360° FOV, the first and last points would be at the same position (0° and 360°)
        // Subtract 1 to avoid counting the same point twice
        if (p.FovH == 360)
        {
            _measurementsPerScanH = _measurementsPerScanH - 1;
        }

        if (p.FovV == 360)
        {
            _measurementsPerScanV = _measurementsPerScanV - 1;
        }
        
        // Pre-calculate all horizontal scan angles
        _scanArrayH = new float[_measurementsPerScanH];
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            _scanArrayH[i] = scanAngStartH + i * p.AngularResH;
        }
        
        // Pre-calculate all vertical scan angles
        _scanArrayV = new float[_measurementsPerScanV];
        for (int i = 0; i < _measurementsPerScanV; i++)
        {
            _scanArrayV[i] = scanAngStartV + i * p.AngularResV;
        }

        _numPoints = (uint)(_measurementsPerScanH * _measurementsPerScanV);

        // Pre-allocate memory for point cloud data (16 bytes per point: x, y, z, intensity)
        _raw_data_len = 16 * _numPoints;
        _raw_data = new byte[_raw_data_len];
        
        // Pre-calculate divergence offsets for beam spreading simulation
        InitializeDivergenceOffsets();
    }
    
    /// <summary>
    /// Pre-calculates ray offset patterns for beam divergence simulation.
    /// Real LiDAR lasers have a finite beam width that spreads over distance.
    /// This creates a circular pattern of rays around the main beam direction.
    /// </summary>
    void InitializeDivergenceOffsets()
    {
        if (_p.RaysPerBeam <= 1)
        {
            _divergenceOffsets = new Vector3[] { Vector3.zero };
            return;
        }
        
        _divergenceOffsets = new Vector3[_p.RaysPerBeam];
        _divergenceOffsets[0] = Vector3.zero; // Center ray (main beam)
        
        // Create a circular pattern of offset angles around the main beam
        // Distribution simulates how laser energy spreads in a cone
        float divergenceRad = _p.BeamDivergence * Mathf.Deg2Rad;
        for (int i = 1; i < _p.RaysPerBeam; i++)
        {
            float angle = (i - 1) * 2f * Mathf.PI / (_p.RaysPerBeam - 1);
            float offsetX = Mathf.Cos(angle) * divergenceRad;
            float offsetY = Mathf.Sin(angle) * divergenceRad;
            _divergenceOffsets[i] = new Vector3(offsetX, offsetY, 0f);
        }
    }

    /// <summary>
    /// Performs a complete LiDAR scan and returns the results as a ROS PointCloud2 message.
    /// Process:
    /// 1. Validates sensor exists
    /// 2. Generates raycast commands for all scan angles (with divergence if enabled)
    /// 3. Executes raycasts in parallel using Unity Job System
    /// 4. Processes hits to calculate positions and intensities
    /// 5. Applies noise and dropout simulation
    /// 6. Converts to ROS coordinate system (FLU: Forward-Left-Up)
    /// </summary>
    public PointCloud2 GetScanMsg(double stampTime)
    {
        // Validate that the LiDAR link and transform still exist
        if (_p.LidarLink == null || _p.LidarLink.transform == null)
        {
            Debug.LogError("LidarLink or its transform is null in LiDARScanner");
            return CreateEmptyPointCloud(stampTime);
        }

        Transform sensor_transform = _p.LidarLink.transform;
        Vector3 sensorPos = sensor_transform.position;
        Quaternion sensorRot = sensor_transform.rotation;

        // Total commands = base measurement points * rays per beam for divergence
        int totalCommands = (int)_numPoints * _p.RaysPerBeam;
        NativeArray<RaycastCommand> commands = new(totalCommands, Allocator.TempJob);
        
        int idx = 0;
        
        // Generate raycast commands for all scan angles
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            float theta = Mathf.Deg2Rad * _scanArrayH[i]; // Horizontal angle

            for (int j = 0; j < _measurementsPerScanV; j++)
            {
                float psi = Mathf.Deg2Rad * _scanArrayV[j]; // Vertical angle
                
                // Calculate base direction vector in sensor's local space
                // Uses spherical coordinates: theta (azimuth), psi (elevation)
                Vector3 localDirVec = new Vector3 (
                    Mathf.Cos(psi) * Mathf.Sin(theta),
                    -Mathf.Sin(psi),
                    Mathf.Cos(psi) * Mathf.Cos(theta)
                );
                
                // BEAM DIVERGENCE SIMULATION
                // Real LiDAR beams aren't infinitely thin - they spread out over distance
                // Cast multiple rays in a circular pattern and take the closest hit
                // This simulates how the laser beam footprint increases with range
                for (int r = 0; r < _p.RaysPerBeam; r++)
                {
                    Vector3 divergedDir = localDirVec;
                    
                    if (_p.RaysPerBeam > 1)
                    {
                        // Calculate local tangent space to apply divergence offset
                        Vector3 tangent = Vector3.Cross(localDirVec, Vector3.up).normalized;
                        if (tangent.sqrMagnitude < 0.01f) // Handle case when beam points straight up/down
                            tangent = Vector3.Cross(localDirVec, Vector3.right).normalized;
                        Vector3 bitangent = Vector3.Cross(localDirVec, tangent).normalized;
                        
                        // Apply pre-calculated divergence offset
                        divergedDir += tangent * _divergenceOffsets[r].x + bitangent * _divergenceOffsets[r].y;
                        divergedDir.Normalize();
                    }
                    
                    // Transform direction from sensor local space to world space
                    Vector3 directionVector = sensorRot * divergedDir;
                    
                    // Start raycast at RangeMin to avoid detecting the sensor itself
                    Vector3 origin = _p.RangeMin * directionVector + sensorPos;
                    
                    // Create raycast command with layer filtering
                    commands[idx++] = new RaycastCommand(origin, directionVector, 
                        new QueryParameters(_p.DetectionLayers, false), _p.RangeMax);
                }
            }
        }

        // Execute all raycasts in parallel using Unity's Job System
        NativeArray<RaycastHit> results = new(totalCommands, Allocator.TempJob);
        JobHandle raycastJob = RaycastCommand.ScheduleBatch(commands, results, 128);
        raycastJob.Complete(); // Wait for completion

        // Process raycast results and build point cloud data
        int raw_data_indx = 0;
        for (int k = 0; k < (int)_numPoints; k++)
        {
            int baseOffset = raw_data_indx * 16; // 16 bytes per point (4 floats)
            
            // When using beam divergence, find the closest valid hit among all diverged rays
            // This simulates how LiDAR returns the strongest/closest reflection
            RaycastHit bestHit = default;
            float closestDistance = float.MaxValue;
            bool hasValidHit = false;
            
            for (int r = 0; r < _p.RaysPerBeam; r++)
            {
                int resultIdx = k * _p.RaysPerBeam + r;
                RaycastHit hit = results[resultIdx];
                
                if (hit.collider != null && hit.distance < closestDistance)
                {
                    bestHit = hit;
                    closestDistance = hit.distance;
                    hasValidHit = true;
                }
            }
            
            float x, y, z, intensity;
            
            // DROPOUT SIMULATION
            // Real LiDAR occasionally misses returns due to:
            // - Surface absorption (dark/black materials)
            // - Extreme angles
            // - Environmental conditions (rain, fog, dust)
            bool isDropout = UnityEngine.Random.value < _p.DropoutProbability;
            
            if (hasValidHit && !isDropout)
            {
                Vector3 delta = bestHit.point - sensorPos;
                float distance = bestHit.distance;
                
                // GAUSSIAN NOISE SIMULATION
                // Real LiDAR measurements have noise due to:
                // - Timing precision limits
                // - Signal processing
                // - Environmental factors
                // Uses Box-Muller transform to generate normally distributed noise
                if (_p.NoiseStdDev > 0f)
                {
                    // Box-Muller transform: converts uniform random to Gaussian
                    float u1 = UnityEngine.Random.value;
                    float u2 = UnityEngine.Random.value;
                    float noise = _p.NoiseStdDev * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
                    
                    // Apply noise along the ray direction (range error, not lateral)
                    Vector3 noiseDir = delta.normalized;
                    delta += noiseDir * noise;
                }
                
                // COORDINATE SYSTEM CONVERSION
                // Unity uses left-handed Y-up: X=right, Y=up, Z=forward
                // ROS uses right-handed Z-up FLU: X=forward, Y=left, Z=up
                x = delta.z;   // Forward (Unity Z -> ROS X)
                y = -delta.x;  // Left (Unity -X -> ROS Y)
                z = delta.y;   // Up (Unity Y -> ROS Z)
                
                // Calculate intensity based on physical properties
                intensity = CalculateIntensity(bestHit, distance);
            }
            else
            {
                // No valid hit or random dropout - mark as invalid point
                x = y = z = float.NaN;
                intensity = 0.0f;
            }

            // Write point data to byte array (little-endian format)
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, _raw_data, baseOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, _raw_data, baseOffset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, _raw_data, baseOffset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(intensity), 0, _raw_data, baseOffset + 12, 4);

            raw_data_indx++;
        }

        commands.Dispose();
        results.Dispose();

        // Build ROS PointCloud2 message
        return new PointCloud2
        {
            header = TimeStamp.GetHeader(stampTime, _frameId),
            height = 1,
            width = _numPoints,
            fields = new PointField[]
            {
                new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "intensity", offset = 12, datatype = PointField.FLOAT32, count = 1 }
            },
            is_bigendian = false,
            point_step = 16,
            row_step = _raw_data_len,
            data = _raw_data,
            is_dense = false // Contains NaN values for invalid points
        };
    }
    
    /// <summary>
    /// Calculates realistic intensity values based on physical properties.
    /// Intensity represents how strongly the laser light reflects back to the sensor.
    /// 
    /// Three physical factors affect intensity:
    /// 1. DISTANCE FALLOFF (Inverse Square Law)
    ///    - Light intensity decreases with distance
    ///    - Approximated as 1/(1 + normalized_distance) for computational efficiency
    /// 
    /// 2. SURFACE REFLECTIVITY (Material Properties)
    ///    - Bright/white surfaces reflect more light than dark/black surfaces
    ///    - Extracted from material albedo/color as a grayscale value
    ///    - Falls back to DefaultReflectivity if material data unavailable
    /// 
    /// 3. ANGLE OF INCIDENCE (Lambert's Cosine Law)
    ///    - Surfaces perpendicular to the beam reflect more than angled surfaces
    ///    - Calculated using dot product between surface normal and ray direction
    ///    - Grazing angles (near parallel) produce weak returns
    /// 
    /// The final intensity is the product of all three factors, clamped to [0,1].
    /// </summary>
    float CalculateIntensity(RaycastHit hit, float distance)
    {
        float intensity = 0f;
        
        if (_p.UseMaterialBasedIntensity)
        {
            // 1. Distance falloff (inverse square law approximation)
            //    Real physics: I ∝ 1/d², but we use 1/(1+d/max) for smoother falloff
            float distanceIntensity = 1.0f / (1.0f + distance / _p.RangeMax);
            
            // 2. Surface reflectivity from material properties
            float reflectivity = _p.DefaultReflectivity;
            
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material mat = renderer.sharedMaterial;
                
                // Extract albedo/color as reflectivity proxy
                // Standard Shader uses _Color, URP/HDRP use _BaseColor
                if (mat.HasProperty("_Color"))
                {
                    Color albedo = mat.GetColor("_Color");
                    reflectivity = albedo.grayscale; // Convert to grayscale [0,1]
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    Color albedo = mat.GetColor("_BaseColor");
                    reflectivity = albedo.grayscale;
                }
                // Could extend to check for _MainTex texture and sample it
            }
            
            // 3. Angle of incidence (Lambert's cosine law)
            //    cos(θ) = dot(normal, -ray_direction)
            //    θ = 0° (perpendicular): maximum reflection
            //    θ = 90° (grazing): no reflection
            Vector3 rayDir = (hit.point - hit.collider.transform.position).normalized;
            float cosIncidence = Mathf.Abs(Vector3.Dot(hit.normal, -rayDir));
            
            // Combine factors: distance falloff * reflectivity * angle
            intensity = distanceIntensity * reflectivity * cosIncidence;
        }
        else
        {
            // Simple distance-based intensity fallback
            // Linear mapping: 0.0 (close) to 1.0 (far)
            intensity = Mathf.Clamp01(distance / _p.RangeMax);
        }
        
        return Mathf.Clamp01(intensity);
    }

    /// <summary>
    /// Creates an empty PointCloud2 message for error cases.
    /// Used when the sensor is invalid or disconnected.
    /// </summary>
    private PointCloud2 CreateEmptyPointCloud(double stampTime)
    {
        return new PointCloud2
        {
            header = TimeStamp.GetHeader(stampTime, _frameId),
            height = 1,
            width = 0,
            fields = new PointField[]
            {
                new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "intensity", offset = 12, datatype = PointField.FLOAT32, count = 1 }
            },
            is_bigendian = false,
            point_step = 16,
            row_step = 0,
            data = new byte[0],
            is_dense = false
        };
    }
}