using System;
using System.Collections;
using UnityEngine;

// Plays every sound effect by listening to fighter events; gameplay code never calls it.
public class AudioManager : MonoBehaviour
{
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
    [SerializeField] AudioClip pickup;
    [SerializeField] AbilitySound[] abilitySounds;
    [SerializeField] AudioClip win;
    [SerializeField] AudioClip lose;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float attackVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] float hitVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float pickupVolume = 0.7f;
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

        if (subscribe)
        {
            if (combat != null) { combat.AttackStarted += OnAttackStarted; combat.HitLanded += OnHitLanded; }
            if (abilities != null) { abilities.PickedUp += OnPickedUp; abilities.Used += OnAbilityUsed; }
            if (movement != null) movement.Slowed += OnSlowed;
        }
        else
        {
            if (combat != null) { combat.AttackStarted -= OnAttackStarted; combat.HitLanded -= OnHitLanded; }
            if (abilities != null) { abilities.PickedUp -= OnPickedUp; abilities.Used -= OnAbilityUsed; }
            if (movement != null) movement.Slowed -= OnSlowed;
        }
    }

    static T Get<T>(GameObject go) where T : Component => go != null ? go.GetComponent<T>() : null;

    void OnAttackStarted(AttackType type) => Play(type == AttackType.Punch ? punch : kick, attackVolume, true);
    void OnHitLanded(FighterCombat.HitInfo info) => Play(hit, hitVolume, true);
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
