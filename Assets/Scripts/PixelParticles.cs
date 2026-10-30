using UnityEngine;

// Pooled pixel particles (one glowing square each) for sparks, frost, shimmer and glitch bits.
// Runs on scaled time, so particles freeze during hit-pause.
public class PixelParticles : MonoBehaviour
{
    public static PixelParticles Instance { get; private set; }

    [SerializeField] Material glowMaterial;
    [SerializeField] int poolSize = 192;
    [SerializeField] float pixelsPerUnit = 22.4f;
    [SerializeField] int sortingOrder = 20;
    [Tooltip("Extra streak length in pixels per unit/second of speed.")]
    [SerializeField] float stretchPerSpeed = 0.5f;

    struct Particle
    {
        public Vector2 position, velocity;
        public Color color;
        public float life, maxLife, size, gravity, drag;
        public bool stretch;
    }

    Particle[] particles;
    SpriteRenderer[] renderers;
    int next;

    void Awake()
    {
        Instance = this;

        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);

        particles = new Particle[poolSize];
        renderers = new SpriteRenderer[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("Pixel");
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixel;
            sr.sharedMaterial = glowMaterial;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            renderers[i] = sr;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // size is in pixels; stretch draws the particle as a streak along its velocity.
    public void Emit(Vector2 position, Vector2 velocity, Color color, float life,
                     float size = 1f, float gravity = 0f, float drag = 0f, bool stretch = false)
    {
        int i = next;
        next = (next + 1) % poolSize;
        particles[i] = new Particle
        {
            position = position, velocity = velocity, color = color,
            life = life, maxLife = life, size = size, gravity = gravity, drag = drag, stretch = stretch,
        };
        renderers[i].enabled = true;
        Place(i);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < poolSize; i++)
        {
            if (!renderers[i].enabled) continue;

            ref Particle p = ref particles[i];
            p.life -= dt;
            if (p.life <= 0f)
            {
                renderers[i].enabled = false;
                continue;
            }
            p.velocity.y -= p.gravity * dt;
            p.velocity /= 1f + p.drag * dt;
            p.position += p.velocity * dt;
            Place(i);
        }
    }

    void Place(int i)
    {
        ref Particle p = ref particles[i];
        Transform t = renderers[i].transform;
        float speed = p.velocity.magnitude;

        if (p.stretch && speed > 0.01f)
        {
            t.position = p.position;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(p.size + speed * stretchPerSpeed, p.size, 1f);
        }
        else
        {
            // Snap to the pixel grid so still particles look like pixel art.
            t.position = new Vector2(Mathf.Round(p.position.x * pixelsPerUnit), Mathf.Round(p.position.y * pixelsPerUnit)) / pixelsPerUnit;
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(p.size, p.size, 1f);
        }

        Color c = p.color;
        c.a *= p.life / p.maxLife;
        renderers[i].color = c;
    }
}
