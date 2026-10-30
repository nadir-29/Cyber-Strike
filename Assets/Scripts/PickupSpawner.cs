using UnityEngine;

// Spawns a random ability pickup at a free spawn point every interval.
public class PickupSpawner : MonoBehaviour
{
    [SerializeField] AbilityPickup pickupPrefab;
    [SerializeField] AbilityCatalog catalog;
    [SerializeField] Transform[] spawnPoints;
    [SerializeField] float interval = 10f;

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
        // Pick a random free point; skip this spawn if every point already has a pickup.
        int point = -1, free = 0;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawned[i] != null) continue;
            free++;
            if (Random.Range(0, free) == 0) point = i;
        }
        if (point < 0) return;

        AbilityCatalog.Entry entry = RandomEntry();
        if (entry == null) return;

        AbilityPickup pickup = Instantiate(pickupPrefab, spawnPoints[point].position, Quaternion.identity, transform);
        pickup.Init(entry.type, entry.pickupSprite);
        spawned[point] = pickup;
    }

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
    }
}
