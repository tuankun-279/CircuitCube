using UnityEngine;

namespace CircuitCube
{
    /// <summary>
    /// The real-time clock the game animates with. It is unscaled time, except while frames are captured at a fixed
    /// rate (Time.captureDeltaTime, used to record previews), where it follows the captured frames so slow frame
    /// grabs do not make effects skip ahead.
    /// </summary>
    public static class Clock
    {
        public static float Now => Time.captureDeltaTime > 0f ? Time.time : Time.unscaledTime;
        public static float Dt => Time.captureDeltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
    }
}
