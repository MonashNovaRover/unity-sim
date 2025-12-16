using System;
using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using Time = RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time;

public static class TimeStamp
{
    public static Header GetHeader(string frameId)
    {
        var timeInSeconds = Clock.FrameStartTimeInSeconds;
        var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        var stamp = new Time(secs, nsecs);

        return new Header(stamp, frameId);
    }
    
    public static Header GetHeader(double time, string frameId)
    {
        var timeInSeconds = time;
        var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        var stamp = new Time(secs, nsecs);

        return new Header(stamp, frameId);
    }

	public static double ToSeconds(Time stamp)
	{
    	return stamp.sec + stamp.nanosec / 1e9;
	}
}