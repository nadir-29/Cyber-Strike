using System.Collections;
using UnityEngine;

// Visual knockout when a fighter reaches 0 HP: brief slow motion, a red flicker,
// and the body toppling over in the direction it was hit. No gameplay rules here;
// the knockback launch itself is done by FighterCombat.
[RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class KnockoutEffect : MonoBehaviour
{
    [Header("Slow Motion")]
    [SerializeField] float slowTimeScale = 0.3f;
    [Tooltip("Real-time seconds the slow motion lasts.")]
    [SerializeField] float slowDuration = 0.8f;

    [Header("Topple")]
    [SerializeField] float toppleDelay = 0.1f;
    [SerializeField] float toppleTime = 0.35f;

    [Header("Flicker")]
    [SerializeField] int flickerCount = 4;
    [SerializeField] float flickerInterval = 0.08f;
    [SerializeField] Color flickerColor = new Color(1f, 0.25f, 0.35f);
    [SerializeField] Color downedColor = new Color(0.6f, 0.6f, 0.7f);

    Health health;
    Rigidbody2D rb;
    SpriteRenderer sprite;
    Fighter fighter;
    bool slowMotionActive;

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        fighter = GetComponent<Fighter>();
    }

    void OnEnable()
    {
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Died -= OnDied;
        EndSlowMotion();
    }

    void OnDied()
    {
        StartCoroutine(SlowMotion());
        StartCoroutine(Flicker());
        StartCoroutine(Topple());
    }

    IEnumerator SlowMotion()
    {
        slowMotionActive = true;
        TimeControl.SetSlowMotion(slowTimeScale);
        yield return new WaitForSecondsRealtime(slowDuration);
        EndSlowMotion();
    }

    void EndSlowMotion()
    {
        if (!slowMotionActive) return;
        slowMotionActive = false;
        TimeControl.SetSlowMotion(1f);
    }

    IEnumerator Flicker()
    {
        Color normal = sprite.color;
        for (int i = 0; i < flickerCount; i++)
        {
            sprite.color = flickerColor;
            yield return new WaitForSeconds(flickerInterval);
            sprite.color = normal;
            yield return new WaitForSeconds(flickerInterval);
        }
        sprite.color = downedColor;
    }

    IEnumerator Topple()
    {
        yield return new WaitForSeconds(toppleDelay);

        // Fall the way the hit pushed us: pushed right = clockwise (negative angle).
        int direction = health.LastHitDirection != 0
            ? health.LastHitDirection
            : -(fighter != null ? fighter.FacingDirection : 1);
        float target = -90f * direction;

        // Set rotation directly: MoveRotation drives a dynamic body by velocity and would cancel the knockback.
        for (float t = 0f; t < toppleTime; t += Time.fixedDeltaTime)
        {
            rb.rotation = Mathf.Lerp(0f, target, Mathf.SmoothStep(0f, 1f, t / toppleTime));
            yield return new WaitForFixedUpdate();
        }
        rb.rotation = target;
    }
}
