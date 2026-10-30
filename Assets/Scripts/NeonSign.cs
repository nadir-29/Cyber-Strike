using System.Collections;
using UnityEngine;

// One flickering neon sign: the lit sprite (with glow) on this object, drawn over a dead "off"
// sprite on a child, so dimming the lit sprite reveals the switched-off sign underneath.
// NeonFlicker decides when a sign misbehaves; this only plays the effect.
public class NeonSign : MonoBehaviour
{
    public enum Effect { Buzz, Glitch, CutOut }

    [SerializeField] SpriteRenderer lit;
    [SerializeField] float pixelsPerUnit = 30f;
    [Tooltip("Colour shifts flashed while glitching.")]
    [SerializeField] Color[] glitchTints = { new Color(0.45f, 1f, 1f), new Color(1f, 0.45f, 1f) };

    public bool Busy { get; private set; }

    Vector3 restPosition;
    Color restColor;

    void Awake()
    {
        if (lit == null) lit = GetComponent<SpriteRenderer>();
        restPosition = lit.transform.localPosition;
        restColor = lit.color;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        Restore();
        Busy = false;
    }

    public void Play(Effect effect, float duration)
    {
        if (Busy || !isActiveAndEnabled) return;
        StartCoroutine(Run(effect, duration));
    }

    IEnumerator Run(Effect effect, float duration)
    {
        Busy = true;
        float t = 0f;
        while (t < duration)
        {
            float k = t / duration;
            switch (effect)
            {
                case Effect.Buzz: // nervous dimming, now and then nearly out
                    SetLevel(Random.value < 0.25f ? Random.Range(0.1f, 0.4f) : Random.Range(0.7f, 1f));
                    break;
                case Effect.Glitch: // jumps a pixel or two, colour-shifts, drops frames
                    lit.transform.localPosition = restPosition + new Vector3(
                        Random.Range(-2, 3) / pixelsPerUnit, (Random.value < 0.3f ? Random.Range(-1, 2) : 0) / pixelsPerUnit);
                    Color tint = Random.value < 0.5f && glitchTints.Length > 0
                        ? glitchTints[Random.Range(0, glitchTints.Length)] : Color.white;
                    lit.color = restColor * tint * new Color(1f, 1f, 1f, Random.value < 0.2f ? 0f : 1f);
                    break;
                case Effect.CutOut: // sputters, dies for a moment, sputters back
                    bool sputter = k < 0.2f || k > 0.85f;
                    SetLevel(sputter ? (Random.value < 0.5f ? 1f : 0f) : 0f);
                    break;
            }
            float step = Random.Range(0.03f, 0.08f);
            yield return new WaitForSeconds(step);
            t += step;
        }
        Restore();
        Busy = false;
    }

    void SetLevel(float level)
    {
        Color c = restColor;
        c.a *= level;
        lit.color = c;
    }

    void Restore()
    {
        if (lit == null) return;
        lit.transform.localPosition = restPosition;
        lit.color = restColor;
    }
}
