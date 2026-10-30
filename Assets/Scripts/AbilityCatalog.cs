using System;
using UnityEngine;

public enum AbilityType { None, DoubleDamage, Healing, AbilitySteal, IceStrike }

// Art and colour for each ability, shared by the pickup spawner, HUD icons and fighter tints.
[CreateAssetMenu(menuName = "Cyber Strike/Ability Catalog")]
public class AbilityCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AbilityType type;
        public Sprite pickupSprite;
        public Sprite icon;
        public Color color = Color.white;
    }

    [SerializeField] Entry[] entries;

    public Entry[] Entries => entries;

    public Entry Get(AbilityType type)
    {
        foreach (Entry entry in entries)
            if (entry.type == type) return entry;
        return null;
    }
}
