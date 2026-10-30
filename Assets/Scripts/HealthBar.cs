using UnityEngine;
using UnityEngine.UI;

// HUD health bar for one fighter. Display only: reads Health, never changes it.
// The fill drops instantly; a trail behind it drains after a short delay to show the damage taken.
public class HealthBar : MonoBehaviour
{
    [SerializeField] Health target;
    [SerializeField] Image fill;
    [SerializeField] Image trail;

    [Header("Feel")]
    [SerializeField] float trailDelay = 0.4f;
    [SerializeField] float trailSpeed = 0.8f;
    [SerializeField] float shakeTime = 0.15f;
    [SerializeField] float shakeAmount = 6f;
    [SerializeField] float lowHealthFraction = 0.25f;
    [SerializeField] Color lowHealthColor = new Color(1f, 0.2f, 0.3f);

    RectTransform rect;
    Vector2 restPosition;
    Color fillColor;
    float trailTimer;
    float shakeTimer;

    void Awake()
    {
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        fillColor = fill.color;
    }

    void OnEnable()
    {
        if (target != null) target.Changed += OnChanged;
    }

    void OnDisable()
    {
        if (target != null) target.Changed -= OnChanged;
        rect.anchoredPosition = restPosition;
    }

    void Start()
    {
        float amount = target != null ? (float)target.Current / target.Max : 1f;
        fill.fillAmount = amount;
        trail.fillAmount = amount;
    }

    void OnChanged(int current, int max)
    {
        float amount = (float)current / max;
        if (amount < fill.fillAmount)
        {
            trailTimer = trailDelay;
            shakeTimer = shakeTime;
        }
        else
        {
            trail.fillAmount = amount; // healing: no damage trail
        }
        fill.fillAmount = amount;
    }

    void Update()
    {
        if (trailTimer > 0f)
            trailTimer -= Time.deltaTime;
        else
            trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, fill.fillAmount, trailSpeed * Time.deltaTime);

        // Glitchy jolt on each hit.
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            Vector2 jolt = new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * shakeAmount;
            rect.anchoredPosition = shakeTimer > 0f ? restPosition + jolt : restPosition;
        }

        // Pulse toward red when low.
        bool low = fill.fillAmount > 0f && fill.fillAmount <= lowHealthFraction;
        float pulse = low ? (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f : 0f;
        fill.color = Color.Lerp(fillColor, lowHealthColor, pulse);
    }
}
