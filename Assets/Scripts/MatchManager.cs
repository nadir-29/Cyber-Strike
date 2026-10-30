using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Match flow: a "3, 2, 1, FIGHT!" countdown with both fighters held, then the fight, then the
// winner's victory pose and the win/loss screen once either fighter is knocked out. Holds fighters by switching their input
// controllers off; gameplay rules stay in Fighter and friends.
public class MatchManager : MonoBehaviour
{
    [Header("Fighters")]
    [SerializeField] Health player;
    [SerializeField] Health opponent;
    [Tooltip("Input controllers held off during the countdown and after the knockout.")]
    [SerializeField] Behaviour[] controllers;
    [SerializeField] PickupSpawner pickupSpawner;

    [Header("Countdown")]
    [SerializeField] PixelText countdownText;
    [Tooltip("Real seconds each of 3, 2, 1 stays up.")]
    [SerializeField] float countdownStep = 0.7f;
    [SerializeField] float fightTextTime = 0.7f;
    [SerializeField] float popScale = 1.6f;
    [SerializeField] Color numberColor = new Color(0.85f, 1f, 1f);
    [SerializeField] Color numberGlow = new Color(0f, 0.9f, 1f, 0.9f);
    [SerializeField] Color fightColor = new Color(1f, 0.85f, 0.95f);
    [SerializeField] Color fightGlow = new Color(1f, 0.15f, 0.7f, 0.95f);

    [Header("Result")]
    [SerializeField] CanvasGroup resultPanel;
    [SerializeField] PixelText resultText;
    [SerializeField] Button restartButton;
    [SerializeField] Button menuButton;
    [Tooltip("Real seconds after the knockout before the winner strikes the victory pose, so the KO plays out.")]
    [SerializeField] float victoryDelay = 1f;
    [Tooltip("Longest real seconds to wait for an airborne winner to land before posing anyway.")]
    [SerializeField] float landingTimeout = 1f;
    [Tooltip("Real seconds the victory pose plays before the result screen shows.")]
    [SerializeField] float victoryPoseTime = 2.4f;
    [SerializeField] float resultFadeTime = 0.35f;
    [SerializeField] Color winColor = new Color(0.85f, 1f, 1f);
    [SerializeField] Color winGlow = new Color(0f, 0.9f, 1f, 0.9f);
    [SerializeField] Color loseColor = new Color(1f, 0.8f, 0.8f);
    [SerializeField] Color loseGlow = new Color(1f, 0.1f, 0.2f, 0.95f);
    [SerializeField] string menuScene = "MainMenu";

    bool matchOver;

    void Awake()
    {
        TimeControl.ResetAll();
        SetFightersActive(false);
        if (pickupSpawner != null) pickupSpawner.enabled = false;

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (resultPanel != null)
        {
            resultPanel.alpha = 0f;
            resultPanel.interactable = false;
            resultPanel.blocksRaycasts = false;
            resultPanel.gameObject.SetActive(false);
        }
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
    }

    void OnEnable()
    {
        if (player != null) player.Died += OnKnockout;
        if (opponent != null) opponent.Died += OnKnockout;
    }

    void OnDisable()
    {
        if (player != null) player.Died -= OnKnockout;
        if (opponent != null) opponent.Died -= OnKnockout;
    }

    void Start()
    {
        StartCoroutine(Countdown());
    }

    IEnumerator Countdown()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            for (int n = 3; n >= 1; n--)
                yield return ShowCountdown(n.ToString(), numberColor, numberGlow, countdownStep, false);
        }

        if (matchOver) yield break;
        SetFightersActive(true);
        if (pickupSpawner != null) pickupSpawner.enabled = true;

        if (countdownText != null)
        {
            yield return ShowCountdown("FIGHT!", fightColor, fightGlow, fightTextTime, true);
            countdownText.gameObject.SetActive(false);
        }
    }

    // Pops the text in big and settles it; optionally fades it out at the end.
    IEnumerator ShowCountdown(string text, Color color, Color glow, float duration, bool fadeOut)
    {
        countdownText.Text = text;
        countdownText.Color = color;
        countdownText.GlowColor = glow;
        countdownText.Rebuild();

        Transform t = countdownText.transform;
        RawImage image = countdownText.GetComponent<RawImage>();
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            float k = time / duration;
            float settle = Mathf.Clamp01(k / 0.25f);
            t.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, 1f - (1f - settle) * (1f - settle));
            Color c = image.color;
            c.a = fadeOut ? Mathf.Clamp01((1f - k) / 0.4f) : Mathf.Clamp01(k / 0.1f);
            image.color = c;
            yield return null;
        }
    }

    void OnKnockout()
    {
        if (matchOver) return;
        matchOver = true;

        // Freeze the fight: nobody acts and no new pickups appear; the KO launch and topple play out.
        SetFightersActive(false);
        if (pickupSpawner != null) pickupSpawner.enabled = false;
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        StartCoroutine(ShowResult(opponent != null && opponent.IsDead && !player.IsDead));
    }

    IEnumerator ShowResult(bool playerWon)
    {
        yield return new WaitForSecondsRealtime(victoryDelay);

        // Double KO: nobody poses.
        Health winner = !player.IsDead ? player : opponent != null && !opponent.IsDead ? opponent : null;
        if (winner != null) yield return VictoryPose(winner, winner == player ? opponent : player);
        if (resultPanel == null) yield break;

        if (resultText != null)
        {
            resultText.Text = playerWon ? "YOU WIN" : "YOU LOSE";
            resultText.Color = playerWon ? winColor : loseColor;
            resultText.GlowColor = playerWon ? winGlow : loseGlow;
            resultText.Rebuild();
        }

        resultPanel.gameObject.SetActive(true);
        for (float time = 0f; time < resultFadeTime; time += Time.unscaledDeltaTime)
        {
            resultPanel.alpha = time / resultFadeTime;
            yield return null;
        }
        resultPanel.alpha = 1f;
        resultPanel.interactable = true;
        resultPanel.blocksRaycasts = true;
    }

    // The winner lands, turns to the loser and holds its victory pose for a moment.
    IEnumerator VictoryPose(Health winner, Health loser)
    {
        Fighter fighter = winner.GetComponent<Fighter>();
        if (fighter != null)
        {
            for (float time = 0f; time < landingTimeout && !fighter.IsGrounded; time += Time.unscaledDeltaTime)
                yield return null;
            if (loser != null)
                fighter.Face(loser.transform.position.x >= winner.transform.position.x ? 1 : -1);
        }

        FighterAnimator animator = winner.GetComponent<FighterAnimator>();
        if (animator != null) animator.PlayVictory();
        yield return new WaitForSecondsRealtime(victoryPoseTime);
    }

    void SetFightersActive(bool active)
    {
        foreach (Behaviour controller in controllers)
            if (controller != null) controller.enabled = active;
    }

    // Reloading the scene rebuilds every fighter, timer, ability and pickup from scratch.
    public void Restart()
    {
        TimeControl.ResetAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMenu()
    {
        TimeControl.ResetAll();
        SceneManager.LoadScene(menuScene);
    }
}
