using UnityEngine;

// Difficulty tuning for one CPU opponent. AIController reads the behaviour values; the stat bonus
// is applied to Health / FighterCombat by OpponentLoader, so gameplay rules stay out of the AI.
[CreateAssetMenu(menuName = "Cyber Strike/AI Profile")]
public class AIProfile : ScriptableObject
{
    [Header("Reactions")]
    [Tooltip("Seconds between decisions (min, max). Lower reacts faster.")]
    public Vector2 reactionTime = new Vector2(0.15f, 0.35f);

    [Header("Attacking")]
    [Tooltip("Seconds between attack attempts (min, max).")]
    public Vector2 attackCooldown = new Vector2(0.3f, 0.9f);
    [Tooltip("Chance to attack when in range and ready; otherwise it hesitates.")]
    [Range(0f, 1f)] public float attackChance = 0.7f;
    [Tooltip("Chance to kick instead of punch when the punch would also reach.")]
    [Range(0f, 1f)] public float kickChance = 0.35f;

    [Header("Retreating")]
    [Range(0f, 1f)] public float backOffAfterAttackChance = 0.25f;
    [Range(0f, 1f)] public float backOffWhenHitChance = 0.4f;
    [Tooltip("Seconds it keeps backing off (min, max).")]
    public Vector2 backOffTime = new Vector2(0.4f, 0.9f);

    [Header("Pickups")]
    [Tooltip("Seconds a pickup has to be in the arena before the AI notices it.")]
    [Min(0f)] public float pickupNoticeDelay = 0.6f;
    [Tooltip("Chance per decision to keep fighting instead of going for a pickup when the target is in range.")]
    [Range(0f, 1f)] public float fightOverPickupChance = 0.5f;

    [Header("Abilities")]
    [Tooltip("Chance per decision to consider using a held ability.")]
    [Range(0f, 1f)] public float useAbilityChance = 0.35f;
    [Tooltip("1 = only uses an ability when it helps; 0 = fires it off as soon as it considers it.")]
    [Range(0f, 1f)] public float abilitySmarts = 0.7f;

    [Header("Stat Bonus")]
    [Tooltip("Added to the fighter's max HP.")]
    [Min(0)] public int bonusHealth;
    [Tooltip("Multiplies all damage this fighter deals.")]
    [Min(0.1f)] public float damageMultiplier = 1f;
}
