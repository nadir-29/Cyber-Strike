using System;
using UnityEngine;

// Hit points for a fighter. Reaching 0 means the fighter is knocked out.
public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 100;

    public int Max => maxHealth;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;

    // (current, max)
    public event Action<int, int> Changed;
    public event Action Damaged;
    public event Action Died;

    void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke();

        if (IsDead)
            Died?.Invoke();
    }
}
