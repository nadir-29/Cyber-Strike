using System;
using UnityEngine;

// The single ability slot of a fighter and what each ability does.
// Input controllers only call UseAbility(); pickups call TryPickUp().
[RequireComponent(typeof(Health))]
public class FighterAbilities : MonoBehaviour
{
    [SerializeField] FighterAbilities opponent;

    [Header("Double Damage")]
    [SerializeField] float doubleDamageDuration = 5f;

    [Header("Healing")]
    [SerializeField] int healAmount = 20;

    [Header("Ice Strike")]
    [SerializeField] float iceSlowDuration = 3f;
    [Tooltip("Move speed multiplier while slowed.")]
    [SerializeField, Range(0.1f, 1f)] float iceSlowMultiplier = 0.5f;

    Health health;
    float doubleDamageTimer;

    public AbilityType Held { get; private set; }
    public FighterAbilities Opponent => opponent;
    public int HealAmount => healAmount;
    public bool DoubleDamageActive => doubleDamageTimer > 0f;
    public int DamageMultiplier => DoubleDamageActive ? 2 : 1;
    // Armed by using Ice Strike; spent by the next punch that connects.
    public bool IceStrikeArmed { get; private set; }

    public event Action<AbilityType> HeldChanged;
    public event Action<AbilityType> PickedUp;
    public event Action<AbilityType> Used;
    // Raised on the victim when the opponent's Ability Steal takes its ability.
    public event Action<AbilityType> Stolen;
    // Raised when UseAbility did nothing, e.g. Ability Steal with nothing to steal.
    public event Action<AbilityType> UseRefused;

    void Awake()
    {
        health = GetComponent<Health>();
        if (opponent == null)
            Debug.LogWarning($"{name}: Opponent is not set on FighterAbilities, so Ability Steal can never work.", this);
    }

    void Update()
    {
        if (doubleDamageTimer > 0f)
            doubleDamageTimer -= Time.deltaTime;
    }

    // Takes the ability if the slot is empty. Returns false (pickup stays) if already holding one.
    public bool TryPickUp(AbilityType type)
    {
        if (type == AbilityType.None || Held != AbilityType.None || health.IsDead) return false;
        SetHeld(type);
        PickedUp?.Invoke(type);
        return true;
    }

    public void UseAbility()
    {
        if (Held == AbilityType.None || health.IsDead) return;

        AbilityType type = Held;
        switch (type)
        {
            case AbilityType.DoubleDamage:
                doubleDamageTimer = doubleDamageDuration;
                SetHeld(AbilityType.None);
                break;
            case AbilityType.Healing:
                health.Heal(healAmount);
                SetHeld(AbilityType.None);
                break;
            case AbilityType.AbilitySteal:
                // Nothing to steal: keep Ability Steal stored for later.
                if (opponent == null || opponent.Held == AbilityType.None)
                {
                    UseRefused?.Invoke(type);
                    return;
                }
                SetHeld(opponent.TakeHeld());
                break;
            case AbilityType.IceStrike:
                IceStrikeArmed = true;
                SetHeld(AbilityType.None);
                break;
        }
        Used?.Invoke(type);
    }

    // Called by FighterCombat when one of this fighter's punches connects.
    public void OnPunchLanded(Health target)
    {
        if (!IceStrikeArmed) return;
        IceStrikeArmed = false;

        Fighter targetFighter = target.GetComponent<Fighter>();
        if (targetFighter != null)
            targetFighter.ApplySlow(iceSlowMultiplier, iceSlowDuration);
    }

    AbilityType TakeHeld()
    {
        AbilityType taken = Held;
        SetHeld(AbilityType.None);
        Stolen?.Invoke(taken);
        return taken;
    }

    void SetHeld(AbilityType type)
    {
        Held = type;
        HeldChanged?.Invoke(type);
    }
}
