using UnityEngine;

// Feeds Fighter state into the Animator. Visual only: no gameplay rules here.
[RequireComponent(typeof(Fighter), typeof(Animator))]
public class FighterAnimator : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    static readonly int PunchHash = Animator.StringToHash("Punch");
    static readonly int KickHash = Animator.StringToHash("Kick");
    static readonly int HurtHash = Animator.StringToHash("Hurt");
    static readonly int DeadHash = Animator.StringToHash("Dead");

    Fighter fighter;
    FighterCombat combat;
    Health health;
    Rigidbody2D rb;
    Animator animator;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (combat == null) return;
        combat.AttackStarted += OnAttackStarted;
        combat.HitTaken += OnHitTaken;
    }

    void OnDisable()
    {
        if (combat == null) return;
        combat.AttackStarted -= OnAttackStarted;
        combat.HitTaken -= OnHitTaken;
    }

    void Update()
    {
        Vector2 velocity = rb.linearVelocity;
        animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
        animator.SetBool(GroundedHash, fighter.IsGrounded);
        animator.SetFloat(VelocityYHash, velocity.y);
        animator.SetBool(DeadHash, health != null && health.IsDead);
    }

    void OnAttackStarted(AttackType type)
    {
        animator.SetTrigger(type == AttackType.Punch ? PunchHash : KickHash);
    }

    void OnHitTaken()
    {
        animator.ResetTrigger(PunchHash);
        animator.ResetTrigger(KickHash);
        animator.SetTrigger(HurtHash);
    }
}
