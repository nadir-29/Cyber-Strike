using UnityEngine;

// The CPU opponents in select-screen order, plus the one-shot hand-off of the chosen opponent
// from the select screen to the match. The choice survives scene reloads, so Restart keeps it.
[CreateAssetMenu(menuName = "Cyber Strike/Opponent Roster")]
public class OpponentRoster : ScriptableObject
{
    [SerializeField] OpponentData[] opponents;
    [Tooltip("Used when the Arena is played directly without going through the select screen.")]
    [SerializeField] int defaultIndex = 1;

    public OpponentData[] Opponents => opponents;

    // Set by the select screen; null until an opponent has been picked this session.
    public static OpponentData Selected { get; set; }

    public OpponentData SelectedOrDefault =>
        Selected != null ? Selected : opponents[Mathf.Clamp(defaultIndex, 0, opponents.Length - 1)];
}
