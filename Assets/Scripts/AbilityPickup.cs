using System.Collections.Generic;
using UnityEngine;

// A floating ability pickup. The first fighter with a free ability slot to touch it takes it.
// Ground pickups bob in place. Falling pickups drop from the sky straight down, glowing and trailing
// sparks in their ability colour; a fighter can grab one mid-air by jumping into it, or pick it up
// once it has landed (it then bobs like a ground pickup).
[RequireComponent(typeof(Collider2D))]
public class AbilityPickup : MonoBehaviour
{
    static readonly List<AbilityPickup> active = new List<AbilityPickup>();
    // Pickups currently in the arena (used by the AI to find them).
    public static IReadOnlyList<AbilityPickup> Active => active;

    [SerializeField] SpriteRenderer visual;

    [Header("Feel")]
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 3f;
    [SerializeField] float pulseAmount = 0.08f;
    [SerializeField] float pulseSpeed = 6f;
    [SerializeField] float spawnPopTime = 0.25f;

    [Header("Falling")]
    [Tooltip("Seconds between trail sparks while falling.")]
    [SerializeField] float trailInterval = 0.03f;
    [SerializeField] float fallingPulseAmount = 0.15f;
    [SerializeField] float fallingPulseSpeed = 14f;
    [SerializeField] int landingSparks = 14;

    Vector3 visualRest;
    float age;
    bool taken;
    float landY;
    float trailTimer;
    float bobAge;
    Color glow = Color.white;

    public AbilityType Type { get; private set; }
    // Seconds since it spawned.
    public float Age => age;
    public bool IsFalling { get; private set; }
    public float FallSpeed { get; private set; }
    // Where it rests on the ground (its current position once landed).
    public Vector2 LandingPoint => new Vector2(transform.position.x, IsFalling ? landY : transform.position.y);

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        // A moving trigger needs a body so touches register while it falls.
        if (!TryGetComponent(out Rigidbody2D body)) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        visualRest = visual.transform.localPosition;
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public void Init(AbilityType type, Sprite sprite, Color color)
    {
        Type = type;
        visual.sprite = sprite;
        glow = color;
    }

    // Drops from where it is down to landY at speed units per second.
    public void StartFalling(float groundY, float speed)
    {
        landY = groundY;
        FallSpeed = speed;
        IsFalling = transform.position.y > groundY;
    }

    void Update()
    {
        age += Time.deltaTime;
        float pop = Mathf.Clamp01(age / spawnPopTime);
        float popScale = Mathf.SmoothStep(0f, 1f, pop);

        if (IsFalling)
        {
            Fall();
            float pulse = 1f + Mathf.Sin(age * fallingPulseSpeed) * fallingPulseAmount;
            visual.transform.localScale = Vector3.one * (popScale * pulse);
            visual.transform.localPosition = visualRest;
            return;
        }

        bobAge += Time.deltaTime;
        float bobPulse = 1f + Mathf.Sin(bobAge * pulseSpeed) * pulseAmount;
        visual.transform.localScale = Vector3.one * (popScale * bobPulse);
        visual.transform.localPosition = visualRest + Vector3.up * (Mathf.Sin(bobAge * bobSpeed) * bobHeight);
    }

    void Fall()
    {
        Vector3 p = transform.position;
        p.y = Mathf.Max(landY, p.y - FallSpeed * Time.deltaTime);
        transform.position = p;

        PixelParticles particles = PixelParticles.Instance;
        trailTimer -= Time.deltaTime;
        if (particles != null && trailTimer <= 0f && Time.deltaTime > 0f)
        {
            trailTimer = trailInterval;
            Vector2 at = (Vector2)visual.transform.position + new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.25f));
            Color c = Random.value < 0.25f ? Color.white : glow;
            particles.Emit(at, new Vector2(0f, Random.Range(0.6f, 1.6f)), c, Random.Range(0.25f, 0.45f), 1f, 0f, 1f, true);
        }

        if (p.y > landY) return;
        IsFalling = false;
        if (particles == null) return;
        // Touchdown: sparks splash out sideways along the floor.
        for (int i = 0; i < landingSparks; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector2 v = new Vector2(side * Random.Range(1.5f, 4f), Random.Range(0.5f, 3f));
            particles.Emit(visual.transform.position, v, Random.value < 0.3f ? Color.white : glow, Random.Range(0.2f, 0.4f), 1f, 8f, 1f);
        }
    }

    void OnTriggerEnter2D(Collider2D other) => TryGive(other);
    void OnTriggerStay2D(Collider2D other) => TryGive(other);

    void TryGive(Collider2D other)
    {
        if (taken) return;
        FighterAbilities abilities = other.GetComponentInParent<FighterAbilities>();
        if (abilities == null || !abilities.TryPickUp(Type)) return;

        taken = true;
        Destroy(gameObject);
    }
}
