using UnityEngine;
using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces;
using PoseStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.PoseStamped;
using RosUtils;

[System.Serializable] 
public class ScannerParams
{
    public GameObject LidarLink;
    public float RangeMin = 0;
    public float RangeMax = 1000;

    public float FovH = 360;
    public float FovV = 45;

    public float AngularResH = 1;
    public float AngularResV = 1;
}

[DefaultExecutionOrder(200)]
public class LiDARPublisher : MonoBehaviour
{
    public string pointsTopic = "/point_cloud";
    public string poseTopic = "/laser_scan_pose";
    
    [SerializeField] private ScannerParams _scannerParams;

    [SerializeField] private double _hz = 20f;
    private double _lastPublishTime;
    private double _publishPeriod => 1.0f / _hz;

	private LiDARScanner _lidarScanner;
	private Publisher<PointCloud2> _pcPublisher;
	private Publisher<PoseStamped> _posePublisher;
	private int _messageCount;

    void Start()
    {   
		CleanParameters();

        _lidarScanner = new LiDARScanner(_scannerParams);

		_pcPublisher = new Publisher<PointCloud2>(pointsTopic);
		_posePublisher = new Publisher<PoseStamped>(poseTopic);

        _lastPublishTime = Clock.time + _publishPeriod;
    }

    void CleanParameters()
    {
        _scannerParams.FovH = _scannerParams.FovH <= 360 ? _scannerParams.FovH : 360;
        _scannerParams.FovV = _scannerParams.FovV <= 360 ? _scannerParams.FovV : 360;
    }

    void LateUpdate()
    {
	    double stampTime = Clock.FrameStartTimeInSeconds;
		
        if (stampTime - _lastPublishTime < _publishPeriod)
            return;

        PointCloud2 pointCloudMsg = _lidarScanner.GetScanMsg(stampTime);

        // Only useful for checking ONE scan
        if (_hz == 1)
            VisualizePointCloud(pointCloudMsg);

        var pos = _scannerParams.LidarLink.transform.position;
		var rot = _scannerParams.LidarLink.transform.rotation;

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
        
        _pcPublisher.Publish(pointCloudMsg);

		_lastPublishTime = stampTime;
    }

	private void OnDestroy()
    {
        _pcPublisher?.Dispose();
		_posePublisher?.Dispose();
    }
	
	# region "debugging"

    void VisualizePointCloud(PointCloud2 pointCloudMsg)
    {
        int step = (int)pointCloudMsg.point_step;
        Vector3 sensorPos = _scannerParams.LidarLink.transform.position;

        for (int i = 0; i < (int)pointCloudMsg.width; i++)
        {
            int baseOffset = i * step;

            float x = BitConverter.ToSingle(pointCloudMsg.data, baseOffset);
            float y = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 4);
            float z = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 8);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            Vector3 localPoint = new Vector3(-y, z, x);
            Vector3 worldPoint = sensorPos + localPoint;

            Color color = Color.Lerp(Color.blue, Color.red, Vector3.Distance(worldPoint, sensorPos) / 5f);
            Debug.DrawRay(worldPoint, Vector3.up * 0.05f, color, 9999f);
        }

        Debug.Log($"Point cloud has {pointCloudMsg.width} points, step: {pointCloudMsg.point_step}, size: {pointCloudMsg.data.Length} bytes");
    }

	# endregion
}
