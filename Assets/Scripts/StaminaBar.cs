using UnityEngine;
using UnityEngine.UI;

// HUD stamina bar for one fighter. Display only: reads Stamina, never changes it.
// Flashes when the fighter tries an action it can't afford.
public class StaminaBar : MonoBehaviour
{
    [SerializeField] Stamina target;
    [SerializeField] Image fill;

    [Header("Feel")]
    [SerializeField] float flashTime = 0.3f;
    [SerializeField] Color exhaustedColor = new Color(1f, 0.2f, 0.3f);

    Color fillColor;
    float flashTimer;

    void Awake()
    {
        fillColor = fill.color;
    }

    void OnEnable()
    {
        if (target == null) return;
        target.Changed += OnChanged;
        target.Exhausted += OnExhausted;
    }

    void OnDisable()
    {
        if (target == null) return;
        target.Changed -= OnChanged;
        target.Exhausted -= OnExhausted;
    }

    void Start()
    {
        fill.fillAmount = target != null ? target.Fraction : 1f;
    }

    void OnChanged(float current, float max)
    {
        fill.fillAmount = current / max;
    }

    void OnExhausted()
    {
        flashTimer = flashTime;
    }

    void Update()
    {
        if (flashTimer <= 0f) return;

        flashTimer -= Time.unscaledDeltaTime;
        bool on = flashTimer > 0f && Mathf.Repeat(flashTimer, 0.1f) > 0.05f;
        fill.color = on ? exhaustedColor : fillColor;
    }
}
