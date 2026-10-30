using UnityEngine;

// Drives a Fighter for the computer opponent: approaches the target, attacks in range,
// sometimes backs off, goes for ability pickups and uses its ability when useful. Like PlayerInputController it only issues inputs; gameplay
// rules stay in Fighter and FighterCombat.
// Decisions are re-evaluated once per reaction interval, so the AI reacts with a delay.
[RequireComponent(typeof(Fighter), typeof(FighterCombat), typeof(Health))]
public class AIController : MonoBehaviour
{
    [SerializeField] Fighter target;

    [Header("Ranges (centre to centre)")]
    [SerializeField] float punchRange = 1.3f;
    [SerializeField] float kickRange = 1.55f;

    [Header("Timing (min, max seconds)")]
    [SerializeField] Vector2 reactionTime = new Vector2(0.15f, 0.35f);
    [SerializeField] Vector2 attackCooldown = new Vector2(0.3f, 0.9f);
    [SerializeField] Vector2 backOffTime = new Vector2(0.4f, 0.9f);

    [Header("Chances")]
    [Tooltip("Chance to attack when in range and ready; otherwise it hesitates.")]
    [SerializeField, Range(0f, 1f)] float attackChance = 0.7f;
    [Tooltip("Chance to kick instead of punch when the punch would also reach.")]
    [SerializeField, Range(0f, 1f)] float kickChance = 0.35f;
    [SerializeField, Range(0f, 1f)] float backOffAfterAttackChance = 0.25f;
    [SerializeField, Range(0f, 1f)] float backOffWhenHitChance = 0.4f;

    [Header("Stamina")]
    [Tooltip("Below this stamina fraction it keeps its distance to recover.")]
    [SerializeField, Range(0f, 1f)] float lowStamina = 0.2f;
    [Tooltip("Stamina fraction at which it goes back on the attack.")]
    [SerializeField, Range(0f, 1f)] float recoveredStamina = 0.6f;
    [SerializeField] float recoverDistance = 3.5f;

    [Header("Pickups and Abilities")]
    [Tooltip("Chance per decision to keep fighting instead of going for a pickup when the target is in range.")]
    [SerializeField, Range(0f, 1f)] float fightOverPickupChance = 0.5f;
    [Tooltip("Chance per decision to use a held ability when it would be useful.")]
    [SerializeField, Range(0f, 1f)] float useAbilityChance = 0.35f;
    [Tooltip("Double Damage and Ice Strike are only used within this distance of the target.")]
    [SerializeField] float abilityUseRange = 3f;
    [Tooltip("Jumps over the target when it stands this close between it and a pickup.")]
    [SerializeField] float jumpOverDistance = 1.6f;

    Fighter fighter;
    FighterCombat combat;
    Health health;
    Stamina stamina;
    FighterAbilities abilities;
    Health targetHealth;
    bool recovering;
    float thinkTimer;
    float attackTimer;
    float backOffTimer;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        health = GetComponent<Health>();
        stamina = GetComponent<Stamina>();
        abilities = GetComponent<FighterAbilities>();
        if (target != null) targetHealth = target.GetComponent<Health>();

        if (target == null)
            Debug.LogWarning($"{name}: Target is not set on AIController, so it will stand still.", this);
    }

    void OnEnable()
    {
        combat.HitTaken += OnHitTaken;
        thinkTimer = Roll(reactionTime);
    }

    void OnDisable()
    {
        combat.HitTaken -= OnHitTaken;
        fighter.SetMoveInput(0f);
    }

    void Update()
    {
        if (target == null || health.IsDead || (targetHealth != null && targetHealth.IsDead))
        {
            fighter.SetMoveInput(0f);
            return;
        }

        float dt = Time.deltaTime;
        attackTimer -= dt;
        backOffTimer -= dt;
        thinkTimer -= dt;
        if (thinkTimer > 0f) return;

        thinkTimer = Roll(reactionTime);
        Think();
    }

    void Think()
    {
        float dx = target.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(dx);
        int toTarget = dx >= 0f ? 1 : -1;

        MaybeUseAbility(distance);

        if (backOffTimer > 0f)
        {
            fighter.SetMoveInput(-toTarget);
            return;
        }

        if (stamina != null)
        {
            if (stamina.Fraction < lowStamina) recovering = true;
            else if (stamina.Fraction >= recoveredStamina) recovering = false;
        }

        if (GoForPickup(distance, toTarget)) return;

        if (recovering)
        {
            fighter.SetMoveInput(distance < recoverDistance ? -toTarget : 0f);
            if (distance >= recoverDistance) fighter.Face(toTarget);
            return;
        }

        // An armed Ice Strike only triggers on a punch, so close in to punch range.
        bool iceArmed = abilities != null && abilities.IceStrikeArmed;
        if (distance > (iceArmed ? punchRange : kickRange))
        {
            fighter.SetMoveInput(toTarget);
            return;
        }

        fighter.SetMoveInput(0f);
        fighter.Face(toTarget);

        if (attackTimer > 0f || !combat.CanAct) return;
        attackTimer = Roll(attackCooldown);
        if (Random.value > attackChance) return;

        if (!iceArmed && (distance > punchRange || Random.value < kickChance))
            combat.Kick();
        else
            combat.Punch();

        if (Random.value < backOffAfterAttackChance)
            backOffTimer = Roll(backOffTime);
    }

    // Heads for the nearest pickup when the ability slot is empty. Returns true if it did.
    bool GoForPickup(float distance, int toTarget)
    {
        if (abilities == null || abilities.Held != AbilityType.None) return false;

        AbilityPickup nearest = null;
        float best = float.MaxValue;
        foreach (AbilityPickup pickup in AbilityPickup.Active)
        {
            float d = Mathf.Abs(pickup.transform.position.x - transform.position.x);
            if (d < best)
            {
                best = d;
                nearest = pickup;
            }
        }
        if (nearest == null) return false;
        if (distance <= kickRange && !recovering && Random.value < fightOverPickupChance) return false;

        int toPickup = nearest.transform.position.x >= transform.position.x ? 1 : -1;
        fighter.SetMoveInput(toPickup);

        // The target stands between us and the pickup: jump over it.
        if (toPickup == toTarget && distance < best && distance < jumpOverDistance)
            fighter.Jump();
        return true;
    }

    void MaybeUseAbility(float distance)
    {
        if (abilities == null || abilities.Held == AbilityType.None) return;
        if (Random.value > useAbilityChance) return;

        bool useful = abilities.Held switch
        {
            AbilityType.Healing => health.Max - health.Current >= abilities.HealAmount,
            AbilityType.DoubleDamage => distance <= abilityUseRange && !abilities.DoubleDamageActive,
            AbilityType.IceStrike => distance <= abilityUseRange && !abilities.IceStrikeArmed,
            AbilityType.AbilitySteal => abilities.Opponent != null && abilities.Opponent.Held != AbilityType.None,
            _ => false,
        };
        if (useful) abilities.UseAbility();
    }

    void OnHitTaken()
    {
        if (Random.value < backOffWhenHitChance)
            backOffTimer = Roll(backOffTime);
    }

    static float Roll(Vector2 range) => Random.Range(range.x, range.y);
}
