using UnityEngine;

// Neon afterimages behind a fighter while it dashes or throws its cyber uppercut: pooled copies of the
// current sprite, tinted and fading out where they were left. The uppercut also bursts neon sparks
// from the rising fist. Visual only; reads Fighter / FighterCombat state and events.
[RequireComponent(typeof(SpriteRenderer), typeof(Fighter), typeof(FighterCombat))]
public class AfterimageTrail : MonoBehaviour
{
    [SerializeField] Material glowMaterial;
    [Tooltip("Dash trail tint; OpponentLoader sets it to the opponent's accent.")]
    [SerializeField] Color dashColor = new Color(0.25f, 0.95f, 1f);
    [SerializeField] Color specialColor = new Color(1f, 0.25f, 0.75f);
    [Tooltip("Scaled seconds between afterimages.")]
    [SerializeField] float interval = 0.03f;
    [Tooltip("Scaled seconds an afterimage takes to fade.")]
    [SerializeField] float fadeTime = 0.22f;
    [SerializeField, Range(0f, 1f)] float startAlpha = 0.65f;
    [SerializeField] int poolSize = 12;

    [Header("Uppercut Sparks")]
    [Tooltip("Fist position relative to the fighter at the apex, as if facing right.")]
    [SerializeField] Vector2 fistOffset = new Vector2(0.45f, 0.9f);
    [SerializeField] Color sparkCore = new Color(1f, 0.9f, 1f);
    [SerializeField] int sparkCount = 18;

    struct Ghost
    {
        public SpriteRenderer renderer;
        public Color color;
        public float life;
    }

    SpriteRenderer sprite;
    Fighter fighter;
    FighterCombat combat;
    Ghost[] ghosts;
    int next;
    float spawnTimer;
    float specialTimer;

    public Color DashColor { get => dashColor; set => dashColor = value; }

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();

        // Ghosts live outside the fighter so they stay where they were left.
        var holder = new GameObject($"{name}Afterimages").transform;
        ghosts = new Ghost[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("Afterimage");
            go.transform.SetParent(holder, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            if (glowMaterial != null) sr.sharedMaterial = glowMaterial;
            sr.sortingLayerID = sprite.sortingLayerID;
            sr.sortingOrder = sprite.sortingOrder - 2;
            sr.enabled = false;
            ghosts[i].renderer = sr;
        }
    }

    void OnEnable()
    {
        fighter.Dashed += OnDashed;
        combat.AttackStarted += OnAttackStarted;
        combat.HitTaken += OnHitTaken;
    }

    void OnDisable()
    {
        fighter.Dashed -= OnDashed;
        combat.AttackStarted -= OnAttackStarted;
        combat.HitTaken -= OnHitTaken;
    }

    void OnDestroy()
    {
        if (ghosts != null && ghosts.Length > 0 && ghosts[0].renderer != null)
            Destroy(ghosts[0].renderer.transform.parent.gameObject);
    }

    void OnDashed(int direction) => spawnTimer = 0f;

    void OnAttackStarted(AttackType type)
    {
        if (type != AttackType.Special) return;
        specialTimer = 0.45f; // the rise and apex of the uppercut
        spawnTimer = 0f;
        Sparks();
    }

    void OnHitTaken() => specialTimer = 0f;

    void Sparks()
    {
        PixelParticles particles = PixelParticles.Instance;
        if (particles == null) return;

        int facing = fighter.FacingDirection;
        Vector2 fist = (Vector2)transform.position + new Vector2(fistOffset.x * facing, fistOffset.y);
        for (int i = 0; i < sparkCount; i++)
        {
            // A fountain of neon streaks shooting up and forward from the fist.
            float angle = Random.Range(-35f, 35f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(angle) + 0.3f * facing, Mathf.Cos(angle)).normalized;
            Color c = Color.Lerp(i % 2 == 0 ? specialColor : dashColor, sparkCore, Random.Range(0f, 0.4f));
            particles.Emit(fist, dir * Random.Range(4f, 10f), c, Random.Range(0.18f, 0.35f), 1f, 10f, 3f, true);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (specialTimer > 0f) specialTimer -= dt;

        bool trailing = fighter.IsDashing || (specialTimer > 0f && combat.IsAttacking);
        if (trailing)
        {
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = interval;
                Spawn(fighter.IsDashing ? dashColor : specialColor);
            }
        }

        for (int i = 0; i < ghosts.Length; i++)
        {
            ref Ghost g = ref ghosts[i];
            if (!g.renderer.enabled) continue;
            g.life -= dt;
            if (g.life <= 0f)
            {
                g.renderer.enabled = false;
                continue;
            }
            Color c = g.color;
            c.a *= startAlpha * (g.life / fadeTime);
            g.renderer.color = c;
        }
    }

    void Spawn(Color color)
    {
        if (sprite.sprite == null) return;
        ref Ghost g = ref ghosts[next];
        next = (next + 1) % ghosts.Length;

        Transform t = g.renderer.transform;
        t.SetPositionAndRotation(transform.position, transform.rotation);
        t.localScale = transform.lossyScale;
        g.renderer.sprite = sprite.sprite;
        g.renderer.flipX = sprite.flipX;
        g.color = color;
        g.life = fadeTime;
        g.renderer.color = new Color(color.r, color.g, color.b, color.a * startAlpha);
        g.renderer.enabled = true;
    }
}
