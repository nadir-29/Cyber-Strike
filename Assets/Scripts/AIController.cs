using UnityEngine;

// Drives a Fighter for the computer opponent: approaches the target (dashing in from afar), attacks in range,
// sometimes backs off, goes for ability pickups, uses its ability when useful, fires its special when the
// meter is full and dashes away from incoming attacks.
// It plays by the same facing rules as the player: it can't turn on the ground, so when the target gets behind
// it, it jumps (after its turn reaction time) and steers toward the target mid-air to turn around. It only
// attacks, blocks and fires its special while facing the target. Like PlayerInputController it only issues inputs; gameplay
// rules stay in Fighter and FighterCombat.
// Decisions are re-evaluated once per reaction interval, so the AI reacts with a delay.
// An AIProfile (set in the Inspector or by OpponentLoader) overrides the tuning below per opponent.
[RequireComponent(typeof(Fighter), typeof(FighterCombat), typeof(Health))]
public class AIController : MonoBehaviour
{
    [SerializeField] Fighter target;
    [Tooltip("Difficulty tuning; overrides the values below when set.")]
    [SerializeField] AIProfile profile;

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
    [Tooltip("Seconds a pickup has to be in the arena before the AI notices it.")]
    [SerializeField, Min(0f)] float pickupNoticeDelay = 0.6f;
    [Tooltip("Chance per decision to consider using a held ability.")]
    [SerializeField, Range(0f, 1f)] float useAbilityChance = 0.35f;
    [Tooltip("1 = only uses an ability when it helps; 0 = fires it off as soon as it considers it.")]
    [SerializeField, Range(0f, 1f)] float abilitySmarts = 0.7f;
    [Tooltip("Double Damage and Ice Strike are only used within this distance of the target.")]
    [SerializeField] float abilityUseRange = 3f;
    [Tooltip("Jumps over the target when it stands this close between it and a pickup.")]
    [SerializeField] float jumpOverDistance = 1.6f;

    [Header("Special and Dash")]
    [Tooltip("Chance per decision to consider the special when the meter is full.")]
    [SerializeField, Range(0f, 1f)] float specialChance = 0.6f;
    [Tooltip("1 = only fires it in range at an open target; 0 = also throws it into a guard or from a bit too far.")]
    [SerializeField, Range(0f, 1f)] float specialSmarts = 0.6f;
    [SerializeField] float specialRange = 1.4f;
    [Tooltip("Chance per decision to dash in when approaching the target or a pickup from farther than Dash In Distance.")]
    [SerializeField, Range(0f, 1f)] float dashInChance = 0.25f;
    [SerializeField] float dashInDistance = 4f;
    [Tooltip("Chance to dash away from an attack the target starts in range (doubled against its special).")]
    [SerializeField, Range(0f, 1f)] float dodgeChance = 0.15f;
    [Tooltip("Seconds a dodge stays pending after the reaction delay, waiting for the fighter to be free to dash.")]
    [SerializeField] float dodgeWindow = 0.3f;

    [Header("Turning")]
    [Tooltip("Seconds after the target gets behind it before it jumps to turn around (min, max).")]
    [SerializeField] Vector2 turnReactionTime = new Vector2(0.25f, 0.5f);

    [Header("Blocking")]
    [Tooltip("Chance to raise the guard when the target starts an attack in range or after taking a hit (a quarter of it, per decision, to guard pre-emptively when close).")]
    [SerializeField, Range(0f, 1f)] float blockChance = 0.35f;
    [Tooltip("Seconds after seeing the attack before it raises the guard (min, max).")]
    [SerializeField] Vector2 blockReactionTime = new Vector2(0.12f, 0.25f);
    [Tooltip("Seconds it holds the guard before dropping it (min, max), so it never blocks forever.")]
    [SerializeField] Vector2 blockHoldTime = new Vector2(0.35f, 0.7f);
    [Tooltip("Seconds after dropping the guard before it can block again.")]
    [SerializeField, Min(0f)] float blockCooldown = 0.9f;
    [Tooltip("Reacts to attacks started within this distance.")]
    [SerializeField] float blockRange = 2f;

    Fighter fighter;
    FighterCombat combat;
    Health health;
    Stamina stamina;
    FighterAbilities abilities;
    SpecialMeter meter;
    Health targetHealth;
    FighterCombat targetCombat;
    bool recovering;
    float thinkTimer;
    float attackTimer;
    float backOffTimer;
    float blockDelayTimer = -1f;
    float blockHoldTimer;
    float blockCooldownTimer;
    bool guarding;
    bool dodgePending;
    float dodgeTimer;
    float turnTimer = -1f;
    bool headingForPickup;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        health = GetComponent<Health>();
        stamina = GetComponent<Stamina>();
        abilities = GetComponent<FighterAbilities>();
        meter = GetComponent<SpecialMeter>();
        if (target != null)
        {
            targetHealth = target.GetComponent<Health>();
            targetCombat = target.GetComponent<FighterCombat>();
        }
        if (profile != null) SetProfile(profile);

