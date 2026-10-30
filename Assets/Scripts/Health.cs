using System;
using UnityEngine;

// Hit points for a fighter. Reaching 0 means the fighter is knocked out.
public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 100;

    public int Max => maxHealth;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    // Direction the last hit pushed this fighter: -1 = left, 1 = right, 0 = unknown.
    public int LastHitDirection { get; private set; }

    // (current, max)
    public event Action<int, int> Changed;
    public event Action Damaged;
    public event Action Died;

    void Awake()
    {
        Current = maxHealth;
    }

    // Restores HP, capped at max.
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }

    public void TakeDamage(int amount, int hitDirection = 0)
    {
        if (IsDead || amount <= 0) return;

        LastHitDirection = hitDirection;
        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke();

        if (IsDead)
            Died?.Invoke();
    }
}
