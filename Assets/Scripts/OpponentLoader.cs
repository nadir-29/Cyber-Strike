using UnityEngine;

// Sets up the CPU fighter for the opponent picked on the select screen: its skin, AI profile,
// stat bonus and the name above its health bar. Runs before the HUD reads health in Start.
[DefaultExecutionOrder(-50)]
public class OpponentLoader : MonoBehaviour
{
    [SerializeField] OpponentRoster roster;

    [Header("CPU Fighter")]
    [SerializeField] FighterSkin skin;
    [SerializeField] AIController ai;
    [SerializeField] Health health;
    [SerializeField] FighterCombat combat;

    [Header("HUD")]
    [SerializeField] PixelText nameLabel;

    public OpponentData Opponent { get; private set; }

    void Awake()
    {
        if (roster == null)
        {
            Debug.LogWarning($"{name}: Roster is not set on OpponentLoader, so the default CPU fighter is used.", this);
            return;
        }

        Opponent = roster.SelectedOrDefault;
        if (skin != null) skin.SetSkin(Opponent.sprites);

        AIProfile profile = Opponent.aiProfile;
        if (profile != null)
        {
            if (ai != null) ai.SetProfile(profile);
            if (health != null && profile.bonusHealth > 0) health.SetMax(health.Max + profile.bonusHealth);
            if (combat != null) combat.DamageScale = profile.damageMultiplier;
        }

        if (nameLabel != null)
        {
            nameLabel.Text = Opponent.displayName;
            nameLabel.GlowColor = new Color(Opponent.accent.r, Opponent.accent.g, Opponent.accent.b, 0.9f);
            nameLabel.Rebuild();
        }
    }
}
