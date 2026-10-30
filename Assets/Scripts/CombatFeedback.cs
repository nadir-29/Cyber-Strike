using UnityEngine;

// Hit juice: a neon spark burst, a tiny screen shake and a short hit-pause whenever an attack lands,
// and a lighter cyan deflect burst when a guard stops one.
// Listens to FighterCombat.HitLanded and Blocked; no gameplay rules here.
public class CombatFeedback : MonoBehaviour
{
    [SerializeField] FighterCombat[] fighters;
    [SerializeField] CameraShake cameraShake;

    [Header("Hit-Pause (real seconds)")]
    [SerializeField] float hitPause = 0.045f;
    [SerializeField] float knockoutHitPause = 0.12f;

    [Header("Screen Shake")]
    [SerializeField] float punchShake = 0.05f;
    [SerializeField] float kickShake = 0.09f;
    [SerializeField] float knockoutShake = 0.22f;
    [SerializeField] float shakeTime = 0.12f;
    [SerializeField] float knockoutShakeTime = 0.35f;

    [Header("Sparks")]
    [SerializeField] Color punchSpark = new Color(0.3f, 0.95f, 1f);
    [SerializeField] Color kickSpark = new Color(1f, 0.35f, 0.8f);
    [SerializeField] Color doubleDamageSpark = new Color(1f, 0.25f, 0.75f);
    [SerializeField] Color coreColor = new Color(1f, 1f, 1f);
    [SerializeField] int punchSparks = 9;
    [SerializeField] int kickSparks = 13;

    [Header("Blocked Hits")]
    [SerializeField] Color deflectSpark = new Color(0.25f, 0.95f, 1f);
    [SerializeField] int deflectSparks = 10;
    [SerializeField] float blockShake = 0.03f;
    [SerializeField] float blockPause = 0.03f;

    float pauseEnd;
    bool paused;

    void OnEnable()
    {
        foreach (FighterCombat f in fighters)
            if (f != null) { f.HitLanded += OnHitLanded; f.Blocked += OnBlocked; }
    }

    void OnDisable()
    {
        foreach (FighterCombat f in fighters)
            if (f != null) { f.HitLanded -= OnHitLanded; f.Blocked -= OnBlocked; }
        if (paused) EndPause();
    }

    void OnHitLanded(FighterCombat.HitInfo hit)
    {
        bool knockout = hit.target.IsDead;
        bool kick = hit.type == AttackType.Kick;

        Sparks(hit, kick);

        if (cameraShake != null)
        {
            if (knockout) cameraShake.Shake(knockoutShake, knockoutShakeTime);
            else cameraShake.Shake(kick ? kickShake : punchShake, shakeTime);
        }
        Pause(knockout ? knockoutHitPause : hitPause);
    }

    void OnBlocked(FighterCombat.HitInfo hit)
    {
        if (hit.target.IsDead) return; // a chip KO gets the full knockout feedback via HitLanded

        PixelParticles particles = PixelParticles.Instance;
        if (particles != null)
        {
            // Sparks glance off the guard back toward the attacker.
            for (int i = 0; i < deflectSparks; i++)
            {
                float angle = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(-Mathf.Cos(angle) * hit.direction, Mathf.Sin(angle));
                Color c = Color.Lerp(deflectSpark, coreColor, Random.Range(0f, 0.6f));
                particles.Emit(hit.point, dir * Random.Range(3f, 7f), c, Random.Range(0.1f, 0.22f), 1f, 8f, 5f, true);
            }
        }
        if (cameraShake != null) cameraShake.Shake(blockShake, shakeTime);
        Pause(blockPause);
    }

    void Sparks(FighterCombat.HitInfo hit, bool kick)
    {
        PixelParticles particles = PixelParticles.Instance;
        if (particles == null) return;

        Color color = hit.empowered ? doubleDamageSpark : (kick ? kickSpark : punchSpark);
        int count = (kick ? kickSparks : punchSparks) + (hit.empowered ? 6 : 0);
        float speedScale = hit.empowered ? 1.3f : 1f;

        // Bright core flash.
        for (int i = 0; i < 4; i++)
            particles.Emit(hit.point + Random.insideUnitCircle * 0.08f, Vector2.zero, coreColor, 0.07f, 3f);

        // Streaks fanning out away from the attacker.
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-70f, 70f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle) * hit.direction, Mathf.Sin(angle));
            if (Random.value < 0.25f) dir.x = -dir.x; // a few fly back toward the attacker
            float speed = Random.Range(3f, 9f) * speedScale;
            Color c = Color.Lerp(color, coreColor, Random.Range(0f, 0.5f));
            particles.Emit(hit.point, dir * speed, c, Random.Range(0.12f, 0.28f), 1f, 12f, 4f, true);
        }
    }

    void Pause(float seconds)
    {
        pauseEnd = Mathf.Max(pauseEnd, Time.unscaledTime + seconds);
        if (paused) return;
        paused = true;
        TimeControl.SetHitPaused(true);
    }

    void Update()
    {
        if (paused && Time.unscaledTime >= pauseEnd) EndPause();
    }

    void EndPause()
    {
        paused = false;
        TimeControl.SetHitPaused(false);
    }
}
