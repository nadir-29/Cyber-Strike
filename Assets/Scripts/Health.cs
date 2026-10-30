using System;
using UnityEngine;

// Hit points for a fighter. Reaching 0 means the fighter is knocked out (Lives may then revive it).
// While invincible (just after a respawn) it takes no damage.
public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 100;

    float invincibleTimer;

    public int Max => maxHealth;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    public bool IsInvincible => invincibleTimer > 0f;
    // Direction the last hit pushed this fighter: -1 = left, 1 = right, 0 = unknown.
    public int LastHitDirection { get; private set; }

    // (current, max)
    public event Action<int, int> Changed;
    public event Action Damaged;
    public event Action Died;
    // Raised when a knocked-out fighter comes back with full HP.
    public event Action Revived;

    void Awake()
    {
        Current = maxHealth;
    }

    void Update()
    {
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
    }

    // Brings a knocked-out fighter back at full HP, invincible for a while.
    public void Revive(float invincibleTime)
    {
        if (!IsDead) return;
        Current = maxHealth;
        LastHitDirection = 0;
        invincibleTimer = invincibleTime;
        Changed?.Invoke(Current, maxHealth);
        Revived?.Invoke();
    }

    // Changes max HP and refills to it, e.g. for an opponent's HP bonus before the match starts.
    public void SetMax(int max)
    {
        maxHealth = Mathf.Max(1, max);
        Current = maxHealth;
        Changed?.Invoke(Current, maxHealth);
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
        if (IsDead || IsInvincible || amount <= 0) return;

        LastHitDirection = hitDirection;
        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke();

        if (IsDead)
            Died?.Invoke();
    }
}
