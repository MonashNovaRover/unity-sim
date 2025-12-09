using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RTransform = RosSharp.RosBridgeClient.MessageTypes.Geometry.Transform;

namespace RosUtils
{
    public static class TransformExtensions
    {
        public static RTransform ToFLU(UnityEngine.Transform t)
        {
            UnityEngine.Vector3 p = t.position;
            UnityEngine.Quaternion q = t.rotation;

            return new RTransform
            {
                translation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 
                {
                    x = p.x,
                    y = p.y,
                    z = p.z,
                },
                rotation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion
                {
                    x = q.x,
                    y = q.y,
                    z = q.z,
                    w = q.w
                }
            };
        }
    }
}
