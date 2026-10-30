using UnityEngine;

// Fighter-side ability VFX. Visual only:
// - neon outline flash on pickup / use, pink glow + embers while Double Damage is active,
//   ice outline pulse while Ice Strike is armed
// - frost tint + falling frost bits while slowed (with a burst when the slow lands)
// - green shimmer on Healing, magenta/cyan glitch on Ability Steal (thief and victim)
[RequireComponent(typeof(SpriteRenderer), typeof(FighterAbilities), typeof(Health))]
public class AbilityVisuals : MonoBehaviour
{
    static readonly Vector2[] OutlineDirections =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1),
    };

    [SerializeField] AbilityCatalog catalog;
    [SerializeField] Material glowMaterial;
    [SerializeField] float pixelsPerUnit = 22.4f;
    [SerializeField] float pulseSpeed = 10f;
    [SerializeField] float flashTime = 0.35f;

    [Header("Double Damage")]
    [SerializeField] Color doubleDamageGlow = new Color(1f, 0.25f, 0.75f);

    [Header("Frost (slowed by Ice Strike)")]
    [SerializeField] Color slowedTint = new Color(0.6f, 0.85f, 1f);
    [SerializeField] Color frostColor = new Color(0.65f, 0.92f, 1f);
    [SerializeField] float frostInterval = 0.05f;

    [Header("Heal Shimmer")]
    [SerializeField] Color healColor = new Color(0.35f, 1f, 0.6f);
    [SerializeField] float healTime = 0.8f;

    [Header("Glitch (Ability Steal)")]
    [SerializeField] float glitchTime = 0.45f;
    [SerializeField] Color glitchColorA = new Color(1f, 0.2f, 0.8f);
    [SerializeField] Color glitchColorB = new Color(0.2f, 1f, 1f);
    [Tooltip("Max ghost offset in pixels.")]
    [SerializeField] float glitchOffset = 3f;

    SpriteRenderer sprite;
    FighterAbilities abilities;
    Health health;
    Fighter fighter;
    SpriteRenderer[] outline;
    SpriteRenderer[] ghosts;
    Color baseColor;
    Color flashColor;
    float flashTimer;
    float healTimer;
    float glitchTimer;
    float glitchStepTimer;
    float emitTimer;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        abilities = GetComponent<FighterAbilities>();
        health = GetComponent<Health>();
        fighter = GetComponent<Fighter>();
        baseColor = sprite.color;

        float px = 1f / pixelsPerUnit;
        outline = new SpriteRenderer[OutlineDirections.Length];
        for (int i = 0; i < outline.Length; i++)
            outline[i] = CreateLayer("Glow", OutlineDirections[i] * px, sprite.sortingOrder - 1);
        // Behind the body, so only the offset chromatic fringes show.
        ghosts = new[]
        {
            CreateLayer("GlitchA", Vector2.zero, sprite.sortingOrder - 1),
            CreateLayer("GlitchB", Vector2.zero, sprite.sortingOrder - 1),
        };
    }

    SpriteRenderer CreateLayer(string layerName, Vector2 offset, int order)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = glowMaterial;
        sr.sortingLayerID = sprite.sortingLayerID;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    void OnEnable()
    {
        abilities.PickedUp += Flash;
        abilities.Used += OnUsed;
        abilities.Stolen += OnStolen;
        if (fighter != null) fighter.Slowed += OnSlowed;
    }

    void OnDisable()
    {
        abilities.PickedUp -= Flash;
        abilities.Used -= OnUsed;
        abilities.Stolen -= OnStolen;
        if (fighter != null) fighter.Slowed -= OnSlowed;
    }

    void Flash(AbilityType type)
    {
        flashColor = ColorOf(type);
        flashTimer = flashTime;
    }

    void OnUsed(AbilityType type)
    {
        Flash(type);
        if (type == AbilityType.Healing) healTimer = healTime;
        if (type == AbilityType.AbilitySteal) glitchTimer = glitchTime;
    }

    void OnStolen(AbilityType type)
    {
        glitchTimer = glitchTime;
    }

    void OnSlowed()
    {
        for (int i = 0; i < 16; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Emit(RandomPointOnBody(), dir * Random.Range(1.5f, 4f), frostColor, Random.Range(0.3f, 0.6f), 1f, 2f, 3f);
        }
    }

    void LateUpdate()
    {
        if (health.IsDead)
        {
            // KnockoutEffect owns the colour from here.
            SetLayers(outline, false);
            SetLayers(ghosts, false);
            sprite.enabled = true;
            return;
        }

        float dt = Time.deltaTime;
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        bool slowed = fighter != null && fighter.IsSlowed;
        if (flashTimer > 0f) flashTimer -= dt;
        if (healTimer > 0f) healTimer -= dt;

        // Body tint.
        Color tint = slowed ? slowedTint : baseColor;
        if (healTimer > 0f) tint = Color.Lerp(tint, healColor, 0.5f * healTimer / healTime);
        sprite.color = tint;

        // Outline glow: flash > heal > Double Damage > Ice Strike armed.
        Color glow = Color.clear;
        if (flashTimer > 0f) glow = WithAlpha(flashColor, flashTimer / flashTime);
        else if (healTimer > 0f) glow = WithAlpha(healColor, 0.4f + 0.4f * pulse);
        else if (abilities.DoubleDamageActive) glow = WithAlpha(doubleDamageGlow, 0.45f + 0.45f * pulse);
        else if (abilities.IceStrikeArmed) glow = WithAlpha(ColorOf(AbilityType.IceStrike), 0.3f + 0.4f * pulse);
        UpdateOutline(glow);

        // Ambient particles.
        emitTimer -= dt;
        if (emitTimer <= 0f && dt > 0f)
        {
            emitTimer = frostInterval;
            if (slowed)
                Emit(RandomPointOnBody(), new Vector2(Random.Range(-0.3f, 0.3f), -Random.Range(0.5f, 1.5f)), frostColor, 0.5f);
            if (abilities.DoubleDamageActive && Random.value < 0.6f)
                Emit(RandomPointOnBody(), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1f, 2.5f)), doubleDamageGlow, 0.45f);
            if (healTimer > 0f)
                for (int i = 0; i < 2; i++)
                    Emit(RandomPointOnBody(), new Vector2(0f, Random.Range(1.5f, 3f)),
                         Random.value < 0.3f ? Color.white : healColor, 0.5f, Random.value < 0.25f ? 2f : 1f);
        }

        UpdateGlitch(dt);
    }

    void UpdateOutline(Color glow)
    {
        bool on = glow.a > 0.01f;
        // Eight overlapping copies add up, so each one carries a share of the glow.
        Color each = WithAlpha(glow, glow.a * 0.35f);
        foreach (SpriteRenderer layer in outline)
        {
            layer.enabled = on;
            if (!on) continue;
            layer.sprite = sprite.sprite;
            layer.color = each;
        }
    }

    void UpdateGlitch(float dt)
    {
        if (glitchTimer <= 0f)
        {
            SetLayers(ghosts, false);
            sprite.enabled = true;
            return;
        }

        glitchTimer -= dt;
        glitchStepTimer -= dt;
        foreach (SpriteRenderer ghost in ghosts) ghost.sprite = sprite.sprite;
        if (glitchStepTimer > 0f) return;

        // Re-roll every ~50 ms: chromatic ghosts jump around, the body blinks, data bits streak sideways.
        glitchStepTimer = 0.05f;
        float px = 1f / pixelsPerUnit;
        float strength = Mathf.Clamp01(glitchTimer / glitchTime);
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i].enabled = true;
            float side = i == 0 ? -1f : 1f;
            ghosts[i].transform.localPosition = new Vector2(
                side * Mathf.Round(Random.Range(1f, glitchOffset)) * px,
                Mathf.Round(Random.Range(-1f, 1f)) * px);
            ghosts[i].color = WithAlpha(i == 0 ? glitchColorA : glitchColorB, 0.5f + 0.4f * strength);
        }
        sprite.enabled = Random.value > 0.25f;

        for (int i = 0; i < 3; i++)
        {
            Color c = Random.value < 0.5f ? glitchColorA : glitchColorB;
            Emit(RandomPointOnBody(), new Vector2((Random.value < 0.5f ? -1f : 1f) * Random.Range(4f, 8f), 0f), c, 0.08f, 1f, 0f, 0f, true);
        }
    }

    Vector2 RandomPointOnBody()
    {
        Bounds b = sprite.bounds;
        // Keep to the middle of the frame, where the body is.
        return new Vector2(
            b.center.x + Random.Range(-0.25f, 0.25f) * b.size.x,
            b.center.y + Random.Range(-0.4f, 0.35f) * b.size.y);
    }

    static void Emit(Vector2 position, Vector2 velocity, Color color, float life,
                     float size = 1f, float gravity = 0f, float drag = 0f, bool stretch = false)
    {
        if (PixelParticles.Instance != null)
            PixelParticles.Instance.Emit(position, velocity, color, life, size, gravity, drag, stretch);
    }

    static void SetLayers(SpriteRenderer[] layers, bool on)
    {
        foreach (SpriteRenderer layer in layers) layer.enabled = on;
    }

    static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    Color ColorOf(AbilityType type)
    {
        AbilityCatalog.Entry entry = catalog != null ? catalog.Get(type) : null;
        return entry != null ? entry.color : Color.white;
    }
}
