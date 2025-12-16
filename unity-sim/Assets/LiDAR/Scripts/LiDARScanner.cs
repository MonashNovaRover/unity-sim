using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using RosUtils;

public class LiDARScanner
{
    private ScannerParams _p;
    private int _measurementsPerScanH, _measurementsPerScanV;
    private float[] _scanArrayH, _scanArrayV;
    private string _frameId;
    uint _numPoints, _raw_data_len;
    byte[] _raw_data;
    
    // For beam divergence
    private Vector3[] _divergenceOffsets;
    
    public LiDARScanner(ScannerParams p)
    {
        _p = p;
            
        _frameId = p.LidarLink.name;

        float scanAngStartH = -p.FovH / 2;
        float scanAngEndH = p.FovH / 2;
        float scanAngStartV = -p.FovV / 2;
        float scanAngEndV = p.FovV / 2;

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
        
        _scanArrayH = new float[_measurementsPerScanH];
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            _scanArrayH[i] = scanAngStartH + i * p.AngularResH;
        }
        
        _scanArrayV = new float[_measurementsPerScanV];
        for (int i = 0; i < _measurementsPerScanV; i++)
        {
            _scanArrayV[i] = scanAngStartV + i * p.AngularResV;
        }

        _numPoints = (uint)(_measurementsPerScanH * _measurementsPerScanV);

        _raw_data_len = 16 * _numPoints;
        _raw_data = new byte[_raw_data_len];
        
