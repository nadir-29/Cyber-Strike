using UnityEngine;

// Cyan energy-shield flash in front of a fighter whenever its guard stops a hit: the shield pops
// in slightly oversized, settles and fades. Visual only; listens to FighterCombat.Blocked.
[RequireComponent(typeof(SpriteRenderer), typeof(FighterCombat))]
public class BlockVisuals : MonoBehaviour
{
    [SerializeField] Sprite shieldSprite;
    [SerializeField] Material glowMaterial;
    [Tooltip("Shield position relative to the fighter, as if facing right.")]
    [SerializeField] Vector2 offset = new Vector2(0.55f, 0.15f);
    [SerializeField] Color shieldColor = new Color(0.4f, 1f, 1f);
    [SerializeField] float flashTime = 0.2f;
    [SerializeField] float popScale = 1.35f;

    FighterCombat combat;
    SpriteRenderer sprite;
    SpriteRenderer shield;
    float timer;

    void Awake()
    {
        combat = GetComponent<FighterCombat>();
        sprite = GetComponent<SpriteRenderer>();

        var go = new GameObject("BlockShield");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset; // the fighter's flipped x scale keeps it on the front side
        shield = go.AddComponent<SpriteRenderer>();
        shield.sprite = shieldSprite;
        if (glowMaterial != null) shield.sharedMaterial = glowMaterial;
        shield.sortingLayerID = sprite.sortingLayerID;
        shield.sortingOrder = sprite.sortingOrder + 2;
        shield.enabled = false;
    }

    void OnEnable() => combat.Blocked += OnBlocked;
    void OnDisable() => combat.Blocked -= OnBlocked;

    void OnBlocked(FighterCombat.HitInfo hit) => timer = flashTime;

    void LateUpdate()
    {
        if (timer <= 0f)
        {
            shield.enabled = false;
            return;
        }
        timer -= Time.unscaledDeltaTime; // keeps flashing through the hit-pause
        float k = 1f - Mathf.Clamp01(timer / flashTime);

        shield.enabled = true;
        shield.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, Mathf.Clamp01(k * 3f));
        Color c = Color.Lerp(Color.white, shieldColor, Mathf.Clamp01(k * 4f));
        c.a = 1f - k * k;
        shield.color = c;
    }
}
