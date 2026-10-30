using UnityEngine;

// One layer of full-screen pixel rain: a single mesh of pixel-snapped, slanted streaks rebuilt every
// frame, so a whole layer is one draw call with no per-drop objects. Covers the camera view.
// Runs on scaled time, so it freezes during hit-pause and slows with the knockout slow motion.
// Keep the object at the world origin; the mesh is built in world coordinates.
// Sorted behind the fighters (sortingOrder), so it never covers them or the UI.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PixelRain : MonoBehaviour
{
    [SerializeField] int drops = 150;
    [Tooltip("Fall speed range in units/second.")]
    [SerializeField] Vector2 speed = new Vector2(9f, 12f);
    [Tooltip("Sideways drift per unit of fall (wind slant).")]
    [SerializeField] float slant = -0.12f;
    [Tooltip("Streak length range in pixels.")]
    [SerializeField] Vector2 length = new Vector2(4f, 7f);
    [Tooltip("Streak width in pixels.")]
    [SerializeField] float width = 1f;
    [SerializeField] Color color = new Color(0.6f, 0.8f, 1f, 0.25f);
    [Tooltip("Each drop's alpha is scaled by a random 1 - this .. 1.")]
    [SerializeField, Range(0f, 1f)] float alphaVariation = 0.4f;
    [SerializeField] float pixelsPerUnit = 30f;
    [SerializeField] int sortingOrder = -15;

    [Header("Splashes")]
    [Tooltip("Optional. When set, each drop lands somewhere on the floor and splashes there.")]
    [SerializeField] RainSplashes splashes;
    [Tooltip("World y range of the alley floor where drops land.")]
    [SerializeField] Vector2 floorY = new Vector2(-4.95f, -2.6f);

    struct Drop
    {
        public float x, y, speed, length, landY;
    }

    const float Margin = 1f; // world units beyond the camera edges

    Drop[] pool;
    Vector3[] vertices;
    Mesh mesh;
    Camera cam;
    Rect area;

    void Awake()
    {
        cam = Camera.main;
        UpdateArea();

        pool = new Drop[drops];
        vertices = new Vector3[drops * 4];
        var colors = new Color32[drops * 4];
        var triangles = new int[drops * 6];
        for (int i = 0; i < drops; i++)
        {
            Respawn(ref pool[i], Random.Range(area.yMin, area.yMax));
            Color c = color;
            c.a *= Random.Range(1f - alphaVariation, 1f);
            for (int k = 0; k < 4; k++) colors[i * 4 + k] = c;
            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        mesh = new Mesh { name = "PixelRain" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    // The view the rain must cover, padded so slanted streaks enter from beyond the edges.
    void UpdateArea()
    {
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);
        Vector2 center = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        float drift = Mathf.Abs(slant) * halfH * 2f;
        area = Rect.MinMaxRect(center.x - halfW - Margin - drift, center.y - halfH - Margin,
                               center.x + halfW + Margin + drift, center.y + halfH + Margin);
    }

    void Respawn(ref Drop d, float y)
    {
        d.x = Random.Range(area.xMin, area.xMax);
        d.y = y;
        d.speed = Random.Range(speed.x, speed.y);
        d.length = Random.Range(length.x, length.y) / pixelsPerUnit;
        d.landY = splashes != null ? Random.Range(floorY.x, floorY.y) : area.yMin;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateArea();

        Vector2 dir = new Vector2(slant, -1f).normalized;
        float w = width / pixelsPerUnit;
        for (int i = 0; i < pool.Length; i++)
        {
            ref Drop d = ref pool[i];
            if (dt > 0f)
            {
                float fall = d.speed * dt;
                d.y -= fall;
                d.x += slant * fall;
                if (d.y <= d.landY)
                {
                    if (splashes != null) splashes.Spawn(new Vector2(d.x, d.landY));
                    Respawn(ref d, area.yMax + Random.Range(0f, 1f));
                }
            }

            // Head and tail both snap to the pixel grid so streaks stay crisp.
            float hx = Snap(d.x), hy = Snap(d.y);
            float tx = Snap(d.x - dir.x * d.length), ty = Snap(d.y - dir.y * d.length);
            int v = i * 4;
            vertices[v] = new Vector3(hx, hy);
            vertices[v + 1] = new Vector3(hx + w, hy);
            vertices[v + 2] = new Vector3(tx + w, ty);
            vertices[v + 3] = new Vector3(tx, ty);
        }

        mesh.vertices = vertices;
        mesh.bounds = new Bounds(area.center, new Vector3(area.width, area.height + 2f, 1f));
    }

    float Snap(float value) => Mathf.Round(value * pixelsPerUnit) / pixelsPerUnit;
}