        if (target == null)
            Debug.LogWarning($"{name}: Target is not set on AIController, so it will stand still.", this);
    }

    // Copies a difficulty profile's behaviour values into this controller.
    public void SetProfile(AIProfile newProfile)
    {
        profile = newProfile;
        if (profile == null) return;

        reactionTime = profile.reactionTime;
        attackCooldown = profile.attackCooldown;
        attackChance = profile.attackChance;
        kickChance = profile.kickChance;
        backOffAfterAttackChance = profile.backOffAfterAttackChance;
        backOffWhenHitChance = profile.backOffWhenHitChance;
        backOffTime = profile.backOffTime;
        pickupNoticeDelay = profile.pickupNoticeDelay;
        fightOverPickupChance = profile.fightOverPickupChance;
        useAbilityChance = profile.useAbilityChance;
        abilitySmarts = profile.abilitySmarts;
        blockChance = profile.blockChance;
        blockReactionTime = profile.blockReactionTime;
        blockHoldTime = profile.blockHoldTime;
        blockCooldown = profile.blockCooldown;
        specialChance = profile.specialChance;
        specialSmarts = profile.specialSmarts;
        dashInChance = profile.dashInChance;
        dodgeChance = profile.dodgeChance;
        turnReactionTime = profile.turnReactionTime;
    }

    void OnEnable()
    {
        combat.HitTaken += OnHitTaken;
        if (targetCombat != null) targetCombat.AttackStarted += OnTargetAttack;
        thinkTimer = Roll(reactionTime);
    }

    void OnDisable()
    {
        combat.HitTaken -= OnHitTaken;
        if (targetCombat != null) targetCombat.AttackStarted -= OnTargetAttack;
        fighter.SetMoveInput(0f);
        DropGuard();
        dodgePending = false;
        turnTimer = -1f;
    }

    void Update()
    {
        if (target == null || health.IsDead || (targetHealth != null && targetHealth.IsDead))
        {
            fighter.SetMoveInput(0f);
            DropGuard();
            return;
        }

        float dt = Time.deltaTime;
        attackTimer -= dt;
        backOffTimer -= dt;
        thinkTimer -= dt;
        blockCooldownTimer -= dt;
        UpdateDodge(dt);
        UpdateTurn(dt);
        // Guard up (or about to go up): nothing else to decide.
        if (!UpdateGuard(dt) && thinkTimer <= 0f)
        {
            thinkTimer = Roll(reactionTime);
            Think();
        }
        SteerInAir();
    }

    int ToTarget => target.transform.position.x >= transform.position.x ? 1 : -1;
    bool FacesTarget => fighter.FacingDirection == ToTarget;

    // The target is behind it: after the turn reaction time, jump so it can turn around mid-air.
    void UpdateTurn(float dt)
    {
        if (FacesTarget || !fighter.IsGrounded)
        {
            turnTimer = -1f;
            return;
        }
        if (turnTimer < 0f)
        {
            turnTimer = Roll(turnReactionTime);
            return;
        }
        turnTimer -= dt;
        if (turnTimer > 0f || !combat.CanAct || fighter.IsDashing) return;
        fighter.Jump();
        turnTimer = -1f;
    }

    // In the air it steers toward the target, which turns it to face the target (unless it is jumping
    // for a pickup).
    void SteerInAir()
    {
        if (!fighter.IsGrounded && !headingForPickup)
            fighter.SetMoveInput(ToTarget);
    }

    void Think()
    {
        float dx = target.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(dx);
        int toTarget = dx >= 0f ? 1 : -1;
        headingForPickup = false;

        MaybeUseAbility(distance);
        if (MaybeSpecial(distance, toTarget)) return;

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
            return;
        }

        // An armed Ice Strike only triggers on a punch, so close in to punch range.
        bool iceArmed = abilities != null && abilities.IceStrikeArmed;
        if (distance > (iceArmed ? punchRange : kickRange))
        {
            fighter.SetMoveInput(toTarget);
            if (distance > dashInDistance && Random.value < dashInChance) fighter.Dash(toTarget);
            return;
        }

        fighter.SetMoveInput(0f);
        if (!FacesTarget) return; // its back is turned: UpdateTurn jumps it around first

        // Waiting on its own attack cooldown up close: sometimes keep the guard up instead.
        if (attackTimer > 0f && Random.value < blockChance * 0.25f && TryStartBlock(0f)) return;

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

    // Heads for the nearest noticed pickup when the ability slot is empty. Returns true if it did.
    bool GoForPickup(float distance, int toTarget)
    {
        if (abilities == null || abilities.Held != AbilityType.None) return false;

        AbilityPickup nearest = null;
        float best = float.MaxValue;
        foreach (AbilityPickup pickup in AbilityPickup.Active)
        {
            if (pickup.Age < pickupNoticeDelay) continue;
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
        headingForPickup = true;
        fighter.SetMoveInput(toPickup);
        if (best > dashInDistance && Random.value < dashInChance) fighter.Dash(toPickup);

        // The target stands between us and the pickup: jump over it.
        if (toPickup == toTarget && distance < best && distance < jumpOverDistance)
            fighter.Jump();
        return true;
    }

    void MaybeUseAbility(float distance)
    {
        if (abilities == null || abilities.Held == AbilityType.None) return;
        if (Random.value > useAbilityChance) return;

        // A careless AI uses whatever it holds without checking whether it helps.
        bool careless = Random.value > abilitySmarts;
        bool useful = careless || abilities.Held switch
        {
            AbilityType.Healing => health.Max - health.Current >= abilities.HealAmount,
            AbilityType.DoubleDamage => distance <= abilityUseRange && !abilities.DoubleDamageActive,
            AbilityType.IceStrike => distance <= abilityUseRange && !abilities.IceStrikeArmed,
            AbilityType.AbilitySteal => abilities.Opponent != null && abilities.Opponent.Held != AbilityType.None,
            _ => false,
        };
        if (useful) abilities.UseAbility();
    }

    // Fires the cyber uppercut when the meter is full. A smart AI waits for the target to be in range and
    // not guarding; a careless one also throws it into a guard or from a bit too far, and whiffs.
    // Returns true if it fired.
    bool MaybeSpecial(float distance, int toTarget)
    {
        if (!combat.CanSpecial || !FacesTarget || Random.value > specialChance) return false;

        bool careless = Random.value > specialSmarts;
        bool open = distance <= specialRange && (targetCombat == null || !targetCombat.IsGuarding);
        if (!(open || (careless && distance <= specialRange * 2f))) return false;

        fighter.SetMoveInput(0f);
        combat.Special();
        return true;
    }

    // The target started an attack: maybe dash away from it, or raise the guard (only when facing it, since
    // the guard only covers the front), after a reaction delay.
    void OnTargetAttack(AttackType type)
    {
        if (target == null || health.IsDead) return;
        float dx = transform.position.x - target.transform.position.x;
        bool facingUs = (dx >= 0f ? 1 : -1) == target.FacingDirection;
        if (!facingUs || Mathf.Abs(dx) > blockRange) return;

        float dodge = type == AttackType.Special ? Mathf.Min(1f, dodgeChance * 2f) : dodgeChance;
        if (!dodgePending && !guarding && Random.value < dodge)
        {
            dodgePending = true;
            dodgeTimer = Roll(blockReactionTime);
            return;
        }
        if (Random.value > blockChance) return;
        TryStartBlock(Roll(blockReactionTime));
    }

    // Counts down the dodge reaction, then dashes away from the target as soon as it is free to
    // (e.g. once its own attack ends); gives up if that takes longer than the dodge window.
    void UpdateDodge(float dt)
    {
        if (!dodgePending) return;
        dodgeTimer -= dt;
        if (dodgeTimer > 0f) return;
        if (fighter.CanDash)
        {
            fighter.Dash(transform.position.x >= target.transform.position.x ? 1 : -1);
            dodgePending = false;
        }
        else if (dodgeTimer < -dodgeWindow)
        {
            dodgePending = false;
        }
    }

    bool TryStartBlock(float delay)
    {
        if (guarding || blockDelayTimer >= 0f || blockCooldownTimer > 0f || !FacesTarget) return false;
        blockDelayTimer = delay;
        return true;
    }

    // Counts down to raising the guard, then holds it for a limited time. Returns true while blocking.
    bool UpdateGuard(float dt)
    {
        if (blockDelayTimer >= 0f)
        {
            blockDelayTimer -= dt;
            if (blockDelayTimer >= 0f) return false;
            fighter.SetMoveInput(0f);
            combat.SetBlock(true);
            guarding = true;
            blockHoldTimer = Roll(blockHoldTime);
        }
        if (!guarding) return false;

        // The target got behind it: the guard only covers the front, so drop it and go turn around.
        if (!FacesTarget)
        {
            DropGuard();
            return false;
        }

        // The hold only counts down while the guard is up or could be (not while stunned or mid-attack),
        // so a guard raised just before a hit still covers the follow-ups. It always runs out eventually.
        if (combat.IsGuarding || combat.CanAct) blockHoldTimer -= dt;
        if (blockHoldTimer > 0f) return true;
        DropGuard();
        blockCooldownTimer = blockCooldown;
        return false;
    }

    void DropGuard()
    {
        blockDelayTimer = -1f;
        if (!guarding) return;
        guarding = false;
        combat.SetBlock(false);
    }

    void OnHitTaken()
    {
        // Turtle up against the follow-ups; the guard rises once the hit stun wears off.
        if (Random.value < blockChance && TryStartBlock(Roll(blockReactionTime))) return;
        if (Random.value < backOffWhenHitChance)
            backOffTimer = Roll(backOffTime);
    }

    static float Roll(Vector2 range) => Random.Range(range.x, range.y);
}
