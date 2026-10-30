using System;
using System.Collections.Generic;
using UnityEngine;

public enum AttackType { Punch, Kick, Special }

// Attacks, hitboxes, blocking and hit reactions shared by the player and the AI.
// Input controllers only call Punch(), Kick(), Special() and SetBlock(). Damage is multiplied by FighterAbilities (Double Damage).
// Special: the cyber uppercut. Needs a full SpecialMeter (spent on use) and the ground; the fighter hops up with it and a
// clean hit launches the target into a long hit stun. Blocked, it is chip damage like any other hit.
// No attacking or guarding mid-dash; a hit cuts a dash short.
// Blocking: while the block input is held the fighter is locked in place (no move, jump or attack). After a short
// startup the guard stops hits from the front: they deal chip damage only, cause no stun and never trigger Ice Strike.
// Hits from behind land normally.
[RequireComponent(typeof(Fighter), typeof(Health))]
public class FighterCombat : MonoBehaviour
{
    public struct HitInfo
    {
        public FighterCombat attacker;
        public Health target;
        public Vector2 point;
        public AttackType type;
        public int damage;
        // -1 = left, 1 = right: the way the hit pushes the target.
        public int direction;
        // True while the attacker's Double Damage is active.
        public bool empowered;
        // True if the target's guard stopped the hit (damage is then only chip damage).
        public bool blocked;
    }

    [Serializable]
    public class Attack
    {
        public int damage;
        public float staminaCost;
        [Tooltip("Seconds before the hitbox turns on.")]
        public float startup;
        [Tooltip("Seconds the hitbox stays on.")]
        public float active;
        [Tooltip("Seconds after the hitbox turns off before the fighter can act again.")]
        public float recovery;
        [Tooltip("Hitbox centre relative to the fighter, as if facing right.")]
        public Vector2 offset;
        public Vector2 size;
        [Tooltip("Velocity a clean hit launches the target with (x away from the attacker); zero = no launch.")]
        public Vector2 launch;
        [Tooltip("Hit stun of a launched target, in seconds.")]
        public float launchStun;
        [Tooltip("Upward velocity the attacker hops with when the hitbox turns on; zero = stays grounded.")]
        public float rise;

        public float Duration => startup + active + recovery;
    }

    // Timings match the 3-frame punch (12 fps) and kick (9 fps) animations.
    [SerializeField] Attack punch = new Attack
    {
        damage = 6, staminaCost = 10f, startup = 0.083f, active = 0.083f, recovery = 0.083f,
        offset = new Vector2(0.72f, 0.27f), size = new Vector2(0.6f, 0.4f),
    };
    [SerializeField] Attack kick = new Attack
    {
        damage = 10, staminaCost = 18f, startup = 0.111f, active = 0.111f, recovery = 0.111f,
        offset = new Vector2(0.8f, -0.42f), size = new Vector2(0.85f, 0.45f),
    };
    // Timings match the 4-frame uppercut animation (charge, rise, apex, recover).
    [SerializeField] Attack special = new Attack
    {
        damage = 22, staminaCost = 0f, startup = 0.16f, active = 0.14f, recovery = 0.3f,
        offset = new Vector2(0.6f, 0.45f), size = new Vector2(0.95f, 1.4f),
        launch = new Vector2(4f, 11f), launchStun = 0.75f, rise = 5f,
    };

    [Header("Hits")]
    [SerializeField] LayerMask hurtboxLayers;
    [SerializeField] float hitStun = 0.25f;
    [Tooltip("Velocity the loser is launched with on the knockout blow (x is away from the hit).")]
    [SerializeField] Vector2 knockoutLaunch = new Vector2(6f, 7f);

    [Header("Block")]
    [Tooltip("Seconds raising the guard before it stops hits; the fighter is already locked in place meanwhile.")]
    [SerializeField] float blockStartup = 0.1f;
    [Tooltip("Fraction of a blocked hit's damage that still goes through (always at least 1).")]
    [SerializeField, Range(0f, 1f)] float chipDamage = 0.2f;

