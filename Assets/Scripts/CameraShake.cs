using UnityEngine;

// Small screen shake on the camera. Uses unscaled time so it keeps shaking during hit-pause.
public class CameraShake : MonoBehaviour
{
    [Tooltip("Offsets snap to this grid so the pixel art doesn't swim.")]
    [SerializeField] float pixelsPerUnit = 30f;

    Vector3 restPosition;
    float amplitude;
    float duration;
    float timer;

    void Awake()
    {
        restPosition = transform.localPosition;
    }

    // A weaker shake never cuts off a stronger one that is still running.
    public void Shake(float strength, float time)
    {
        float current = timer > 0f ? amplitude * (timer / duration) : 0f;
        if (strength < current) return;
        amplitude = strength;
        duration = time;
        timer = time;
    }

    void LateUpdate()
    {
        if (timer <= 0f) return;

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            transform.localPosition = restPosition;
            return;
        }

        Vector2 offset = Random.insideUnitCircle * (amplitude * timer / duration);
        offset.x = Mathf.Round(offset.x * pixelsPerUnit) / pixelsPerUnit;
        offset.y = Mathf.Round(offset.y * pixelsPerUnit) / pixelsPerUnit;
        transform.localPosition = restPosition + (Vector3)offset;
    }

    void OnDisable()
    {
        timer = 0f;
        transform.localPosition = restPosition;
    }
}
