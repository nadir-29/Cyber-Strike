using System;
using System.Collections;
using UnityEngine;

// Hearts (lives) for a fighter. Reaching 0 HP costs a heart: the knockout plays out briefly, then the
// fighter respawns at its start position with full HP, an empty special meter, its abilities kept and a
// moment of invincibility. Losing the last heart loses the match (MatchManager listens to OutOfHearts).
// The parts reset themselves on Health.Revived (FighterCombat, SpecialMeter, Stamina, KnockoutEffect, ...).
[RequireComponent(typeof(Health), typeof(Fighter))]
public class Lives : MonoBehaviour
{
    [SerializeField] int hearts = MatchSettings.DefaultHearts;
    [Tooltip("Real seconds the knockout plays before the fighter respawns.")]
    [SerializeField] float respawnDelay = 1.8f;
    [Tooltip("Seconds of invincibility (flashing) after respawning.")]
    [SerializeField] float invincibleTime = 1.5f;

    Health health;
    Fighter fighter;
    Vector2 startPosition;
    int startFacing;

    public int Max => hearts;
    public int Current { get; private set; }
    public bool OnLastHeart => Current == 1;
    public bool IsOut => Current <= 0;
    public bool IsRespawning { get; private set; }

    // (current, max)
    public event Action<int, int> Changed;
    public event Action HeartLost;
    public event Action Respawned;
    public event Action OutOfHearts;

    void Awake()
    {
        health = GetComponent<Health>();
        fighter = GetComponent<Fighter>();
        startPosition = transform.position;
        startFacing = fighter.FacingDirection;
        Current = hearts;
    }

    void OnEnable() => health.Died += OnDied;
    void OnDisable() => health.Died -= OnDied;

    // Sets the heart count and refills, before the match starts (MatchManager, from MatchSettings).
    public void SetMax(int max)
    {
        hearts = Mathf.Max(1, max);
        Current = hearts;
        Changed?.Invoke(Current, hearts);
    }

    void OnDied()
    {
        if (IsOut) return;
        Current--;
        Changed?.Invoke(Current, hearts);
        HeartLost?.Invoke();

        if (IsOut) OutOfHearts?.Invoke();
        else StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        IsRespawning = true;
        yield return new WaitForSecondsRealtime(respawnDelay);
        fighter.Respawn(startPosition, startFacing);
        health.Revive(invincibleTime);
        IsRespawning = false;
        Respawned?.Invoke();
    }
}