    Fighter fighter;
    Health health;
    Stamina stamina;
    FighterAbilities abilities;
    SpecialMeter meter;
    Attack current;
    AttackType currentType;
    float attackTime;
    float stunTime;
    ContactFilter2D hurtboxFilter;
    readonly List<Collider2D> overlaps = new List<Collider2D>();
    readonly HashSet<Health> hitThisSwing = new HashSet<Health>();
    bool blockHeld;
    float guardTime;
    bool absorbing;
    bool rose;

    // Multiplies all damage this fighter deals, e.g. an opponent's difficulty bonus.
    public float DamageScale { get; set; } = 1f;
    public bool IsAttacking => current != null;
    // Guard raised or being raised: the fighter can't move, jump or attack.
    public bool IsGuarding { get; private set; }
    // Guard fully up: hits from the front are blocked.
    public bool IsBlocking => IsGuarding && guardTime >= blockStartup;
    public bool CanAct => current == null && stunTime <= 0f && !health.IsDead && !IsGuarding && !fighter.IsDashing;
    public bool CanSpecial => CanAct && fighter.IsGrounded && meter != null && meter.IsFull;

    public event Action<AttackType> AttackStarted;
    public event Action HitTaken;
    // Raised once per target each time one of this fighter's attacks connects.
    public event Action<HitInfo> HitLanded;
    // Raised on the defender each time its guard stops a hit.
    public event Action<HitInfo> Blocked;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        health = GetComponent<Health>();
        stamina = GetComponent<Stamina>();
        abilities = GetComponent<FighterAbilities>();
        meter = GetComponent<SpecialMeter>();
        hurtboxFilter = new ContactFilter2D();
        hurtboxFilter.SetLayerMask(hurtboxLayers);

        if (hurtboxLayers == 0)
            Debug.LogWarning($"{name}: Hurtbox Layers is not set on FighterCombat, so attacks can never hit.", this);
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    public void Punch() => StartAttack(punch, AttackType.Punch);
    public void Kick() => StartAttack(kick, AttackType.Kick);

    // Cyber uppercut: only with a full special meter, from the ground.
    public void Special()
    {
        if (!CanSpecial || !meter.TrySpend()) return;
        StartAttack(special, AttackType.Special);
    }

    // Hold to block. The guard goes up as soon as the fighter is free (not attacking, stunned or airborne).
    public void SetBlock(bool held) => blockHeld = held;

    void StartAttack(Attack attack, AttackType type)
    {
        if (!CanAct) return;
        if (stamina != null && !stamina.TrySpend(attack.staminaCost)) return;

        current = attack;
        currentType = type;
        attackTime = 0f;
        rose = false;
        hitThisSwing.Clear();
        UpdateMovementLock();
        AttackStarted?.Invoke(type);
    }

    void FixedUpdate()
    {
        if (stunTime > 0f)
        {
            stunTime -= Time.fixedDeltaTime;
            if (stunTime <= 0f) UpdateMovementLock();
        }
        UpdateGuard();

        if (current == null) return;

        attackTime += Time.fixedDeltaTime;
        if (attackTime >= current.startup && !rose)
        {
            rose = true;
            if (current.rise > 0f) fighter.Launch(new Vector2(0f, current.rise));
        }
        if (attackTime >= current.startup && attackTime < current.startup + current.active)
            CheckHits();

        if (attackTime >= current.Duration)
        {
            current = null;
            UpdateMovementLock();
        }
    }

