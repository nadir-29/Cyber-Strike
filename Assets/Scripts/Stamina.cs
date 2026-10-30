using System;
using UnityEngine;

// Stamina for a fighter. Attacks and jumps spend it; it regenerates after a short pause.
// Refills when the fighter respawns (Health.Revived).
public class Stamina : MonoBehaviour
{
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float regenPerSecond = 30f;
    [Tooltip("Seconds after spending before regeneration starts.")]
    [SerializeField] float regenDelay = 0.6f;

    float regenTimer;
    Health health;

    public float Max => maxStamina;
    public float Current { get; private set; }
    public float Fraction => Current / maxStamina;

    // (current, max)
    public event Action<float, float> Changed;
    // Raised when an action was refused for lack of stamina.
    public event Action Exhausted;

    void Awake()
    {
        Current = maxStamina;
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (health != null) health.Revived += Refill;
    }

    void OnDisable()
    {
        if (health != null) health.Revived -= Refill;
    }

    // A respawned fighter comes back fresh.
    void Refill()
    {
        Current = maxStamina;
        regenTimer = 0f;
        Changed?.Invoke(Current, maxStamina);
    }

    // Spends the amount if there is enough; otherwise spends nothing and returns false.
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current < amount)
        {
            Exhausted?.Invoke();
            return false;
        }

        Current -= amount;
        regenTimer = regenDelay;
        Changed?.Invoke(Current, maxStamina);
        return true;
    }

    void Update()
    {
        if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
            return;
        }
        if (Current >= maxStamina) return;

        Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);
        Changed?.Invoke(Current, maxStamina);
    }
}
