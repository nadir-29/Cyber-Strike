using System;
using UnityEngine;

// The two ability slots of a fighter and what each ability does.
// Input controllers only call UseAbility(slot); pickups call TryPickUp().
// A pickup goes into the first empty slot; with both full the fighter can't pick up.
// Held abilities survive a respawn (Lives).
[RequireComponent(typeof(Health))]
public class FighterAbilities : MonoBehaviour
{
    public const int SlotCount = 2;

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
    readonly AbilityType[] slots = new AbilityType[SlotCount];

    public FighterAbilities Opponent => opponent;
    public int HealAmount => healAmount;
    public bool DoubleDamageActive => doubleDamageTimer > 0f;
    public int DamageMultiplier => DoubleDamageActive ? 2 : 1;
    // Armed by using Ice Strike; spent by the next punch that connects.
    public bool IceStrikeArmed { get; private set; }
    public bool IsFull => slots[0] != AbilityType.None && slots[1] != AbilityType.None;
    public bool HasAny => slots[0] != AbilityType.None || slots[1] != AbilityType.None;

    // (slot, ability now in it)
    public event Action<int, AbilityType> SlotChanged;
    public event Action<AbilityType> PickedUp;
    public event Action<AbilityType> Used;
    // Raised on the victim when the opponent's Ability Steal takes one of its abilities.
    public event Action<AbilityType> Stolen;
    // Raised when UseAbility did nothing, e.g. Ability Steal with nothing to steal: (slot, ability).
    public event Action<int, AbilityType> UseRefused;

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

    public AbilityType Get(int slot) => slot >= 0 && slot < SlotCount ? slots[slot] : AbilityType.None;

    // Slot holding this ability, or -1.
    public int SlotOf(AbilityType type)
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i] == type) return i;
        return -1;
    }

    // Takes the ability into the first empty slot. Returns false (pickup stays) if both slots are full.
    public bool TryPickUp(AbilityType type)
    {
        if (type == AbilityType.None || health.IsDead) return false;
        int slot = SlotOf(AbilityType.None);
        if (slot < 0) return false;
        SetSlot(slot, type);
        PickedUp?.Invoke(type);
        return true;
    }

    public void UseAbility(int slot)
    {
        AbilityType type = Get(slot);
        if (type == AbilityType.None || health.IsDead) return;

        switch (type)
        {
            case AbilityType.DoubleDamage:
                doubleDamageTimer = doubleDamageDuration;
                SetSlot(slot, AbilityType.None);
                break;
            case AbilityType.Healing:
                health.Heal(healAmount);
                SetSlot(slot, AbilityType.None);
                break;
            case AbilityType.AbilitySteal:
                // Nothing to steal: keep Ability Steal stored for later.
                if (opponent == null || !opponent.HasAny)
                {
                    UseRefused?.Invoke(slot, type);
                    return;
                }
                // The stolen ability takes Ability Steal's place.
                SetSlot(slot, opponent.TakeForSteal());
                break;
            case AbilityType.IceStrike:
                IceStrikeArmed = true;
                SetSlot(slot, AbilityType.None);
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

    // Ability Steal takes slot 1, or slot 2 if slot 1 is empty.
    AbilityType TakeForSteal()
    {
        int slot = slots[0] != AbilityType.None ? 0 : 1;
        AbilityType taken = slots[slot];
        SetSlot(slot, AbilityType.None);
        Stolen?.Invoke(taken);
        return taken;
    }

    void SetSlot(int slot, AbilityType type)
    {
        slots[slot] = type;
        SlotChanged?.Invoke(slot, type);
    }
}
