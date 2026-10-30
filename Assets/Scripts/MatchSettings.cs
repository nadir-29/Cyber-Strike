using UnityEngine;

// Match options picked on the fighter select screen and handed to the Arena. Static, like
// OpponentRoster.Selected, so Restart keeps them.
public static class MatchSettings
{
    public const int MinHearts = 1;
    public const int MaxHearts = 5;
    public const int DefaultHearts = 3;

    static int hearts = DefaultHearts;

    // Hearts (lives) each fighter starts the match with.
    public static int Hearts
    {
        get => hearts;
        set => hearts = Mathf.Clamp(value, MinHearts, MaxHearts);
    }

    // Domain reload is disabled for Play mode: start each Play session from the default.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSettings() => hearts = DefaultHearts;
}
