using UnityEngine;

// Shared fighter logic for both the player and the AI.
// Input controllers (PlayerInputController / AIController) only call SetMoveInput and Jump.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Fighter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float jumpVelocity = 12f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDepth = 0.05f;

    Rigidbody2D rb;
    Collider2D col;
    float moveInput;
    bool jumpRequested;

    public bool IsGrounded { get; private set; }
    public int FacingDirection { get; private set; } = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;

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

    void FixedUpdate()
    {
        IsGrounded = CheckGrounded();

        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInput * moveSpeed;

        if (jumpRequested && IsGrounded)
            velocity.y = jumpVelocity;
        jumpRequested = false;

        rb.linearVelocity = velocity;

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