        // Pre-calculate divergence offsets for beam spreading
        InitializeDivergenceOffsets();
    }
    
    void InitializeDivergenceOffsets()
    {
        if (_p.RaysPerBeam <= 1)
        {
            _divergenceOffsets = new Vector3[] { Vector3.zero };
            return;
        }
        
        _divergenceOffsets = new Vector3[_p.RaysPerBeam];
        _divergenceOffsets[0] = Vector3.zero; // Center ray
        
        // Create a circular pattern of offset angles
        float divergenceRad = _p.BeamDivergence * Mathf.Deg2Rad;
        for (int i = 1; i < _p.RaysPerBeam; i++)
        {
            float angle = (i - 1) * 2f * Mathf.PI / (_p.RaysPerBeam - 1);
            float offsetX = Mathf.Cos(angle) * divergenceRad;
            float offsetY = Mathf.Sin(angle) * divergenceRad;
            _divergenceOffsets[i] = new Vector3(offsetX, offsetY, 0f);
        }
    }

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

        // Total commands = base points * rays per beam
        int totalCommands = (int)_numPoints * _p.RaysPerBeam;
        NativeArray<RaycastCommand> commands = new(totalCommands, Allocator.TempJob);
        
        int idx = 0;
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            float theta = Mathf.Deg2Rad * _scanArrayH[i];

            for (int j = 0; j < _measurementsPerScanV; j++)
            {
                float psi = Mathf.Deg2Rad * _scanArrayV[j];
                
                // Base direction for this measurement
                Vector3 localDirVec = new Vector3 (
                    Mathf.Cos(psi) * Mathf.Sin(theta),
                    -Mathf.Sin(psi),
                    Mathf.Cos(psi) * Mathf.Cos(theta)
                );
                
                // Cast multiple rays with divergence
                for (int r = 0; r < _p.RaysPerBeam; r++)
                {
                    Vector3 divergedDir = localDirVec;
                    
                    if (_p.RaysPerBeam > 1)
                    {
                        // Apply divergence offset in local tangent space
                        Vector3 tangent = Vector3.Cross(localDirVec, Vector3.up).normalized;
                        if (tangent.sqrMagnitude < 0.01f) // localDirVec is parallel to up
                            tangent = Vector3.Cross(localDirVec, Vector3.right).normalized;
                        Vector3 bitangent = Vector3.Cross(localDirVec, tangent).normalized;
                        
                        divergedDir += tangent * _divergenceOffsets[r].x + bitangent * _divergenceOffsets[r].y;
                        divergedDir.Normalize();
                    }
                    
                    Vector3 directionVector = sensorRot * divergedDir;
                    Vector3 origin = _p.RangeMin * directionVector + sensorPos;
                    commands[idx++] = new RaycastCommand(origin, directionVector, 
                        new QueryParameters(_p.DetectionLayers, false), _p.RangeMax);
                }
            }
        }

        NativeArray<RaycastHit> results = new(totalCommands, Allocator.TempJob);
        JobHandle raycastJob = RaycastCommand.ScheduleBatch(commands, results, 128);
        raycastJob.Complete();

        // Process results - if using beam divergence, select closest hit
        int raw_data_indx = 0;
        for (int k = 0; k < (int)_numPoints; k++)
        {
            int baseOffset = raw_data_indx * 16;
            
            // Find the closest valid hit among the diverged rays
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
            
            // Random dropout simulation
            bool isDropout = UnityEngine.Random.value < _p.DropoutProbability;
            
            if (hasValidHit && !isDropout)
            {
                Vector3 delta = bestHit.point - sensorPos;
                float distance = bestHit.distance;
                
                // Add Gaussian noise to the measurement
                if (_p.NoiseStdDev > 0f)
                {
                    // Box-Muller transform for Gaussian noise
                    float u1 = UnityEngine.Random.value;
                    float u2 = UnityEngine.Random.value;
                    float noise = _p.NoiseStdDev * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
                    
                    // Apply noise along the ray direction
                    Vector3 noiseDir = delta.normalized;
                    delta += noiseDir * noise;
                }
                
                // Coordinate conversion: Unity (Left-handed Y-up) -> ROS (Right-handed Z-up FLU convention)
                // Unity coordinates: X=right, Y=up, Z=forward
                // ROS FLU coordinates: X=forward, Y=left, Z=up
                x = delta.z;   // Forward (Unity Z -> ROS X)
                y = -delta.x;  // Left (Unity -X -> ROS Y)
                z = delta.y;   // Up (Unity Y -> ROS Z)
                
                // Calculate intensity based on material properties and distance
                intensity = CalculateIntensity(bestHit, distance);
            }
            else
            {
                // No hit or dropout - set coordinates to NaN to indicate invalid point
                x = y = z = float.NaN;
                intensity = 0.0f;
            }

            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, _raw_data, baseOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, _raw_data, baseOffset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, _raw_data, baseOffset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(intensity), 0, _raw_data, baseOffset + 12, 4);

            raw_data_indx++;
        }

        commands.Dispose();
        results.Dispose();

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
            is_dense = false
        };
    }
    
    float CalculateIntensity(RaycastHit hit, float distance)
    {
        float intensity = 0f;
        
        if (_p.UseMaterialBasedIntensity)
        {
            // Base intensity from distance (inverse square law approximation)
            float distanceIntensity = 1.0f / (1.0f + distance / _p.RangeMax);
            
            // Get material reflectivity
            float reflectivity = _p.DefaultReflectivity;
            
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material mat = renderer.sharedMaterial;
                
                // Try to get albedo/color as reflectivity proxy
                if (mat.HasProperty("_Color"))
                {
                    Color albedo = mat.GetColor("_Color");
                    reflectivity = albedo.grayscale; // Use grayscale value as reflectivity
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    Color albedo = mat.GetColor("_BaseColor");
                    reflectivity = albedo.grayscale;
                }
            }
            
            // Factor in angle of incidence (Lambert's cosine law)
            Vector3 rayDir = (hit.point - hit.collider.transform.position).normalized;
            float cosIncidence = Mathf.Abs(Vector3.Dot(hit.normal, -rayDir));
            
            // Combine factors: distance falloff * reflectivity * angle
            intensity = distanceIntensity * reflectivity * cosIncidence;
        }
        else
        {
            // Simple distance-based intensity (0.0 = close, 1.0 = far)
            intensity = Mathf.Clamp01(distance / _p.RangeMax);
        }
        
        return Mathf.Clamp01(intensity);
    }

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