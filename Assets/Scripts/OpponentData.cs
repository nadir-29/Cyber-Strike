using UnityEngine;

// One CPU opponent on the fighter select screen: who they are, how they look and how they fight.
[CreateAssetMenu(menuName = "Cyber Strike/Opponent")]
public class OpponentData : ScriptableObject
{
    public string displayName = "CPU";
    [TextArea(2, 6)] public string bio;
    [Tooltip("Difficulty shown as stars on the select card.")]
    [Range(1, 3)] public int difficulty = 1;
    public Sprite portrait;
    [Tooltip("Card frame, stars and name glow.")]
    public Color accent = Color.white;

    [Tooltip("Every sprite of this opponent's fighter and KO sheets; FighterSkin matches them to the base sheet by name.")]
    public Sprite[] sprites;
    public AIProfile aiProfile;
}
