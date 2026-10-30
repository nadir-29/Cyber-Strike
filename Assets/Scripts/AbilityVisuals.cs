using UnityEngine;

// Tints the fighter to show ability effects: a flash on pickup/use, a pulse while
// Double Damage is active or Ice Strike is armed, and icy blue while slowed. Visual only.
[RequireComponent(typeof(SpriteRenderer), typeof(FighterAbilities), typeof(Health))]
public class AbilityVisuals : MonoBehaviour
{
    [SerializeField] AbilityCatalog catalog;
    [SerializeField] Color slowedColor = new Color(0.55f, 0.8f, 1f);
    [SerializeField] float pulseSpeed = 10f;
    [SerializeField] float flashTime = 0.35f;

    SpriteRenderer sprite;
    FighterAbilities abilities;
    Health health;
    Fighter fighter;
    Color baseColor;
    Color flashColor;
    float flashTimer;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        abilities = GetComponent<FighterAbilities>();
        health = GetComponent<Health>();
        fighter = GetComponent<Fighter>();
        baseColor = sprite.color;
    }

    void OnEnable()
    {
        abilities.HeldChanged += OnHeldChanged;
        abilities.Used += Flash;
    }

    void OnDisable()
    {
        abilities.HeldChanged -= OnHeldChanged;
        abilities.Used -= Flash;
    }

    void OnHeldChanged(AbilityType type)
    {
        if (type != AbilityType.None) Flash(type);
    }

    void Flash(AbilityType type)
    {
        flashColor = ColorOf(type);
        flashTimer = flashTime;
    }

    void LateUpdate()
    {
        if (health.IsDead) return; // KnockoutEffect owns the colour from here

        Color color = baseColor;
        if (fighter != null && fighter.IsSlowed) color = slowedColor;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        if (abilities.DoubleDamageActive)
            color = Color.Lerp(color, ColorOf(AbilityType.DoubleDamage), 0.25f + 0.35f * pulse);
        if (abilities.IceStrikeArmed)
            color = Color.Lerp(color, ColorOf(AbilityType.IceStrike), 0.25f + 0.35f * pulse);

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            color = Color.Lerp(color, flashColor, Mathf.Clamp01(flashTimer / flashTime));
        }
        sprite.color = color;
    }

    Color ColorOf(AbilityType type)
    {
        AbilityCatalog.Entry entry = catalog != null ? catalog.Get(type) : null;
        return entry != null ? entry.color : Color.white;
    }
}
