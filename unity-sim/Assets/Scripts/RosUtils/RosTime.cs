using System;
using UnityEngine;
using TimeMsg = RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time;

namespace RosUtils
{
    /// <summary>
    /// Holds static utility methods
    /// </summary>
    public static class RosTime
    {
        /// <summary>
        /// Creates a new ROS2 time message from a given double
        /// </summary>
        /// <param name="time">The elapsed time in seconds</param>
        /// <returns>A ROS2 Time message</returns>
        public static TimeMsg ToTimeMsg(double time)
        {
            var sec = (int)Math.Floor(time);
            var nanosec = (uint)Math.Min((time - sec) * 1000000000, 999999999);

            return new TimeMsg
            {
                sec = sec,
                nanosec = nanosec
            };
        }
        
        /// <summary>
        /// Gets the current time as a ROS2 Time message
        /// </summary>
        /// <returns>A ROS2 Time message for the current time</returns>
        public static TimeMsg Now() => ToTimeMsg(Time.timeAsDouble);
    }
}