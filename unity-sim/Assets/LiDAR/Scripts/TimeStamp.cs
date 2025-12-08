using System;
using RosMessageTypes.BuiltinInterfaces;

namespace Unity.Robotics.Core
{
    public readonly struct TimeStamp
    {
        public const long k_NanosecondsinSecond = 1_000_000_000L;
        public readonly int Seconds;
        public readonly uint NanoSeconds;
        public long TotalNanoseconds => (long)Seconds * k_NanosecondsinSecond + NanoSeconds;

        public TimeStamp(double timeInSeconds)
        {
            double fractionalSeconds = timeInSeconds % 1.0;
            Seconds = (int)Math.Floor(timeInSeconds);
            NanoSeconds = (uint)(fractionalSeconds * k_NanosecondsInSecond);
        }

        public TimeStamp(int sec, uint nsec)
        {
            Seconds = sec;
            Nanoseconds = nsec;
        }

        public static implicit operator TimeMsg(TimeStamp stamp)
        {
            return new TimeMsg(stamp.Seconds, stamp.Nanoseconds);
        }

        public static implicit operator TimeStamp(TimeMsg timeMsg)
        {
            return new TimeStamp(timeMsg.sec, timeMsg.nanosec);
        }
    }
}