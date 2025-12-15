using RosMessageTypes.Geometry;
using RosMessageTypes.BuiltinInterfaces;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

using UnityTransform = UnityEngine.Transform;
using UnityVector3 = UnityEngine.Vector3;
using UnityQuaternion = UnityEngine.Quaternion;

using RosTransform = RosSharp.RosBridgeClient.MessageTypes.Geometry.Transform;
using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;

namespace RosUtils
{
    public static class TransformExtensions
    {
        public static RosTransform ToFLU(UnityEngine.Transform t)
        {
            UnityEngine.Vector3 p = t.position;
            UnityEngine.Quaternion q = t.rotation;

            return new RosTransform
            {
                translation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 { x = p.z, y = -p.x, z = p.y },
                rotation    = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion { x = q.x, y = q.y, z = q.z, w = q.w }
            };
        }

		public static TransformMsg ToROSTransform(this UnityTransform tfUnity)
        {
            return new TransformMsg(
                tfUnity.localPosition.To<FLU>(),
                tfUnity.localRotation.To<FLU>());
        }

        static TimeMsg GetStamp(double time)
        {
            var timeInSeconds = time;
            var secs = (int)timeInSeconds;
            var nsecs = (uint)((timeInSeconds - secs) * 1e9);
            return new TimeMsg(secs, nsecs);
        }

        public static RosTransform ToRosSharpTransform(this UnityTransform unityTransform)
        {
            return new RosTransform
            {
                translation = unityTransform.position.Vector3Msg(),
                rotation = unityTransform.rotation.QuaternionMsg()
            };
        }

        public static RosVector3 Vector3Msg(this UnityVector3 vec)
        {
            return new RosVector3 { x = vec.x, y = vec.y, z = vec.z };
        }

        public static RosQuaternion QuaternionMsg(this UnityQuaternion quat)
        {
            return new RosQuaternion { x = quat.x, y = quat.y, z = quat.z, w = quat.w };
        }

        public static TransformStamped ToRosTransformStamped(this UnityTransform unityTransform, double time)
        {
            return new TransformStamped
            {
                header = TimeStamp.GetHeader(time, unityTransform.parent.gameObject.name),
                child_frame_id = unityTransform.gameObject.name,
                transform = unityTransform.ToRosSharpTransform()
            };
        }
    }
}
