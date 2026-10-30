using System;
using UnityEngine;

// Special meter for a fighter. Fills from damage dealt (hits that land) and damage taken (including
// blocked chip damage); FighterCombat spends all of it on the cyber uppercut. The uppercut itself
// builds no meter for its user, so specials can't chain.
[RequireComponent(typeof(Health), typeof(FighterCombat))]
public class SpecialMeter : MonoBehaviour
{
    [SerializeField] float maxMeter = 100f;
    [Tooltip("Meter gained per point of damage this fighter deals.")]
    [SerializeField] float perDamageDealt = 2f;
    [Tooltip("Meter gained per point of damage this fighter takes.")]
    [SerializeField] float perDamageTaken = 1.5f;

    Health health;
    FighterCombat combat;
    int lastHealth;

    public float Max => maxMeter;
    public float Current { get; private set; }
    public float Fraction => Current / maxMeter;
    public bool IsFull => Current >= maxMeter;

    // (current, max)
    public event Action<float, float> Changed;
    // Raised once each time the meter tops up.
    public event Action Filled;

    void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<FighterCombat>();
    }

    void OnEnable()
    {
        health.Changed += OnHealthChanged;
        combat.HitLanded += OnHitLanded;
    }

    void OnDisable()
    {
        health.Changed -= OnHealthChanged;
        combat.HitLanded -= OnHitLanded;
    }

    void Start()
    {
        lastHealth = health.Current; // after any max HP bonus
    }

    // Empties the meter if it is full. Returns false (and spends nothing) otherwise.
    public bool TrySpend()
    {
        if (!IsFull) return false;
        Current = 0f;
        Changed?.Invoke(Current, maxMeter);
        return true;
    }

    void OnHitLanded(FighterCombat.HitInfo hit)
    {
        if (hit.type == AttackType.Special || hit.blocked) return;
        Add(hit.damage * perDamageDealt);
    }

    void OnHealthChanged(int current, int max)
    {
        int lost = lastHealth - current;
        lastHealth = current;
        if (lost > 0) Add(lost * perDamageTaken);
    }

    void Add(float amount)
    {
        if (amount <= 0f || IsFull || health.IsDead) return;
        Current = Mathf.Min(maxMeter, Current + amount);
        Changed?.Invoke(Current, maxMeter);
        if (IsFull) Filled?.Invoke();
    }
}
