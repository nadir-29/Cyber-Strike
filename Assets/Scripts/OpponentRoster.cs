using UnityEngine;

// The CPU opponents in select-screen order, plus the one-shot hand-off of the chosen opponent
// from the select screen to the match. The choice survives scene reloads, so Restart keeps it.
[CreateAssetMenu(menuName = "Cyber Strike/Opponent Roster")]
public class OpponentRoster : ScriptableObject
{
    [SerializeField] OpponentData[] opponents;
    [Tooltip("Used when the Arena loads with no opponent picked (index 0 = Easy).")]
    [SerializeField] int defaultIndex;

    public OpponentData[] Opponents => opponents;

    // Set by the select screen; null until an opponent has been picked this session.
    public static OpponentData Selected { get; set; }

    // Domain reload is disabled for Play mode, so statics survive between Play sessions in the
    // editor. Clear the pick on each launch so a new session never inherits the last opponent.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSelection() => Selected = null;

    public OpponentData SelectedOrDefault =>
        Selected != null ? Selected : opponents[Mathf.Clamp(defaultIndex, 0, opponents.Length - 1)];
}
