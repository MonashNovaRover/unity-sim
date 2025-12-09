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
    
    public LiDARScanner(ScannerParams p)
    {
        _p = p;
            
        _frameId = p.LidarLink.name;

        float scanAngStartH = -p.FovH / 2;
        float scanAngEndH = p.FovH / 2;
        float scanAngStartV = -p.FovV / 2;
        float scanAngEndV = p.FovV / 2;

        _measurementsPerScanH = Mathf.FloorToInt((scanAngEndH - scanAngStartH) / p.AngularResH) + 1;
        _measurementsPerScanV = Mathf.FloorToInt((scanAngEndV - scanAngStartV) / p.AngularResH) + 1;

        if (p.FovH == 360)
        {
            _measurementsPerScanH = _measurementsPerScanH -1;
        }

        if (p.FovV == 360)
        {
            _measurementsPerScanV = _measurementsPerScanV -1;
        }
        
        _scanArrayH = new float[_measurementsPerScanH];
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            _scanArrayH[i] = scanAngStartH + i * p.AngularResH;
        }

        _measurementsPerScanV = Mathf.FloorToInt((scanAngEndV - scanAngStartV) / p.AngularResV) + 1;
        _scanArrayV = new float[_measurementsPerScanV];
        for (int i = 0; i < _measurementsPerScanV; i++)
        {
            _scanArrayV[i] = scanAngStartV + i * p.AngularResV;
        }

        _numPoints = (uint)(_measurementsPerScanH * _measurementsPerScanV);

        _raw_data_len = 16 * _numPoints;
        _raw_data = new byte[_raw_data_len];
    }

    public PointCloud2 getScanMsg()
    {
        Transform sensor_transform = _p.LidarLink.transform;
        Vector3 sensorPos = sensor_transform.position;
        Quaternion sensorRot = sensor_transform.rotation;

        NativeArray<RaycastCommand> commands = new((int)_numPoints, Allocator.TempJob);
        int idx = 0;
        for (int i = 0; i < _measurementsPerScanH; i++)
        {
            float theta = Mathf.Deg2Rad * _scanArrayH[i];

            for (int j = 0; j < _measurementsPerScanV; j++)
            {
                float psi = Mathf.Deg2Rad * _scanArrayV[j];
                Vector3 localDirVec = new Vector3 (
                    Mathf.Cos(psi) * Mathf.Sin(theta),
                    -Mathf.Sin(psi),
                    Mathf.Cos(psi) * Mathf.Cos(theta)
                );
                
                Vector3 directionVector = sensorRot * localDirVec;
                Vector3 origin = _p.RangeMin * directionVector + sensorPos;
                commands[idx++] = new RaycastCommand(origin, directionVector, new QueryParameters(Physics.DefaultRaycastLayers, false), _p.RangeMax);
            }
        }

        NativeArray<RaycastHit> results = new((int)_numPoints, Allocator.TempJob);
        JobHandle raycastJob = RaycastCommand.ScheduleBatch(commands, results, 128);
        raycastJob.Complete();

        int raw_data_indx = 0;
        for (int k = 0; k < results.Length; k++)
        {
            RaycastHit hit = results[k];
            int baseOffset = raw_data_indx * 16;

            float x, y, z, intensity;
            if (hit.collider != null)
            {
                Vector3 delta = hit.point - sensorPos;
                z = delta.z;
                x = -delta.x;
                y = delta.y;
            }
            else
            {
                x = y = z = float.NaN;
            }

			intensity = 0.0f;

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
            header = TimeStamp.GetHeader(_frameId),
            height = 1,
            width = _numPoints,
            fields = new PointField[]
            {
                new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "i", offset = 12, datatype = PointField.FLOAT32, count = 1 }
            },
            is_bigendian = false,
            point_step = 16,
            row_step = _raw_data_len,
            data = _raw_data,
            is_dense = false
        };
    }
}
