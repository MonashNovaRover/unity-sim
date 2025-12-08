using System;
using UnityEngine;

public static class Clock
{
    public enum ClockMode
    {
        UnityScaled
    }

    static readonly DateTime k_UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
    
    static double SecondsSinceUnixEpoch => (DateTime.Now - k_UnixEpoch).TotalSeconds;
    static double UnityUnscaledTimeSinceFrameStart => 
        Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;

    public static double FrameStartTimeInSeconds
    {
        get
        {
            return Mode switch
            {
                ClockMode.UnityScaled => Time.timeAsDouble,
                _ => throw new NotImplementedException()
            };
        }
    }

    public static double NowTimeInSeconds
    {
        get
        {
            return Mode switch
            {
                ClockMode.UnityScaled => Time.timeAsDouble + UnityUnscaledTimeSinceFrameStart * Time.timeScale,
                _ => throw new NotImplementedException()
            };
        }
    }
    
    public static float DeltaTimeInSeconds
    {
        get
        {
            return Mode switch
            {
                ClockMode.UnityScaled => Time.deltaTime,
                _ => Time.unscaledDeltaTime,
            };
        }
    }

    public static ClockMode Mode = ClockMode.UnityScaled;

    public static double Now => NowTimeInSeconds;
    public static double time => FrameStartTimeInSeconds;
}