using UnityEngine;

// Single owner of Time.timeScale, so the knockout slow motion and hit-pause can overlap
// without one restoring the wrong value over the other.
public static class TimeControl
{
    static float slowMotion = 1f;
    static bool hitPaused;
    static float defaultFixedDeltaTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        slowMotion = 1f;
        hitPaused = false;
        defaultFixedDeltaTime = -1f;
    }

    // Back to normal speed, e.g. when a scene is (re)loaded mid slow motion or hit-pause.
    public static void ResetAll()
    {
        slowMotion = 1f;
        hitPaused = false;
        Apply();
    }

    public static void SetSlowMotion(float scale)
    {
        slowMotion = scale;
        Apply();
    }

    public static void SetHitPaused(bool paused)
    {
        hitPaused = paused;
        Apply();
    }

    static void Apply()
    {
        if (defaultFixedDeltaTime < 0f) defaultFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = hitPaused ? 0f : slowMotion;
        Time.fixedDeltaTime = defaultFixedDeltaTime * slowMotion;
    }
}
