using System;
using System.Collections.Generic;
using UnityEngine;

public enum AttackType { Punch, Kick }

// Attacks, hitboxes and hit reactions shared by the player and the AI.
// Input controllers only call Punch() and Kick().
[RequireComponent(typeof(Fighter), typeof(Health))]
public class FighterCombat : MonoBehaviour
{
    [Serializable]
    public class Attack
    {
        public int damage;
        [Tooltip("Seconds before the hitbox turns on.")]
        public float startup;
        [Tooltip("Seconds the hitbox stays on.")]
        public float active;
        [Tooltip("Seconds after the hitbox turns off before the fighter can act again.")]
        public float recovery;
        [Tooltip("Hitbox centre relative to the fighter, as if facing right.")]
        public Vector2 offset;
        public Vector2 size;

        public float Duration => startup + active + recovery;
    }

    // Timings match the 3-frame punch (12 fps) and kick (9 fps) animations.
    [SerializeField] Attack punch = new Attack
    {
        damage = 6, startup = 0.083f, active = 0.083f, recovery = 0.083f,
        offset = new Vector2(0.72f, 0.27f), size = new Vector2(0.6f, 0.4f),
    };
    [SerializeField] Attack kick = new Attack
    {
        damage = 10, startup = 0.111f, active = 0.111f, recovery = 0.111f,
        offset = new Vector2(0.8f, -0.42f), size = new Vector2(0.85f, 0.45f),
    };

    [Header("Hits")]
    [SerializeField] LayerMask hurtboxLayers;
    [SerializeField] float hitStun = 0.25f;

    Fighter fighter;
    Health health;
    Attack current;
    float attackTime;
    float stunTime;
    ContactFilter2D hurtboxFilter;
    readonly List<Collider2D> overlaps = new List<Collider2D>();
    readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

    public bool IsAttacking => current != null;
    public bool CanAct => current == null && stunTime <= 0f && !health.IsDead;

    public event Action<AttackType> AttackStarted;
    public event Action HitTaken;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        health = GetComponent<Health>();
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

    void StartAttack(Attack attack, AttackType type)
    {
        if (!CanAct) return;

        current = attack;
        attackTime = 0f;
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

        if (current == null) return;

        attackTime += Time.fixedDeltaTime;
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
        Physics2D.OverlapBox(HitboxCenter(current), current.size, 0f, hurtboxFilter, overlaps);
        foreach (Collider2D col in overlaps)
        {
            Health target = col.GetComponentInParent<Health>();
            // HashSet.Add returns false if this swing already hit the target.
            if (target == null || target == health || !hitThisSwing.Add(target)) continue;
            target.TakeDamage(current.damage);
        }
    }

    Vector2 HitboxCenter(Attack attack)
    {
        int facing = fighter != null ? fighter.FacingDirection : (transform.localScale.x < 0f ? -1 : 1);
        return (Vector2)transform.position + new Vector2(attack.offset.x * facing, attack.offset.y);
    }

    void OnDamaged()
    {
        current = null;
        stunTime = hitStun;
        UpdateMovementLock();
        HitTaken?.Invoke();
    }

    void OnDied()
    {
        current = null;
        UpdateMovementLock();
        Debug.Log($"{name} is K.O.", this);
    }

    void UpdateMovementLock()
    {
        fighter.MovementLocked = current != null || stunTime > 0f || health.IsDead;
    }

    void OnDrawGizmosSelected()
    {
        // Outline both hitboxes; fill the one that is currently live.
        foreach (Attack attack in new[] { punch, kick })
        {
            bool live = attack == current && attackTime >= attack.startup && attackTime < attack.startup + attack.active;
            Gizmos.color = live ? new Color(1f, 0.2f, 0.3f, 0.6f) : new Color(1f, 0.6f, 0.2f, 0.8f);
            if (live) Gizmos.DrawCube(HitboxCenter(attack), attack.size);
            else Gizmos.DrawWireCube(HitboxCenter(attack), attack.size);
        }
    }
}
