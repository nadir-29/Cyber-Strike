using UnityEngine;

// Feeds Fighter state into the Animator. Visual only: no gameplay rules here.
[RequireComponent(typeof(Fighter), typeof(Animator))]
public class FighterAnimator : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int VelocityYHash = Animator.StringToHash("VelocityY");

    Fighter fighter;
    Rigidbody2D rb;
    Animator animator;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        Vector2 velocity = rb.linearVelocity;
        animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
        animator.SetBool(GroundedHash, fighter.IsGrounded);
        animator.SetFloat(VelocityYHash, velocity.y);
    }
}
