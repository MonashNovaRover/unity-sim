using System;
using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using PoseStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.PoseStamped;
using RosUtils;

/// <summary>
/// Configuration parameters for a LiDAR sensor.
/// Defines the physical characteristics and simulation realism settings.
/// Can be configured manually or loaded from a LiDARConfigProfile asset.
/// </summary>
[System.Serializable] 
public class ScannerParams
{
    [Header("Sensor Configuration")]
    [Tooltip("GameObject representing the LiDAR sensor in the scene")]
    public GameObject LidarLink;
    
    [Tooltip("Minimum detection range in meters")]
    public float RangeMin = 0;
    
    [Tooltip("Maximum detection range in meters")]
    public float RangeMax = 1000;

    [Header("Field of View")]
    [Tooltip("Horizontal field of view in degrees (360 = full rotation)")]
    public float FovH = 360;
    
    [Tooltip("Vertical field of view in degrees")]
    public float FovV = 45;

    [Header("Angular Resolution")]
    [Tooltip("Horizontal angular resolution in degrees (smaller = more points)")]
    public float AngularResH = 1;
    
    [Tooltip("Vertical angular resolution in degrees (smaller = more points)")]
    public float AngularResV = 1;
    
    [Tooltip("Layer mask to determine which objects the LiDAR can detect")]
    public LayerMask DetectionLayers = -1; // Default to all layers
    
    [Header("Realism Settings")]
    [Tooltip("Standard deviation of Gaussian noise added to measurements (in meters)")]
    public float NoiseStdDev = 0.01f;
    
    [Tooltip("Probability of random point dropout (0-1). Simulates missed returns.")]
    [Range(0f, 1f)]
    public float DropoutProbability = 0.02f;
    
    [Tooltip("Number of rays per beam for divergence simulation (1 = no divergence)")]
    [Range(1, 5)]
    public int RaysPerBeam = 1;
    
    [Tooltip("Beam divergence angle in degrees. Simulates laser beam spread.")]
    [Range(0f, 5f)]
    public float BeamDivergence = 0.1f;
    
    [Tooltip("Use material properties to calculate intensity (more realistic)")]
    public bool UseMaterialBasedIntensity = true;
    
    [Tooltip("Base reflectivity for materials without albedo information")]
    [Range(0f, 1f)]
    public float DefaultReflectivity = 0.5f;
}

/// <summary>
/// Main LiDAR publisher component that manages scanning and ROS message publication.
/// Handles:
/// - Periodic scanning based on configured frequency
/// - Adaptive scanning (only scan when sensor moves)
/// - Publishing PointCloud2 messages to ROS
/// - Publishing sensor pose messages
/// - Optional debug visualization
/// 
/// Attach this component to a GameObject in your scene and configure the scanner parameters.
/// Execution order is set to 200 to ensure it runs after physics updates.
/// </summary>
[DefaultExecutionOrder(200)]
public class LiDARPublisher : MonoBehaviour
{
    [Header("ROS Topics")]
    [Tooltip("ROS topic for publishing point cloud data")]
    public string pointsTopic = "/point_cloud";
    
    [Tooltip("ROS topic for publishing LiDAR pose")]
    public string poseTopic = "/laser_scan_pose";
    
    [Header("Configuration")]
    [Tooltip("Optional: Load settings from a pre-configured profile (overrides manual settings)")]
    [SerializeField] private LiDARConfigProfile _configProfile;
    
    [Tooltip("Manual scanner configuration (used if no profile is assigned)")]
    [SerializeField] private ScannerParams _scannerParams;

    [Header("Publishing Rate")]
    [Tooltip("Scan frequency in Hz (scans per second)")]
    [SerializeField] private double _hz = 20f;
    
    [Header("Adaptive Scanning")]
    [Tooltip("Only scan when the sensor has moved (saves CPU for static scenes)")]
    [SerializeField] private bool _useAdaptiveScanning = true;
    
    [Tooltip("Minimum position change (meters) to trigger a scan")]
    [SerializeField] private float _positionThreshold = 0.01f;
    
    [Tooltip("Minimum rotation change (degrees) to trigger a scan")]
    [SerializeField] private float _rotationThreshold = 0.5f;
    
    private double _lastPublishTime;
    private double _publishPeriod => 1.0f / _hz;
    
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private bool _hasInitialPose;

    [Header("Debug Visualization")]
    [Tooltip("Draw debug rays for point cloud (only works at low scan rates)")]
    [SerializeField] private bool _enableVisualization = false;
    
    [Tooltip("How long debug rays should persist in seconds")]
    [SerializeField] private float _visualizationDuration = 1f;

    // Core components
	private LiDARScanner _lidarScanner;
	private Publisher<PointCloud2> _pcPublisher;
	private Publisher<PoseStamped> _posePublisher;

