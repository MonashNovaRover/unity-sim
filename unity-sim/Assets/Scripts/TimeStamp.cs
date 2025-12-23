using RosSharp.RosBridgeClient.MessageTypes.Std;
using Time = RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time;

/// <summary>
/// Utility class for creating ROS-compatible timestamps and headers.
/// Handles conversion between Unity's time system and ROS time (seconds + nanoseconds).
/// 
/// ROS uses a two-part timestamp:
/// - sec: Integer seconds since epoch
/// - nanosec: Nanoseconds component (0-999,999,999)
/// 
/// This allows for high-precision timing needed for sensor fusion and synchronization.
/// </summary>
public static class TimeStamp
{   
    /// <summary>
    /// Creates a ROS Header with a specific timestamp and frame ID.
    /// Use this when you need to specify an exact time (e.g., for recorded data playback).
    /// </summary>
    public static Header GetHeader(string frameId, double time = -1.0) => new Header(GetTime(time), frameId);

	public static Time GetTime(double time = -1.0)
	{
		var timeInSeconds = time == -1.0 ? Clock.FrameStartTimeInSeconds : time;
		var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        return new Time(secs, nsecs);
	}

    /// <summary>
    /// Converts a ROS Time structure back to floating-point seconds.
    /// Useful for calculating time differences or converting ROS timestamps to Unity time.
    /// </summary>
	public static double ToSeconds(Time stamp)
	{
    	return stamp.sec + stamp.nanosec / 1e9;
	}
}