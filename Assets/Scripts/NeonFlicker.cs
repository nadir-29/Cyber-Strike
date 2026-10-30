using UnityEngine;

// Makes the alley's neon signs misbehave now and then: every few seconds a random idle sign buzzes,
// glitches or briefly cuts out, with a quiet electric buzz. Never all signs at once: at most
// maxAtOnce (and always at least one sign fewer than there are) flicker together.
public class NeonFlicker : MonoBehaviour
{
    [SerializeField] NeonSign[] signs;
    [Tooltip("Seconds between flicker events.")]
    [SerializeField] Vector2 interval = new Vector2(1.2f, 4f);
    [SerializeField] int maxAtOnce = 2;

    [Header("Effects (relative chance, duration range in seconds)")]
    [SerializeField] float buzzChance = 0.5f;
    [SerializeField] Vector2 buzzTime = new Vector2(0.4f, 1.2f);
    [SerializeField] float glitchChance = 0.3f;
    [SerializeField] Vector2 glitchTime = new Vector2(0.15f, 0.45f);
    [SerializeField] float cutOutChance = 0.2f;
    [SerializeField] Vector2 cutOutTime = new Vector2(0.5f, 1.8f);

    [Header("Sound")]
    [SerializeField] AudioClip buzz;
    [SerializeField, Range(0f, 1f)] float buzzVolume = 0.15f;
    [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.12f;

    AudioSource source;
    float timer;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        timer = Random.Range(interval.x, interval.y);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(interval.x, interval.y);
        Trigger();
    }

    void Trigger()
    {
        if (signs == null || signs.Length < 2) return;

        int busy = 0, idle = 0;
        foreach (NeonSign sign in signs)
        {
            if (sign == null) continue;
            if (sign.Busy) busy++; else idle++;
        }
        if (idle == 0 || busy >= Mathf.Min(maxAtOnce, signs.Length - 1)) return;

        // Pick a random idle sign.
        int pick = Random.Range(0, idle);
        NeonSign target = null;
        foreach (NeonSign sign in signs)
        {
            if (sign == null || sign.Busy) continue;
            if (pick-- == 0) { target = sign; break; }
        }

        float roll = Random.value * (buzzChance + glitchChance + cutOutChance);
        if (roll < buzzChance) target.Play(NeonSign.Effect.Buzz, Random.Range(buzzTime.x, buzzTime.y));
        else if (roll < buzzChance + glitchChance) target.Play(NeonSign.Effect.Glitch, Random.Range(glitchTime.x, glitchTime.y));
        else target.Play(NeonSign.Effect.CutOut, Random.Range(cutOutTime.x, cutOutTime.y));

        if (buzz != null)
        {
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(buzz, buzzVolume);
        }
    }
}
