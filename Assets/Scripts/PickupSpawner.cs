using UnityEngine;

// Spawns a random ability pickup every interval: either on the ground at a free spawn point, or falling
// from the sky at a random x, landing at the spawn points' height.
public class PickupSpawner : MonoBehaviour
{
    [SerializeField] AbilityPickup pickupPrefab;
    [SerializeField] AbilityCatalog catalog;
    [SerializeField] Transform[] spawnPoints;
    [SerializeField] float interval = 10f;

    [Header("Falling From The Sky")]
    [Tooltip("Chance each spawn drops from the sky instead of appearing on the ground.")]
    [SerializeField, Range(0f, 1f)] float fallChance = 0.5f;
    [Tooltip("Height falling pickups start at (just above the top of the screen).")]
    [SerializeField] float skyY = 5.8f;
    [SerializeField] float fallSpeed = 2.6f;
    [Tooltip("Random x range falling pickups drop in.")]
    [SerializeField] Vector2 fallRangeX = new Vector2(-6.5f, 6.5f);
    [Tooltip("At most this many pickups in the arena at once; a spawn is skipped beyond it.")]
    [SerializeField] int maxActive = 4;

    AbilityPickup[] spawned;
    float timer;

    void Awake()
    {
        spawned = new AbilityPickup[spawnPoints.Length];
        timer = interval;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer += interval;
        Spawn();
    }

    void Spawn()
    {
        if (AbilityPickup.Active.Count >= maxActive) return;
        AbilityCatalog.Entry entry = RandomEntry();
        if (entry == null) return;

        int point = RandomFreePoint();
        // From the sky by chance, or whenever every ground point is taken.
        if (point < 0 || Random.value < fallChance)
        {
            Vector3 at = new Vector3(Random.Range(fallRangeX.x, fallRangeX.y), skyY, 0f);
            AbilityPickup falling = Instantiate(pickupPrefab, at, Quaternion.identity, transform);
            falling.Init(entry.type, entry.pickupSprite, entry.color);
            falling.StartFalling(GroundY(), fallSpeed);
            return;
        }

        AbilityPickup pickup = Instantiate(pickupPrefab, spawnPoints[point].position, Quaternion.identity, transform);
        pickup.Init(entry.type, entry.pickupSprite, entry.color);
        spawned[point] = pickup;
    }

    // A random spawn point without a pickup on it, or -1.
    int RandomFreePoint()
    {
        int point = -1, free = 0;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawned[i] != null) continue;
            free++;
            if (Random.Range(0, free) == 0) point = i;
        }
        return point;
    }

    float GroundY() => spawnPoints.Length > 0 ? spawnPoints[0].position.y : -2.95f;

    AbilityCatalog.Entry RandomEntry()
    {
        AbilityCatalog.Entry chosen = null;
        int count = 0;
        foreach (AbilityCatalog.Entry entry in catalog.Entries)
        {
            if (entry.type == AbilityType.None) continue;
            count++;
            if (Random.Range(0, count) == 0) chosen = entry;
        }
        return chosen;
    }

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.8f, 0.8f);
        foreach (Transform point in spawnPoints)
            if (point != null) Gizmos.DrawWireSphere(point.position, 0.4f);
        // Where falling pickups drop.
        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.6f);
        Gizmos.DrawLine(new Vector3(fallRangeX.x, skyY), new Vector3(fallRangeX.y, skyY));
    }
}
