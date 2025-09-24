using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosUtils;
using UnityEngine;
using UnitySensors.DataType.LiDAR;
using UnitySensors.Sensor.LiDAR;
using UnitySensors.Utils.PointCloud;
using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Std;

namespace LiDAR {
    public class LiDARPublisher : MonoBehaviour
    {
        [Header("References")]
        public LiDARSensor lidar; 
        
        [Space(9)]
        public ScanPattern scanPattern;

        [Header("ROS2")]
        // public Publisher<LaserScan> publisher = new("/livox/lidar");
        public Publisher<PointCloud2> cloudPublisher = new("/livox/point_cloud");
        public string frameId = "livox_frame";
        
        [Header("Scan")]
        [SerializeField]
        private float _minRange = 0.1f;
        [SerializeField]
        private float _maxRange = 70.0f;
        [SerializeField]
        private float _gaussianNoiseSigma = 0.02f;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (!lidar)
                TryGetComponent(out lidar);

            if (!lidar)
            {
                Debug.LogError("Failed to get the LiDAR component");
                return;
            }
            
            Debug.Log("LiDAR publisher started.");
            lidar.onSensorUpdateComplete += Publish;
        }

        // Update is called once per frame
        void Update()
        {
            // if (lidar && lidar.isActiveAndEnabled)
            //     Publish();
        }

        // void Publish()
        // {
        //     var msg = new LaserScan
        //     {
        //         header = new Header
        //         {
        //             stamp = RosTime.Now(),
        //             frame_id = frameId
        //         },
        //         
        //         angle_min = scanPattern.minAzimuthAngle,
        //         angle_max = scanPattern.maxAzimuthAngle,
        //         angle_increment = (scanPattern.maxAzimuthAngle - scanPattern.minAzimuthAngle) / lidar.pointsNum,
        //         time_increment = 0.0f,
        //         scan_time = 0.0f,
        //         range_min = _minRange,
        //         range_max = _maxRange,
        //         ranges = new float[lidar.pointsNum],
        //         intensities = new float[lidar.pointsNum],
        //     };
        //     
        //     for (var i = 0; i < lidar.pointsNum; i++)
        //     {
        //         var point = lidar.pointCloud.points[i];
        //         msg.ranges[i] = Mathf.Sqrt(point.position.x * point.position.x + point.position.z * point.position.z);
        //         if (msg.ranges[i] < _minRange || msg.ranges[i] > _maxRange)
        //         {
        //             msg.ranges[i] = float.MaxValue;
        //         }
        //         else if (!float.IsFinite(msg.ranges[i]))
        //         {
        //             msg.ranges[i] = float.MaxValue;
        //         }
        //         else
        //         {
        //             var u1 = 1.0 - UnityEngine.Random.Range(0f, 1f); //uniform(0,1] random doubles
        //             var u2 = 1.0 - UnityEngine.Random.Range(0f, 1f);
        //             var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
        //                                 Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
        //             msg.ranges[i] += (float)randStdNormal * _gaussianNoiseSigma; //random normal(mean,stdDev^2)
        //         }
        //         msg.intensities[i] = point.intensity;
        //     }
        //
        //     publisher.Publish(msg);
        // }

        void Publish()
        {
            var pointCount = lidar.pointsNum;
            const int pointStep = 4 * 4; //< 3 floats * 4 bytes
            var data = new byte[pointCount * pointStep];
            
            for (var i = 0; i < pointCount; i++)
            {
                var offset = i * pointStep;
                var p = lidar.pointCloud.points[i];
        
                // BitConverter returns bytes in little-endian order on most platforms
                Array.Copy(BitConverter.GetBytes(p.position.x), 0, data, offset, 4);
                Array.Copy(BitConverter.GetBytes(p.position.z), 0, data, offset + 4, 4);
                Array.Copy(BitConverter.GetBytes(p.position.y), 0, data, offset + 8, 4);
                Array.Copy(BitConverter.GetBytes(p.intensity), 0, data, offset + 12, 4);
            }
        
            var msg = new PointCloud2
            {
                header =
                {
                    stamp = RosTime.Now(),
                    frame_id = frameId
                },
                fields = new[]
                {
                    new PointField("x", 0, PointField.FLOAT32, 1),
                    new PointField("y", 4, PointField.FLOAT32, 1),
                    new PointField("z", 8, PointField.FLOAT32, 1),
                    new PointField("i", 12, PointField.FLOAT32, 1),
                },
                is_bigendian = false,
                is_dense = true, //< are we certain there aren't any NaN or Inf values?
                
                width = (uint) pointCount,
                height = 1,
                
                row_step = (uint) (pointStep * pointCount), //< point step * width
                point_step = pointStep,
                data = data,
            };
        
            cloudPublisher.Publish(msg);
        }
    }
}
