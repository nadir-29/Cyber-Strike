using System.Collections.Generic;
using UnityEngine;

// Re-skins a fighter without new animation clips: after the Animator sets a base-sheet sprite,
// swaps it for the skin sprite with the same frame name. Names match after the first two
// "_"-separated parts, e.g. fighter_ai_12 -> fighter_cyborg_12, fighter_ai_ko_0 -> fighter_cyborg_ko_0.
// Visual only. Runs early so AbilityVisuals' glow layers copy the skinned sprite.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(SpriteRenderer))]
public class FighterSkin : MonoBehaviour
{
    [SerializeField] Sprite[] skin;

    SpriteRenderer sprite;
    readonly Dictionary<string, Sprite> byKey = new Dictionary<string, Sprite>();
    readonly Dictionary<Sprite, Sprite> swaps = new Dictionary<Sprite, Sprite>();

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        SetSkin(skin);
    }

    public void SetSkin(Sprite[] sprites)
    {
        skin = sprites;
        byKey.Clear();
        swaps.Clear();
        if (skin == null) return;
        foreach (Sprite s in skin)
            if (s != null) byKey[Key(s)] = s;
    }

    void LateUpdate()
    {
        Sprite current = sprite.sprite;
        if (current == null || byKey.Count == 0) return;

        if (!swaps.TryGetValue(current, out Sprite swapped))
        {
            byKey.TryGetValue(Key(current), out swapped);
            swaps[current] = swapped;
            if (swapped == null)
                Debug.LogWarning($"{name}: FighterSkin has no sprite matching {current.name}.", this);
        }
        if (swapped != null) sprite.sprite = swapped;
    }

    static string Key(Sprite s)
    {
        string n = s.name;
        int first = n.IndexOf('_');
        int second = first < 0 ? -1 : n.IndexOf('_', first + 1);
        return second < 0 ? n : n.Substring(second + 1);
    }
}
