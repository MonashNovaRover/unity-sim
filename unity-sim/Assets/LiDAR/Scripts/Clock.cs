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
                // This might be an approximation... needs testing.
                ClockMode.UnityScaled => Time.timeAsDouble,
                // ClockMode.UnityUnscaled => Time.unscaledTimeAsDouble,
                // ClockMode.UnixEpoch => k_StartTimeEpochSeconds + UnityUnscaledTimeSinceFrameStart,
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
                // ClockMode.UnityUnscaled => Time.realtimeSinceStartupAsDouble,
                // ClockMode.UnixEpoch => SecondsSinceUnixEpoch,
                _ => throw new NotImplementedException()
            };
        }
    }
    
    // NOTE: Precision loss vs. other time measurements due to no deltaTimeAsDouble interface
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

    // Simple interfaces for supporting commonly used vocabulary
    public static double Now => NowTimeInSeconds;
    public static double time => FrameStartTimeInSeconds;
}