using System;
using System.Collections;
using UnityEngine;

// Plays every sound effect by listening to fighter events; gameplay code never calls it.
// Menu sounds go through the static PlayUI, which works in every scene and survives scene loads.
public class AudioManager : MonoBehaviour
{
    // Persistent 2D source for UI sounds, created on first use. Kept across scene loads so a button
    // that loads a new scene is still heard; it isn't tied to an AudioManager in the scene.
    static AudioSource uiSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetUISource() => uiSource = null; // domain reload is off: clear the stale reference

    public static void PlayUI(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (uiSource == null)
        {
            var go = new GameObject("UIAudio");
            DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;
        }
        uiSource.PlayOneShot(clip, volume);
    }

    [Serializable]
    class AbilitySound
    {
        public AbilityType type;
        public AudioClip clip;
    }

    [SerializeField] GameObject player;
    [SerializeField] GameObject opponent;

    [Header("Clips")]
    [SerializeField] AudioClip punch;
    [SerializeField] AudioClip kick;
    [SerializeField] AudioClip hit;
    [Tooltip("A hit stopped by a guard.")]
    [SerializeField] AudioClip deflect;
    [Tooltip("Cyber uppercut wind-up.")]
    [SerializeField] AudioClip special;
    [Tooltip("Cyber uppercut landing (replaces the hit sound).")]
    [SerializeField] AudioClip specialHit;
    [Tooltip("Special meter just filled.")]
    [SerializeField] AudioClip specialReady;
    [SerializeField] AudioClip dash;
    [SerializeField] AudioClip pickup;
    [SerializeField] AbilitySound[] abilitySounds;
    [SerializeField] AudioClip win;
    [SerializeField] AudioClip lose;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float attackVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] float hitVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float pickupVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float specialVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] float readyVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] float dashVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] float abilityVolume = 0.8f;
    [Tooltip("Ice Strike sound replayed when its slow lands.")]
    [SerializeField, Range(0f, 1f)] float freezeVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] float jingleVolume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.08f;
    [Tooltip("Real seconds after the knockout before the win/loss jingle plays.")]
    [SerializeField] float jingleDelay = 0.6f;
    [SerializeField] int voices = 8;

    AudioSource[] sources;
    int nextSource;
    bool matchOver;

    void Awake()
    {
        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sources[i] = source;
        }
    }

    void OnEnable()
    {
        Hook(player, true);
        Hook(opponent, true);
        Health playerHealth = Get<Health>(player), opponentHealth = Get<Health>(opponent);
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (opponentHealth != null) opponentHealth.Died += OnOpponentDied;
    }

    void OnDisable()
    {
        Hook(player, false);
        Hook(opponent, false);
        Health playerHealth = Get<Health>(player), opponentHealth = Get<Health>(opponent);
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (opponentHealth != null) opponentHealth.Died -= OnOpponentDied;
    }

    void Hook(GameObject fighter, bool subscribe)
    {
        FighterCombat combat = Get<FighterCombat>(fighter);
        FighterAbilities abilities = Get<FighterAbilities>(fighter);
        Fighter movement = Get<Fighter>(fighter);
        SpecialMeter meter = Get<SpecialMeter>(fighter);

        if (subscribe)
        {
            if (combat != null) { combat.AttackStarted += OnAttackStarted; combat.HitLanded += OnHitLanded; combat.Blocked += OnBlocked; }
            if (abilities != null) { abilities.PickedUp += OnPickedUp; abilities.Used += OnAbilityUsed; }
            if (movement != null) { movement.Slowed += OnSlowed; movement.Dashed += OnDashed; }
            if (meter != null) meter.Filled += OnMeterFilled;
        }
        else
        {
            if (combat != null) { combat.AttackStarted -= OnAttackStarted; combat.HitLanded -= OnHitLanded; combat.Blocked -= OnBlocked; }
            if (abilities != null) { abilities.PickedUp -= OnPickedUp; abilities.Used -= OnAbilityUsed; }
            if (movement != null) { movement.Slowed -= OnSlowed; movement.Dashed -= OnDashed; }
            if (meter != null) meter.Filled -= OnMeterFilled;
        }
    }

    static T Get<T>(GameObject go) where T : Component => go != null ? go.GetComponent<T>() : null;

    void OnAttackStarted(AttackType type)
    {
        if (type == AttackType.Special) Play(special, specialVolume, false);
        else Play(type == AttackType.Punch ? punch : kick, attackVolume, true);
    }

    void OnHitLanded(FighterCombat.HitInfo info)
    {
        if (info.type == AttackType.Special && !info.blocked) Play(specialHit, specialVolume, false);
        else Play(hit, hitVolume, true);
    }

    void OnDashed(int direction) => Play(dash, dashVolume, true);
    void OnMeterFilled() => Play(specialReady, readyVolume, false);
    void OnBlocked(FighterCombat.HitInfo info) => Play(deflect, hitVolume, true);
    void OnPickedUp(AbilityType type) => Play(pickup, pickupVolume, false);
    void OnAbilityUsed(AbilityType type) => Play(AbilityClip(type), abilityVolume, false);
    void OnSlowed() => Play(AbilityClip(AbilityType.IceStrike), freezeVolume, true);
    void OnPlayerDied() => EndMatch(lose);
    void OnOpponentDied() => EndMatch(win);

    void EndMatch(AudioClip jingle)
    {
        if (matchOver) return;
        matchOver = true;
        StartCoroutine(PlayAfter(jingle, jingleDelay));
    }

    IEnumerator PlayAfter(AudioClip clip, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Play(clip, jingleVolume, false);
    }

    AudioClip AbilityClip(AbilityType type)
    {
        foreach (AbilitySound sound in abilitySounds)
            if (sound.type == type) return sound.clip;
        return null;
    }

    void Play(AudioClip clip, float volume, bool varyPitch)
    {
        if (clip == null) return;
        AudioSource source = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        source.pitch = varyPitch ? 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation) : 1f;
        source.PlayOneShot(clip, volume);
    }
}
