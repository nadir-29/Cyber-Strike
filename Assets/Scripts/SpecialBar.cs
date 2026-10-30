using UnityEngine;
using UnityEngine.UI;

// HUD special meter for one fighter. Display only: reads SpecialMeter, never changes it.
// Flashes white when the meter fills, then pulses while it stays full (the uppercut is ready).
public class SpecialBar : MonoBehaviour
{
    [SerializeField] SpecialMeter target;
    [SerializeField] Image fill;

    [Header("Feel")]
    [SerializeField] Color readyColor = new Color(1f, 0.55f, 0.9f);
    [SerializeField] float pulseSpeed = 8f;
    [SerializeField] float fillFlashTime = 0.25f;

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
        target.Filled += OnFilled;
    }

    void OnDisable()
    {
        if (target == null) return;
        target.Changed -= OnChanged;
        target.Filled -= OnFilled;
    }

    void Start()
    {
        fill.fillAmount = target != null ? target.Fraction : 0f;
    }

    void OnChanged(float current, float max)
    {
        fill.fillAmount = current / max;
    }

    void OnFilled()
    {
        flashTimer = fillFlashTime;
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.unscaledDeltaTime;
            fill.color = Color.white;
            return;
        }
        if (target != null && target.IsFull)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            fill.color = Color.Lerp(fillColor, readyColor, k);
            return;
        }
        fill.color = fillColor;
    }
}
