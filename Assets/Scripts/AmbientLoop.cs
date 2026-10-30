using UnityEngine;

// A quiet looping ambience bed (rain), not music. Starts at a random point in the clip and fades in,
// so a Restart doesn't replay the exact same few seconds. Unaffected by slow motion and hit-pause.
public class AmbientLoop : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField, Range(0f, 1f)] float volume = 0.06f;
    [Tooltip("Real seconds to fade in from silence.")]
    [SerializeField] float fadeIn = 1.5f;

    AudioSource source;
    float fade;

    void Start()
    {
        if (clip == null) return;
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.time = Random.Range(0f, clip.length);
        source.Play();
    }

    void Update()
    {
        if (source == null || fade >= 1f) return;
        fade = fadeIn > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeIn) : 1f;
        source.volume = volume * fade;
    }
}
