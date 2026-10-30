using UnityEngine;

// Drives a Fighter for the computer opponent: approaches the target (dashing in from afar), attacks in range,
// sometimes backs off, goes for ability pickups (jumping for falling ones passing overhead when it's safe),
// uses its two ability slots when useful, fires its special when the meter is full and dashes away from
// incoming attacks. On its last heart it plays more carefully (Last Heart Caution). It doesn't waste
// attacks on a target that is still invincible after respawning.
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

    [Header("Falling Pickups")]
    [Tooltip("Chance to jump for a falling pickup passing overhead when it is close and safe to.")]
    [SerializeField, Range(0f, 1f)] float airGrabChance = 0.6f;
    [Tooltip("Jumps for a falling pickup within this horizontal distance...")]
    [SerializeField] float airGrabReach = 0.8f;
    [Tooltip("...while it is this high above the fighter (min, max), so the jump meets it.")]
    [SerializeField] Vector2 airGrabHeight = new Vector2(2f, 5f);
    [Tooltip("Only jumps for one when the target is at least this far away (more on its last heart).")]
    [SerializeField] float airGrabSafeDistance = 2.4f;

    [Header("Last Heart")]
    [Tooltip("0 = plays the same on its last heart; 1 = much more carefully (see AIProfile).")]
    [SerializeField, Range(0f, 1f)] float lastHeartCaution = 0.5f;

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
    Lives lives;
    AbilityPickup airGrabRolled;
    bool airGrabWanted;
    AbilityPickup airGrabTarget;
    float airGrabJumpTime;

    // Last heart: how much more carefully it plays right now (0 = not on its last heart).
    float Caution => lives != null && lives.OnLastHeart ? lastHeartCaution : 0f;
    float BlockChance => Mathf.Lerp(blockChance, 1f, Caution * 0.5f);
    float DodgeChance => Mathf.Lerp(dodgeChance, 0.6f, Caution * 0.5f);
    float BackOffAfterAttackChance => Mathf.Lerp(backOffAfterAttackChance, 0.8f, Caution * 0.5f);
    float AbilitySmarts => Mathf.Lerp(abilitySmarts, 1f, Caution);
    float SpecialSmarts => Mathf.Lerp(specialSmarts, 1f, Caution);
    float LowStamina => lowStamina + 0.2f * Caution;
    float AirGrabSafeDistance => airGrabSafeDistance + 2f * Caution;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        health = GetComponent<Health>();
        lives = GetComponent<Lives>();
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
        airGrabChance = profile.airGrabChance;
        lastHeartCaution = profile.lastHeartCaution;
    }

    void OnEnable()
    {
        combat.HitTaken += OnHitTaken;
        health.Revived += ResetState;
        if (targetCombat != null) targetCombat.AttackStarted += OnTargetAttack;
        thinkTimer = Roll(reactionTime);
    }

    void OnDisable()
    {
        combat.HitTaken -= OnHitTaken;
        health.Revived -= ResetState;
        if (targetCombat != null) targetCombat.AttackStarted -= OnTargetAttack;
        ResetState();
    }

    // Forgets what it was doing: on disable and after respawning.
    void ResetState()
    {
        fighter.SetMoveInput(0f);
        DropGuard();
        dodgePending = false;
        turnTimer = -1f;
        backOffTimer = 0f;
        recovering = false;
        headingForPickup = false;
        airGrabTarget = null;
    }

    void Update()
    {
        if (target == null || health.IsDead)
        {
            fighter.SetMoveInput(0f);
            DropGuard();
            return;
        }

        float dt = Time.deltaTime;
        // The target is knocked out (respawning): nothing to fight, so use the time to collect pickups.
        if (targetHealth != null && targetHealth.IsDead)
        {
            DropGuard();
            dodgePending = false;
            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = Roll(reactionTime);
                headingForPickup = false;
                if (!GoForPickup(float.MaxValue, ToTarget)) fighter.SetMoveInput(0f);
            }
            UpdateAirGrab();
            SteerInAir();
            return;
        }

        attackTimer -= dt;
        backOffTimer -= dt;
        thinkTimer -= dt;
        blockCooldownTimer -= dt;
        UpdateDodge(dt);
        UpdateTurn(dt);
        // Guard up (or about to go up): nothing else to decide.
        bool guardUp = UpdateGuard(dt);
        if (!guardUp && thinkTimer <= 0f)
        {
            thinkTimer = Roll(reactionTime);
            Think();
        }
        if (!guardUp) UpdateAirGrab();
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
    // for a pickup: then it steers under the falling pickup it jumped for, or keeps heading for the pickup).
    void SteerInAir()
    {
        if (fighter.IsGrounded)
        {
            // The jump only leaves the ground on the next physics step; forget the pickup once landed.
            if (Time.time > airGrabJumpTime + 0.2f) airGrabTarget = null;
            return;
        }
        if (airGrabTarget != null)
        {
            float dx = airGrabTarget.transform.position.x - transform.position.x;
            fighter.SetMoveInput(Mathf.Abs(dx) > 0.15f ? Mathf.Sign(dx) : 0f);
        }
        else if (!headingForPickup)
        {
            fighter.SetMoveInput(ToTarget);
        }
    }

    // Checked every frame (the window is short): a noticed falling pickup is passing overhead at a height
    // the jump can meet. Rolls once per pickup whether to go for it, and only jumps when the target is
    // far enough away (or down) not to punish it.
    void UpdateAirGrab()
    {
        if (abilities == null || abilities.IsFull || !fighter.IsGrounded || !combat.CanAct) return;

        foreach (AbilityPickup pickup in AbilityPickup.Active)
        {
            if (!pickup.IsFalling || pickup.Age < pickupNoticeDelay) continue;
            Vector2 offset = pickup.transform.position - transform.position;
            if (Mathf.Abs(offset.x) > airGrabReach || offset.y < airGrabHeight.x || offset.y > airGrabHeight.y) continue;

            if (airGrabRolled != pickup)
            {
                airGrabRolled = pickup;
                airGrabWanted = Random.value < airGrabChance;
            }
            bool targetDown = targetHealth != null && targetHealth.IsDead;
            bool safe = targetDown || Mathf.Abs(target.transform.position.x - transform.position.x) >= AirGrabSafeDistance;
            if (!airGrabWanted || !safe) continue;

            fighter.Jump();
            airGrabTarget = pickup;
            airGrabJumpTime = Time.time;
            return;
        }
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
            if (stamina.Fraction < LowStamina) recovering = true;
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

        // Waiting on its own attack cooldown up close (or for a respawned target's invincibility to run
        // out): sometimes keep the guard up instead.
        bool targetInvincible = targetHealth != null && targetHealth.IsInvincible;
        if ((attackTimer > 0f || targetInvincible) && Random.value < BlockChance * 0.25f && TryStartBlock(0f)) return;

        if (attackTimer > 0f || targetInvincible || !combat.CanAct) return;
        attackTimer = Roll(attackCooldown);
        if (Random.value > attackChance) return;

        if (!iceArmed && (distance > punchRange || Random.value < kickChance))
            combat.Kick();
        else
            combat.Punch();

        if (Random.value < BackOffAfterAttackChance)
            backOffTimer = Roll(backOffTime);
    }

    // Heads for the nearest noticed pickup (on the ground, or where a falling one will land) while it has
    // a free ability slot. Returns true if it did.
    bool GoForPickup(float distance, int toTarget)
    {
        if (abilities == null || abilities.IsFull) return false;

        AbilityPickup nearest = NearestNoticedPickup(out float best);
        if (nearest == null) return false;
        if (distance <= kickRange && !recovering && Random.value < fightOverPickupChance) return false;

        int toPickup = nearest.transform.position.x >= transform.position.x ? 1 : -1;
        headingForPickup = true;
        // Under a falling pickup: wait for it here (UpdateAirGrab may jump for it).
        if (nearest.IsFalling && best < 0.3f)
        {
            fighter.SetMoveInput(0f);
            return true;
        }
        fighter.SetMoveInput(toPickup);
        if (best > dashInDistance && Random.value < dashInChance) fighter.Dash(toPickup);

        // The target stands between us and the pickup: jump over it.
        if (toPickup == toTarget && distance < best && distance < jumpOverDistance)
            fighter.Jump();
        return true;
    }

    // Nearest pickup (by x) that has been around long enough to be noticed, with its x distance.
    AbilityPickup NearestNoticedPickup(out float distance)
    {
        AbilityPickup nearest = null;
        distance = float.MaxValue;
        foreach (AbilityPickup pickup in AbilityPickup.Active)
        {
            if (pickup.Age < pickupNoticeDelay) continue;
            float d = Mathf.Abs(pickup.transform.position.x - transform.position.x);
            if (d < distance)
            {
                distance = d;
                nearest = pickup;
            }
        }
        return nearest;
    }

    // Considers both slots and uses the one that helps most right now. A careless AI just fires the first
    // ability it holds. With both slots full and a pickup waiting it is less picky, to make room. On its
    // last heart and hurt, it uses Healing straight away.
    void MaybeUseAbility(float distance)
    {
        if (abilities == null || !abilities.HasAny) return;

        int healSlot = abilities.SlotOf(AbilityType.Healing);
        bool desperate = Caution > 0f && healSlot >= 0 && health.Current <= health.Max / 2;
        if (desperate)
        {
            abilities.UseAbility(healSlot);
            return;
        }
        if (Random.value > useAbilityChance) return;

        bool careless = Random.value > AbilitySmarts;
        bool makeRoom = abilities.IsFull && NearestNoticedPickup(out _) != null;
        int bestSlot = -1, bestValue = 0;
        for (int slot = 0; slot < FighterAbilities.SlotCount; slot++)
        {
            AbilityType type = abilities.Get(slot);
            if (type == AbilityType.None) continue;
            int value = careless ? 1 : AbilityValue(type, distance, makeRoom);
            if (value <= bestValue) continue;
            bestValue = value;
            bestSlot = slot;
            if (careless) break;
        }
        if (bestSlot >= 0) abilities.UseAbility(bestSlot);
    }

    // How much an ability helps right now: 0 = not at all (keep it), higher = use it first.
    int AbilityValue(AbilityType type, float distance, bool makeRoom)
    {
        float range = makeRoom ? abilityUseRange * 2f : abilityUseRange;
        int missing = health.Max - health.Current;
        switch (type)
        {
            case AbilityType.Healing:
                if (missing >= abilities.HealAmount) return 4;
                return makeRoom && missing >= abilities.HealAmount / 2 ? 1 : 0;
            case AbilityType.AbilitySteal:
                return abilities.Opponent != null && abilities.Opponent.HasAny ? 3 : 0;
            case AbilityType.DoubleDamage:
                return distance <= range && !abilities.DoubleDamageActive ? 2 : 0;
            case AbilityType.IceStrike:
                return distance <= range && !abilities.IceStrikeArmed ? 2 : 0;
            default:
                return 0;
        }
    }

    // Fires the cyber uppercut when the meter is full. A smart AI waits for the target to be in range and
    // not guarding; a careless one also throws it into a guard or from a bit too far, and whiffs.
    // Returns true if it fired.
    bool MaybeSpecial(float distance, int toTarget)
    {
        if (!combat.CanSpecial || !FacesTarget || Random.value > specialChance) return false;
        if (targetHealth != null && targetHealth.IsInvincible) return false; // it would pass straight through

        bool careless = Random.value > SpecialSmarts;
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

        float dodge = type == AttackType.Special ? Mathf.Min(1f, DodgeChance * 2f) : DodgeChance;
        if (!dodgePending && !guarding && Random.value < dodge)
        {
            dodgePending = true;
            dodgeTimer = Roll(blockReactionTime);
            return;
        }
        if (Random.value > BlockChance) return;
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
        if (Random.value < BlockChance && TryStartBlock(Roll(blockReactionTime))) return;
        if (Random.value < backOffWhenHitChance)
            backOffTimer = Roll(backOffTime);
    }

    static float Roll(Vector2 range) => Random.Range(range.x, range.y);
}
