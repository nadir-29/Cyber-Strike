using UnityEngine;

// Tiny pixel splashes where rain hits the alley floor: a short flat flash plus two droplets that
// hop outward and fall back. Pooled and drawn as one mesh (one draw call); PixelRain calls Spawn.
// Runs on scaled time like the rain. Keep the object at the world origin.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RainSplashes : MonoBehaviour
{
    [Tooltip("Max splashes alive at once; the oldest is reused when full.")]
    [SerializeField] int poolSize = 64;
    [SerializeField] float life = 0.3f;
    [Tooltip("How long the flat impact flash shows.")]
    [SerializeField] float flashTime = 0.1f;
    [Tooltip("Droplets' initial upward speed range in units/second (sideways is 60% of it).")]
    [SerializeField] Vector2 hopSpeed = new Vector2(1.3f, 2.3f);
    [SerializeField] float gravity = 14f;
    [SerializeField] Color color = new Color(0.75f, 0.9f, 1f, 0.55f);
    [SerializeField] float pixelsPerUnit = 30f;
    [SerializeField] int sortingOrder = -4;

    struct Splash
    {
        public Vector2 origin;
        public float age, vx, vy;
        public bool alive;
    }

    const int QuadsPer = 3; // flash, left droplet, right droplet

    Splash[] pool;
    Vector3[] vertices;
    Color32[] colors;
    Mesh mesh;
    int next;

    void Awake()
    {
        pool = new Splash[poolSize];
        int quads = poolSize * QuadsPer;
        vertices = new Vector3[quads * 4];
        colors = new Color32[quads * 4];
        var triangles = new int[quads * 6];
        for (int q = 0; q < quads; q++)
        {
            int v = q * 4, t = q * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        mesh = new Mesh { name = "RainSplashes" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1f));
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    public void Spawn(Vector2 position)
    {
        pool[next] = new Splash
        {
            origin = position, alive = true,
            vx = Random.Range(hopSpeed.x, hopSpeed.y) * 0.6f,
            vy = Random.Range(hopSpeed.x, hopSpeed.y),
        };
        next = (next + 1) % pool.Length;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float px = 1f / pixelsPerUnit;

        for (int i = 0; i < pool.Length; i++)
        {
            ref Splash s = ref pool[i];
            int q = i * QuadsPer;
            if (s.alive && dt > 0f)
            {
                s.age += dt;
                if (s.age >= life) s.alive = false;
            }
            if (!s.alive)
            {
                for (int k = 0; k < QuadsPer; k++) Quad(q + k, Vector2.zero, 0f, 0f, default);
                continue;
            }

            float fade = 1f - s.age / life;
            Color c = color;
            c.a *= fade;

            // Flat 3 px flash at the impact point.
            if (s.age < flashTime) Quad(q, s.origin + new Vector2(-px, 0f), 3f * px, px, c);
            else Quad(q, Vector2.zero, 0f, 0f, default);

            // Two droplets hopping out to either side; hidden once they fall back below the floor.
            float t = s.age;
            float dy = s.vy * t - 0.5f * gravity * t * t;
            for (int side = 0; side < 2; side++)
            {
                if (dy < 0f) { Quad(q + 1 + side, Vector2.zero, 0f, 0f, default); continue; }
                float dx = (side == 0 ? -1f : 1f) * s.vx * t;
                Quad(q + 1 + side, s.origin + new Vector2(dx, dy + px), px, px, c);
            }
        }

        mesh.vertices = vertices;
        mesh.colors32 = colors;
    }

    // One axis-aligned pixel quad snapped to the grid; zero size hides it.
    void Quad(int quad, Vector2 position, float w, float h, Color32 c)
    {
        float x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
        float y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
        int v = quad * 4;
        vertices[v] = new Vector3(x, y);
        vertices[v + 1] = new Vector3(x + w, y);
        vertices[v + 2] = new Vector3(x + w, y + h);
        vertices[v + 3] = new Vector3(x, y + h);
        for (int k = 0; k < 4; k++) colors[v + k] = c;
    }
}