    /// <summary>
    /// Initializes the LiDAR scanner and ROS publishers.
    /// Applies configuration profile if assigned, otherwise uses manual parameters.
    /// </summary>
    void Start()
    {   
        if (_configProfile != null)
        {
            _configProfile.ApplyToParams(ref _scannerParams);
            Debug.Log($"Applied LiDAR profile: {_configProfile.ProfileName}");
        }
        
		CleanParameters();

        _lidarScanner = new LiDARScanner(_scannerParams);

		_pcPublisher = new Publisher<PointCloud2>(pointsTopic);
		_posePublisher = new Publisher<PoseStamped>(poseTopic);

        _lastPublishTime = Clock.time + _publishPeriod;
        
        // Initialize adaptive scanning baseline
        if (_scannerParams.LidarLink != null)
        {
            _lastPosition = _scannerParams.LidarLink.transform.position;
            _lastRotation = _scannerParams.LidarLink.transform.rotation;
            _hasInitialPose = true;
        }
    }

    void CleanParameters()
    {
        _scannerParams.FovH = _scannerParams.FovH <= 360 ? _scannerParams.FovH : 360;
        _scannerParams.FovV = _scannerParams.FovV <= 360 ? _scannerParams.FovV : 360;
    }

    /// <summary>
    /// Determines if a scan should be performed based on sensor movement.
    /// Used for adaptive scanning to avoid unnecessary scans when the sensor is stationary.
    /// Ideal for mobile robots where the environment is mostly static.
    /// </summary>
    bool ShouldScan()
    {
        if (!_useAdaptiveScanning)
            return true;
            
        if (_scannerParams.LidarLink == null)
            return false;
            
        // Initialize baseline pose on first check
        if (!_hasInitialPose)
        {
            _lastPosition = _scannerParams.LidarLink.transform.position;
            _lastRotation = _scannerParams.LidarLink.transform.rotation;
            _hasInitialPose = true;
            return true;
        }
        
        Transform sensorTransform = _scannerParams.LidarLink.transform;
        
        float positionDelta = Vector3.Distance(sensorTransform.position, _lastPosition);
        if (positionDelta > _positionThreshold)
        {
            _lastPosition = sensorTransform.position;
            _lastRotation = sensorTransform.rotation;
            return true;
        }
        
        float rotationDelta = Quaternion.Angle(sensorTransform.rotation, _lastRotation);
        if (rotationDelta > _rotationThreshold)
        {
            _lastPosition = sensorTransform.position;
            _lastRotation = sensorTransform.rotation;
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Main update loop - performs scanning and publishing at configured frequency.
    /// LateUpdate ensures it runs after all physics and transform updates.
    /// </summary>
    void LateUpdate()
    {
	    double stampTime = Clock.FrameStartTimeInSeconds;
		
        if (stampTime - _lastPublishTime < _publishPeriod)
            return;

        if (_scannerParams.LidarLink == null)
        {
            Debug.LogWarning("LidarLink is null, skipping LiDAR scan");
            return;
        }
        
        // Check if scan is needed based on movement (if adaptive scanning enabled)
        if (!ShouldScan())
            return;

        PointCloud2 pointCloudMsg = _lidarScanner.GetScanMsg(stampTime);

        if (_enableVisualization && _hz <= 5)
            VisualizePointCloud(pointCloudMsg);

        var pos = _scannerParams.LidarLink.transform.position;
		var rot = _scannerParams.LidarLink.transform.rotation;

        // Build pose message
		var poseMsg = new PoseStamped
		{
    		header = new Header
			{
    			frame_id = _scannerParams.LidarLink.name,
    			stamp = pointCloudMsg.header.stamp
			},
    		pose = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose
    		{
        		position = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Point(pos.x, pos.y, pos.z),
        		orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion(rot.x, rot.y, rot.z, rot.w)
    		}
		};
        
        // Publish to ROS topics
        _pcPublisher.Publish(pointCloudMsg);
        _posePublisher.Publish(poseMsg);

		_lastPublishTime = stampTime;
    }

	private void OnDestroy()
    {
        _pcPublisher?.Dispose();
		_posePublisher?.Dispose();
    }
	
	#region debugging

    /// <summary>
    /// Visualizes the point cloud as debug rays in the Unity scene view.
    /// Each point is drawn as a small vertical ray, colored by intensity.
    /// Only recommended for low-frequency scans (1-5 Hz) to avoid performance issues.
    /// </summary>
    void VisualizePointCloud(PointCloud2 pointCloudMsg)
    {
        int step = (int)pointCloudMsg.point_step;
        Vector3 sensorPos = _scannerParams.LidarLink.transform.position;

        for (int i = 0; i < (int)pointCloudMsg.width; i++)
        {
            int baseOffset = i * step;

            // Extract point data from byte array
            float x = BitConverter.ToSingle(pointCloudMsg.data, baseOffset);
            float y = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 4);
            float z = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 8);
            float intensity = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 12);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            // Convert back from ROS coordinates to Unity coordinates for visualization
            Vector3 localPoint = new Vector3(-y, z, x);
            Vector3 worldPoint = sensorPos + localPoint;

            // Color by intensity
            Color color = Color.Lerp(Color.blue, Color.red, intensity);
            Debug.DrawRay(worldPoint, Vector3.up * 0.05f, color, _visualizationDuration);
        }

        Debug.Log($"Point cloud has {pointCloudMsg.width} points, step: {pointCloudMsg.point_step}, size: {pointCloudMsg.data.Length} bytes");
    }

	#endregion
}