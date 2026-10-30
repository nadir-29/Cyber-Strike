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
    static readonly int BlockingHash = Animator.StringToHash("Blocking");
    static readonly int BlockHitHash = Animator.StringToHash("BlockHit");
    static readonly int WinHash = Animator.StringToHash("Win");
    static readonly int SpecialHash = Animator.StringToHash("Special");
    static readonly int DashingHash = Animator.StringToHash("Dashing");
    static readonly int BackwardHash = Animator.StringToHash("Backward");

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
        combat.Blocked += OnBlocked;
    }

    void OnDisable()
    {
        if (combat == null) return;
        combat.AttackStarted -= OnAttackStarted;
        combat.HitTaken -= OnHitTaken;
        combat.Blocked -= OnBlocked;
    }

    void Update()
    {
        Vector2 velocity = rb.linearVelocity;
        animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
        animator.SetBool(GroundedHash, fighter.IsGrounded);
        animator.SetFloat(VelocityYHash, velocity.y);
        animator.SetBool(DeadHash, health != null && health.IsDead);
        animator.SetBool(BlockingHash, combat != null && combat.IsGuarding);
        animator.SetBool(DashingHash, fighter.IsDashing);
        // 0 = forward walk / dash, 1 = backward walk / backdash (blend trees pick one clip).
        animator.SetFloat(BackwardHash, fighter.IsMovingBackward ? 1f : 0f);
    }

    void OnAttackStarted(AttackType type)
    {
        animator.SetTrigger(type switch
        {
            AttackType.Punch => PunchHash,
            AttackType.Kick => KickHash,
            _ => SpecialHash,
        });
    }

    // Strikes the victory pose (one-shot intro, then a loop) and holds it for the rest of the match.
    // Pending attack / hurt triggers are cleared so nothing interrupts it.
    public void PlayVictory()
    {
        animator.ResetTrigger(PunchHash);
        animator.ResetTrigger(KickHash);
        animator.ResetTrigger(SpecialHash);
        animator.ResetTrigger(HurtHash);
        animator.ResetTrigger(BlockHitHash);
        animator.SetTrigger(WinHash);
    }

    void OnBlocked(FighterCombat.HitInfo hit) => animator.SetTrigger(BlockHitHash);

    void OnHitTaken()
    {
        animator.ResetTrigger(PunchHash);
        animator.ResetTrigger(KickHash);
        animator.ResetTrigger(SpecialHash);
        animator.SetTrigger(HurtHash);
    }
}