    void CheckHits()
    {
        Vector2 center = HitboxCenter(current);
        Physics2D.OverlapBox(center, current.size, 0f, hurtboxFilter, overlaps);
        foreach (Collider2D col in overlaps)
        {
            Health target = col.GetComponentInParent<Health>();
            // HashSet.Add returns false if this swing already hit the target.
            if (target == null || target == health || !hitThisSwing.Add(target)) continue;
            if (target.IsDead) continue;

            int multiplier = abilities != null ? abilities.DamageMultiplier : 1;
            HitInfo hit = new HitInfo
            {
                attacker = this, target = target, point = col.ClosestPoint(center), type = currentType,
                damage = Mathf.Max(1, Mathf.RoundToInt(current.damage * multiplier * DamageScale)),
                direction = fighter.FacingDirection, empowered = multiplier > 1,
            };

            // A guard facing the attacker turns the hit into chip damage: no Ice Strike, no stun.
            FighterCombat defender = target.GetComponent<FighterCombat>();
            if (defender != null && defender.Blocks(transform.position))
            {
                hit = defender.Absorb(hit);
                if (target.IsDead) HitLanded?.Invoke(hit); // chip KO still gets the knockout feedback
                continue;
            }

            target.TakeDamage(hit.damage, hit.direction);
            if (hit.type == AttackType.Punch && abilities != null)
                abilities.OnPunchLanded(target);
            if (defender != null && !target.IsDead && current.launch != Vector2.zero)
                defender.LaunchFrom(hit.direction, current.launch, current.launchStun);
            HitLanded?.Invoke(hit);
        }
    }

    Vector2 HitboxCenter(Attack attack)
    {
        int facing = fighter != null ? fighter.FacingDirection : (transform.localScale.x < 0f ? -1 : 1);
        return (Vector2)transform.position + new Vector2(attack.offset.x * facing, attack.offset.y);
    }

    // True if the guard is fully up and the attacker stands in front of this fighter.
    bool Blocks(Vector2 attackerPosition)
    {
        if (!IsBlocking) return false;
        float dx = attackerPosition.x - transform.position.x;
        return dx == 0f || (dx > 0f ? 1 : -1) == fighter.FacingDirection;
    }

    HitInfo Absorb(HitInfo hit)
    {
        hit.blocked = true;
        hit.damage = chipDamage > 0f ? Mathf.Max(1, Mathf.RoundToInt(hit.damage * chipDamage)) : 0;
        absorbing = true;
        health.TakeDamage(hit.damage, hit.direction);
        absorbing = false;
        Blocked?.Invoke(hit);
        return hit;
    }

    // Knocked into the air by a launcher: flies away from the hit and stays stunned for a while.
    void LaunchFrom(int direction, Vector2 velocity, float stun)
    {
        stunTime = Mathf.Max(stunTime, stun);
        UpdateMovementLock();
        fighter.Launch(new Vector2(velocity.x * direction, velocity.y));
    }

    void UpdateGuard()
    {
        bool canGuard = blockHeld && current == null && stunTime <= 0f && !health.IsDead && fighter.IsGrounded
                        && !fighter.IsDashing;
        if (canGuard)
        {
            if (IsGuarding)
            {
                guardTime += Time.fixedDeltaTime;
                return;
            }
            IsGuarding = true;
            guardTime = 0f;
            UpdateMovementLock();
        }
        else if (IsGuarding)
        {
            IsGuarding = false;
            UpdateMovementLock();
        }
    }

    void OnDamaged()
    {
        if (absorbing) return; // blocked chip damage: the guard holds, no stun
        fighter.CancelDash();
        IsGuarding = false;
        current = null;
        stunTime = hitStun;
        UpdateMovementLock();
        HitTaken?.Invoke();
    }

    void OnDied()
    {
        current = null;
        IsGuarding = false;
        UpdateMovementLock();
        int direction = health.LastHitDirection != 0 ? health.LastHitDirection : -fighter.FacingDirection;
        fighter.Launch(new Vector2(knockoutLaunch.x * direction, knockoutLaunch.y));
        Debug.Log($"{name} is K.O.", this);
    }

    void UpdateMovementLock()
    {
        fighter.MovementLocked = current != null || stunTime > 0f || health.IsDead || IsGuarding;
    }

    void OnDrawGizmosSelected()
    {
        // Outline both hitboxes; fill the one that is currently live.
        foreach (Attack attack in new[] { punch, kick, special })
        {
            bool live = attack == current && attackTime >= attack.startup && attackTime < attack.startup + attack.active;
            Gizmos.color = live ? new Color(1f, 0.2f, 0.3f, 0.6f) : new Color(1f, 0.6f, 0.2f, 0.8f);
            if (live) Gizmos.DrawCube(HitboxCenter(attack), attack.size);
            else Gizmos.DrawWireCube(HitboxCenter(attack), attack.size);
        }
    }
}
