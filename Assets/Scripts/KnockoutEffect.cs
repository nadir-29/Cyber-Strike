using System.Collections;
using UnityEngine;

// Visual knockout when a fighter reaches 0 HP: brief slow motion, a red flicker,
// and the body toppling over in the direction it was hit. No gameplay rules here;
// the knockback launch itself is done by FighterCombat.
// Respawn (Health.Revived, from Lives): the body stands back up in a burst of cyan pixels and blinks
// while it is invincible. Runs late so the blink goes over AbilityVisuals' tint.
[DefaultExecutionOrder(100)]
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

    [Header("Respawn")]
    [SerializeField] Color respawnColor = new Color(0.3f, 0.95f, 1f);
    [SerializeField] int respawnSparks = 28;
    [Tooltip("Blinks per second while invincible.")]
    [SerializeField] float blinkRate = 12f;
    [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.25f;

    Health health;
    Rigidbody2D rb;
    SpriteRenderer sprite;
    Fighter fighter;
    Color normalColor;
    bool slowMotionActive;
    bool blinking;

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        fighter = GetComponent<Fighter>();
        normalColor = sprite.color;
    }

    void OnEnable()
    {
        health.Died += OnDied;
        health.Revived += OnRevived;
    }

    void OnDisable()
    {
        health.Died -= OnDied;
        health.Revived -= OnRevived;
        EndSlowMotion();
    }

    void OnDied()
    {
        StartCoroutine(SlowMotion());
        StartCoroutine(Flicker());
        StartCoroutine(Topple());
    }

    void OnRevived()
    {
        StopAllCoroutines();
        EndSlowMotion();
        rb.rotation = 0f;
        sprite.color = normalColor;

        PixelParticles particles = PixelParticles.Instance;
        if (particles == null) return;
        // Pixels shoot up through the body as it rematerializes.
        Bounds b = sprite.bounds;
        for (int i = 0; i < respawnSparks; i++)
        {
            Vector2 p = new Vector2(b.center.x + Random.Range(-0.35f, 0.35f), b.min.y + Random.Range(0f, b.size.y * 0.8f));
            Color c = Random.value < 0.3f ? Color.white : respawnColor;
            particles.Emit(p, new Vector2(0f, Random.Range(2f, 6f)), c, Random.Range(0.25f, 0.5f), 1f, 0f, 2f, true);
        }
    }

    void LateUpdate()
    {
        if (!health.IsInvincible && !blinking) return;
        blinking = health.IsInvincible;
        bool dim = blinking && Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f;
        Color c = sprite.color;
        c.a = dim ? blinkAlpha : 1f;
        sprite.color = c;
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
