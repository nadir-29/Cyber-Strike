using UnityEngine;
using UnityEngine.UI;

// HUD slot showing a fighter's held ability. Display only: reads FighterAbilities, never changes it.
public class AbilityIcon : MonoBehaviour
{
    [SerializeField] FighterAbilities target;
    [SerializeField] AbilityCatalog catalog;
    [SerializeField] Image icon;

    [Header("Feel")]
    [SerializeField] float popTime = 0.2f;
    [SerializeField] float popScale = 1.4f;
    [SerializeField] float shakeTime = 0.25f;
    [SerializeField] float shakeAmount = 4f;

    RectTransform iconRect;
    Vector2 restPosition;
    float popTimer;
    float shakeTimer;

    void Awake()
    {
        iconRect = icon.rectTransform;
        restPosition = iconRect.anchoredPosition;
    }

    void OnEnable()
    {
        if (target == null) return;
        target.HeldChanged += OnHeldChanged;
        target.UseRefused += OnUseRefused;
    }

    void OnDisable()
    {
        if (target == null) return;
        target.HeldChanged -= OnHeldChanged;
        target.UseRefused -= OnUseRefused;
    }

    void Start()
    {
        Show(target != null ? target.Held : AbilityType.None);
        popTimer = 0f;
    }

    void OnHeldChanged(AbilityType type)
    {
        Show(type);
        popTimer = popTime;
    }

    void OnUseRefused(AbilityType type)
    {
        shakeTimer = shakeTime;
    }

    void Show(AbilityType type)
    {
        AbilityCatalog.Entry entry = catalog != null ? catalog.Get(type) : null;
        icon.enabled = entry != null && entry.icon != null;
        if (icon.enabled) icon.sprite = entry.icon;
    }

    void Update()
    {
        if (popTimer > 0f) popTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(popTimer / popTime);
        iconRect.localScale = Vector3.one * Mathf.Lerp(1f, popScale, t * t);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float x = shakeTimer > 0f ? Mathf.Sin(shakeTimer * 80f) * shakeAmount : 0f;
            iconRect.anchoredPosition = restPosition + new Vector2(x, 0f);
        }
    }
}
