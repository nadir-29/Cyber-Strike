using System.Collections.Generic;
using UnityEngine;

// A floating ability pickup. The first fighter with an empty slot to touch it takes it.
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

    Vector3 visualRest;
    float age;
    bool taken;

    public AbilityType Type { get; private set; }
    // Seconds since it spawned.
    public float Age => age;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        visualRest = visual.transform.localPosition;
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public void Init(AbilityType type, Sprite sprite)
    {
        Type = type;
        visual.sprite = sprite;
    }

    void Update()
    {
        age += Time.deltaTime;
        float pop = Mathf.Clamp01(age / spawnPopTime);
        float pulse = 1f + Mathf.Sin(age * pulseSpeed) * pulseAmount;
        visual.transform.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, pop) * pulse);
        visual.transform.localPosition = visualRest + Vector3.up * (Mathf.Sin(age * bobSpeed) * bobHeight);
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
