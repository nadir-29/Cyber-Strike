using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// CPU fighter select screen, which doubles as the difficulty choice. Hover or Left/Right highlights
// a card; click or Enter picks it and starts the match; Back or Escape returns to the title.
public class FighterSelect : MonoBehaviour
{
    [SerializeField] OpponentCard[] cards;
    [SerializeField] Button backButton;
    [SerializeField] string arenaScene = "Arena";
    [SerializeField] string menuScene = "MainMenu";
    [Tooltip("Real seconds the picked card flashes before the match loads.")]
    [SerializeField] float pickDelay = 0.4f;

    InputAction leftAction;
    InputAction rightAction;
    InputAction confirmAction;
    InputAction backAction;
    UIButtonSound[] cardSounds;
    UIButtonSound backSound;
    int index;
    bool picked;

    void Awake()
    {
        TimeControl.ResetAll();

        leftAction = new InputAction("Left", InputActionType.Button, "<Keyboard>/leftArrow");
        leftAction.AddBinding("<Keyboard>/a");
        rightAction = new InputAction("Right", InputActionType.Button, "<Keyboard>/rightArrow");
        rightAction.AddBinding("<Keyboard>/d");
        confirmAction = new InputAction("Confirm", InputActionType.Button, "<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/numpadEnter");
        backAction = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");

        cardSounds = new UIButtonSound[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Hovered += OnCardHovered;
            cards[i].Clicked += OnCardClicked;
            cardSounds[i] = cards[i].GetComponent<UIButtonSound>();
        }
        if (backButton != null)
        {
            backButton.onClick.AddListener(GoBack);
            backSound = backButton.GetComponent<UIButtonSound>();
        }
    }

    void OnEnable()
    {
        leftAction.Enable();
        rightAction.Enable();
        confirmAction.Enable();
        backAction.Enable();
    }

    void OnDisable()
    {
        leftAction.Disable();
        rightAction.Disable();
        confirmAction.Disable();
        backAction.Disable();
    }

    void OnDestroy()
    {
        leftAction.Dispose();
        rightAction.Dispose();
        confirmAction.Dispose();
        backAction.Dispose();
    }

    void Start()
    {
        // Start on the last opponent picked this session, otherwise the first card.
        index = 0;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i].Opponent == OpponentRoster.Selected) index = i;
        Highlight(index, false); // the starting card is highlighted silently
    }

    void Update()
    {
        if (picked) return;

        if (leftAction.WasPressedThisFrame()) Highlight(Mathf.Max(0, index - 1));
        if (rightAction.WasPressedThisFrame()) Highlight(Mathf.Min(cards.Length - 1, index + 1));
        if (confirmAction.WasPressedThisFrame()) Pick(index);
        else if (backAction.WasPressedThisFrame()) GoBack();
    }

    void OnCardHovered(OpponentCard card)
    {
        if (!picked) Highlight(System.Array.IndexOf(cards, card));
    }

    void OnCardClicked(OpponentCard card)
    {
        if (!picked) Pick(System.Array.IndexOf(cards, card));
    }

    // Hover sounds follow the highlight, so arrow keys and the mouse sound the same and a card
    // that stays highlighted doesn't blip again.
    void Highlight(int i, bool playSound = true)
    {
        if (i < 0 || i >= cards.Length) return;
        index = i;
        for (int c = 0; c < cards.Length; c++)
        {
            cards[c].SetHighlighted(c == index);
            if (cardSounds[c] != null) cardSounds[c].SetHovered(c == index, playSound);
        }
    }

    void Pick(int i)
    {
        if (i < 0 || i >= cards.Length) return;
        Highlight(i);
        picked = true;
        if (cardSounds[i] != null) cardSounds[i].PlayClick();
        OpponentRoster.Selected = cards[i].Opponent;
        cards[i].PlayPicked();
        StartCoroutine(LoadArena());
    }

    IEnumerator LoadArena()
    {
        yield return new WaitForSecondsRealtime(pickDelay);
        SceneManager.LoadScene(arenaScene);
    }

    public void GoBack()
    {
        if (picked) return;
        if (backSound != null) backSound.PlayClick(); // Esc; a mouse click already played it (deduped)
        SceneManager.LoadScene(menuScene);
    }
}
