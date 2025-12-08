using UnityEngine;
using System;
using RosMessageTypes.Geometry;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;

[System.Serializable]
public class ScannerParams
{
    public GameObject laser_sensor_link;
    public float RangeMin = 0;
    public float RangeMax = 1000;

    public float FovH = 360;
    public float FovV = 45;

    public float AngularResH = 1;
    public float AngularResV = 1;
}


public class LiDARPublisher : MonoBehaviour
{
    public string pointsTopic = "/point_cloud";
    public string poseTopic = "/laser_scan_pose";
    
    [SerializeField] private ScannerParams _scannerParams;

    ROSConnection ros;
    LiDARScanner lidarScanner;

    [SerializeField] private double _hz = 20f;
    private double _lastPublishTime;
    private double _publishPeriod => 1.0f / _hz;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PointCloud2Msg>(pointsTopic);
        ros.RegisterPublisher<PoseMsg>(poseTopic);

        CleanParameters();

        lidarScanner = new LiDARScanner(_scannerParams);

        _lastPublishTime = Clock.time + _publishPeriod;
    }

    void CleanParameters()
    {
        _scannerParams.FovH = _scannerParams.FovH <= 360 ? _scannerParams.FovH : 360;
        _scannerParams.FovV = _scannerParams.FovV <= 360 ? _scannerParams.FovV : 360;
    }

    void Update()
    {
        if (Clock.NowTimeInSeconds - _lastPublishTime < _publishPeriod)
            return;

        PointCloud2Msg point_cloud_msg = lidarScanner.getScanMsg();
        
        // Only useful for checking ONE scan
        if (_hz == 1)
            VisualizePointCloud(point_cloud_msg);

        PoseMsg pose_msg = new PoseMsg
        {
            position = new PointMsg(
                _scannerParams.laser_sensor_link.transform.position.x, 
                _scannerParams.laser_sensor_link.transform.position.y, 
                _scannerParams.laser_sensor_link.transform.position.z
                ),
            orientation = new QuaternionMsg(
                _scannerParams.laser_sensor_link.transform.rotation.x, 
                _scannerParams.laser_sensor_link.transform.rotation.y, 
                _scannerParams.laser_sensor_link.transform.rotation.z, 
                _scannerParams.laser_sensor_link.transform.rotation.w
                ),
        };

        ros.Publish(pointsTopic, point_cloud_msg);
        ros.Publish(poseTopic, pose_msg);

        _lastPublishTime = Clock.NowTimeInSeconds;
    }

    void VisualizePointCloud(PointCloud2Msg pointCloudMsg)
    {
        int step = (int)pointCloudMsg.point_step;
        Vector3 sensorPos = _scannerParams.laser_sensor_link.transform.position;

        for (int i = 0; i < (int)pointCloudMsg.width; i++)
        {
            int baseOffset = i * step;

            float x = BitConverter.ToSingle(pointCloudMsg.data, baseOffset);
            float y = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 4);
            float z = BitConverter.ToSingle(pointCloudMsg.data, baseOffset + 8);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            Vector3 localPoint = new Vector3(-x, y, z); // Adjust based on ROS-to-Unity transform
            Vector3 worldPoint = sensorPos + localPoint;

            Color color = Color.Lerp(Color.red, Color.green, i / (float)pointCloudMsg.width);
            Debug.DrawRay(worldPoint, Vector3.up * 0.05f, color, 9999f);
        }

        Debug.Log($"Point cloud has {pointCloudMsg.width} points, step: {pointCloudMsg.point_step}, size: {pointCloudMsg.data.Length} bytes");
    }
}
