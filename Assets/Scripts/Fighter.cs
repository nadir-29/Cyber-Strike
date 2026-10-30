using UnityEngine;

// Shared fighter logic for both the player and the AI.
// Input controllers (PlayerInputController / AIController) only call SetMoveInput, Face, Jump and Dash
// here, and Punch / Kick / Special on FighterCombat.
// Dash: a short, fast burst along the ground with a cooldown. It keeps the current facing, can't start
// while movement is locked (attacking, stunned, guarding) and a hit cuts it short.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Fighter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float jumpVelocity = 12f;
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

    public bool IsGrounded { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    // Set by FighterCombat while attacking, stunned or knocked out.
    public bool MovementLocked { get; set; }
    public bool IsSlowed => slowTimer > 0f;
    public bool IsDashing => dashTimer > 0f;
    public bool CanDash => !IsDashing && dashCooldownTimer <= 0f && !MovementLocked && IsGrounded;

    public event System.Action Slowed;
    // Raised when a dash starts, with its direction (-1 or 1).
    public event System.Action<int> Dashed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        stamina = GetComponent<Stamina>();
        rb.freezeRotation = true;
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

    // Turns to face a direction (-1 or 1) without moving. Ignored while movement is locked.
    public void Face(int direction)
    {
        if (!MovementLocked && direction != 0)
            SetFacing(direction > 0 ? 1 : -1);
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

        if (jumpRequested && IsGrounded && !MovementLocked && (stamina == null || stamina.TrySpend(jumpStaminaCost)))
            velocity.y = jumpVelocity;
        jumpRequested = false;

        rb.linearVelocity = velocity;

        if (MovementLocked) return;
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
