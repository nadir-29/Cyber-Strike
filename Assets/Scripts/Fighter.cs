using UnityEngine;

// Shared fighter logic for both the player and the AI.
// Input controllers (PlayerInputController / AIController) only call SetMoveInput, Jump and Dash
// here, and Punch / Kick / Special on FighterCombat.
// Facing: on the ground the fighter never turns; moving the other way walks backwards. It can only turn
// while airborne, by holding the opposite direction, and lands facing the new way. Attacks, hitboxes and
// blocking all use FacingDirection.
// Dash: a short, fast burst along the ground in either direction (backwards = backdash) with a cooldown.
// It keeps the current facing, can't start while movement is locked (attacking, stunned, guarding) and a
// hit cuts it short.
// A fighter never rests on another fighter's head: it slides off (toward the arena centre when stacked).
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Fighter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 7f;
    [Tooltip("Together with Gravity Scale this sets the jump: height = v² / (2 · 9.81 · scale).")]
    [SerializeField] float jumpVelocity = 13.1f;
    [Tooltip("Rigidbody gravity scale, applied on Awake. Higher = snappier, less floaty arcs.")]
    [SerializeField] float gravityScale = 2.4f;
    [SerializeField] float jumpStaminaCost = 8f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 17f;
    [Tooltip("Seconds the dash lasts.")]
    [SerializeField] float dashTime = 0.16f;
    [Tooltip("Seconds after a dash ends before the next one can start.")]
    [SerializeField] float dashCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDepth = 0.05f;
    [Tooltip("Sideways speed a fighter slides off another fighter's head with.")]
    [SerializeField] float slideOffSpeed = 5f;

    Rigidbody2D rb;
    Collider2D col;
    Stamina stamina;
    float moveInput;
    bool jumpRequested;
    float slowMultiplier = 1f;
    float slowTimer;
    int dashDirection;
    float dashTimer;
    float dashCooldownTimer;
    readonly System.Collections.Generic.List<Collider2D> below = new System.Collections.Generic.List<Collider2D>();

    public bool IsGrounded { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    // Set by FighterCombat while attacking, stunned or knocked out.
    public bool MovementLocked { get; set; }
    public bool IsSlowed => slowTimer > 0f;
    public bool IsDashing => dashTimer > 0f;
    public bool CanDash => !IsDashing && dashCooldownTimer <= 0f && !MovementLocked && IsGrounded;
    // Moving against the facing direction: walking backwards or backdashing.
    public bool IsMovingBackward => IsDashing
        ? dashDirection != FacingDirection
        : Mathf.Abs(rb.linearVelocity.x) > 0.1f && (rb.linearVelocity.x > 0f ? 1 : -1) != FacingDirection;

    public event System.Action Slowed;
    // Raised when a dash starts, with its direction (-1 or 1).
    public event System.Action<int> Dashed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        stamina = GetComponent<Stamina>();
        rb.freezeRotation = true;
        rb.gravityScale = gravityScale;
        FacingDirection = transform.localScale.x < 0f ? -1 : 1;

        if (groundLayer == 0)
            Debug.LogWarning($"{name}: Ground Layer is not set on Fighter, so it can never jump.", this);
    }

    // -1 = left, 1 = right, 0 = stop.
    public void SetMoveInput(float direction)
    {
        moveInput = Mathf.Clamp(direction, -1f, 1f);
    }

    // Jumps on the next physics step if grounded.
    public void Jump()
    {
        jumpRequested = true;
    }

    // Turns to face a direction (-1 or 1), outside the airborne-only rule. Only for match flow (the winner
    // turning to the loser for its victory pose), never for gameplay.
    public void SnapFacing(int direction)
    {
        if (direction != 0) SetFacing(direction > 0 ? 1 : -1);
    }

    // Puts a knocked-out fighter back at a position facing a direction, standing upright and at rest,
    // with no slow, dash or queued jump. Called by Lives when it respawns, never in gameplay.
    public void Respawn(Vector2 position, int facing)
    {
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.rotation = 0f;
        transform.rotation = Quaternion.identity;
        moveInput = 0f;
        jumpRequested = false;
        slowTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        SnapFacing(facing);
    }

    // Dashes left (-1) or right (1) along the ground. Ignored while airborne, locked or on cooldown.
    public void Dash(int direction)
    {
        if (direction == 0 || !CanDash) return;
        dashDirection = direction > 0 ? 1 : -1;
        dashTimer = dashTime;
        Dashed?.Invoke(dashDirection);
    }

    // Ends a dash early, e.g. when hit. The cooldown still applies.
    public void CancelDash()
    {
        if (!IsDashing) return;
        dashTimer = 0f;
        dashCooldownTimer = dashCooldown;
    }

    // Sets velocity directly, e.g. for a knockback. Keeps its horizontal momentum until it lands.
    public void Launch(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    // Multiplies move speed for a while (e.g. Ice Strike). A new slow refreshes the duration.
    public void ApplySlow(float multiplier, float duration)
    {
        slowMultiplier = multiplier;
        slowTimer = Mathf.Max(slowTimer, duration);
        Slowed?.Invoke();
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        if (slowTimer > 0f) slowTimer -= Time.fixedDeltaTime;
        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.fixedDeltaTime;

        Vector2 velocity = rb.linearVelocity;
        if (IsDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f) dashCooldownTimer = dashCooldown;
            velocity.x = IsDashing ? dashDirection * dashSpeed * (IsSlowed ? slowMultiplier : 1f) : 0f;
            rb.linearVelocity = velocity;
            jumpRequested = false;
            return; // no steering, jumping or turning mid-dash
        }
        if (!MovementLocked)
            velocity.x = moveInput * moveSpeed * (IsSlowed ? slowMultiplier : 1f);
        else if (IsGrounded && velocity.y <= 0.01f)
            velocity.x = 0f; // airborne attacks and launches keep their momentum

        if (!IsGrounded && velocity.y <= 0.01f && FighterBelow(out float otherX))
        {
            float dx = transform.position.x - otherX;
            float away = Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) : -Mathf.Sign(transform.position.x);
            velocity.x = away * slideOffSpeed;
        }

        if (jumpRequested && IsGrounded && !MovementLocked && (stamina == null || stamina.TrySpend(jumpStaminaCost)))
            velocity.y = jumpVelocity;
        jumpRequested = false;

        rb.linearVelocity = velocity;

        // Turning only happens in the air: holding the opposite direction mid-jump flips the fighter.
        if (MovementLocked || IsGrounded) return;
        if (moveInput > 0.01f) SetFacing(1);
        else if (moveInput < -0.01f) SetFacing(-1);
    }

    bool CheckGrounded()
    {
        Bounds bounds = col.bounds;
        Vector2 center = new Vector2(bounds.center.x, bounds.min.y - groundCheckDepth * 0.5f);
        Vector2 size = new Vector2(bounds.size.x * 0.9f, groundCheckDepth);
        return Physics2D.OverlapBox(center, size, 0f, groundLayer) != null;
    }

    // True if another fighter is right under this one's feet; gives that fighter's x.
    bool FighterBelow(out float otherX)
    {
        otherX = 0f;
        Bounds bounds = col.bounds;
        Vector2 center = new Vector2(bounds.center.x, bounds.min.y - groundCheckDepth * 0.5f);
        Physics2D.OverlapBox(center, new Vector2(bounds.size.x * 0.9f, groundCheckDepth), 0f, ContactFilter2D.noFilter, below);
        foreach (Collider2D c in below)
        {
            Fighter other = c.GetComponentInParent<Fighter>();
            if (other == null || other == this) continue;
            otherX = other.transform.position.x;
            return true;
        }
        return false;
    }

    void SetFacing(int direction)
    {
        if (direction == FacingDirection) return;
        FacingDirection = direction;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds bounds = c.bounds;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(
            new Vector3(bounds.center.x, bounds.min.y - groundCheckDepth * 0.5f),
            new Vector3(bounds.size.x * 0.9f, groundCheckDepth));
    }
}
