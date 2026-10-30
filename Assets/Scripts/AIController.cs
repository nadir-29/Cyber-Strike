using UnityEngine;

// Drives a Fighter for the computer opponent: approaches the target, attacks in range
// and sometimes backs off. Like PlayerInputController it only issues inputs; gameplay
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

    Fighter fighter;
    FighterCombat combat;
    Health health;
    Health targetHealth;
    float thinkTimer;
    float attackTimer;
    float backOffTimer;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        health = GetComponent<Health>();
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

        if (backOffTimer > 0f)
        {
            fighter.SetMoveInput(-toTarget);
            return;
        }

        if (distance > kickRange)
        {
            fighter.SetMoveInput(toTarget);
            return;
        }

        fighter.SetMoveInput(0f);
        fighter.Face(toTarget);

        if (attackTimer > 0f || !combat.CanAct) return;
        attackTimer = Roll(attackCooldown);
        if (Random.value > attackChance) return;

        if (distance > punchRange || Random.value < kickChance)
            combat.Kick();
        else
            combat.Punch();

        if (Random.value < backOffAfterAttackChance)
            backOffTimer = Roll(backOffTime);
    }

    void OnHitTaken()
    {
        if (Random.value < backOffWhenHitChance)
            backOffTimer = Roll(backOffTime);
    }

    static float Roll(Vector2 range) => Random.Range(range.x, range.y);
}
