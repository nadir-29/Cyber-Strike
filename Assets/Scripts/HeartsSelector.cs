using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// "HEARTS" selector on the fighter select screen: < and > arrow buttons (or Up/Down, W/S) pick how many
// hearts (lives) each fighter gets, 1-5. Writes MatchSettings.Hearts, which the Arena reads and Restart keeps.
// Shows the count as a row of pixel hearts (lit up to the pick) plus the number. An arrow dims at its limit.
public class HeartsSelector : MonoBehaviour
{
    [SerializeField] Button decreaseButton;
    [SerializeField] Button increaseButton;
    [SerializeField] PixelText valueText;
    [Tooltip("Parent the heart icons are built under, left to right.")]
    [SerializeField] RectTransform heartRow;
    [SerializeField] Sprite fullHeart;
    [SerializeField] Sprite emptyHeart;
    [SerializeField] Vector2 heartSize = new Vector2(44f, 40f);
    [SerializeField] float spacing = 6f;

    [Header("Feel")]
    [SerializeField] float popTime = 0.18f;
    [SerializeField] float popScale = 1.5f;
    [SerializeField] Color arrowColor = new Color(0.85f, 1f, 1f);
    [SerializeField] Color arrowDisabledColor = new Color(0.3f, 0.3f, 0.4f, 0.6f);

    InputAction upAction;
    InputAction downAction;
    Image[] hearts;
    UIButtonSound decreaseSound;
    UIButtonSound increaseSound;
    int popIndex = -1;
    float popTimer;

    // Off while the screen is leaving (a card was picked).
    public bool Locked { get; set; }

    void Awake()
    {
        upAction = new InputAction("HeartsUp", InputActionType.Button, "<Keyboard>/upArrow");
        upAction.AddBinding("<Keyboard>/w");
        downAction = new InputAction("HeartsDown", InputActionType.Button, "<Keyboard>/downArrow");
        downAction.AddBinding("<Keyboard>/s");

        if (decreaseButton != null)
        {
            decreaseButton.onClick.AddListener(() => Change(-1));
            decreaseSound = decreaseButton.GetComponent<UIButtonSound>();
        }
        if (increaseButton != null)
        {
            increaseButton.onClick.AddListener(() => Change(1));
            increaseSound = increaseButton.GetComponent<UIButtonSound>();
        }

        hearts = new Image[MatchSettings.MaxHearts];
        for (int i = 0; i < hearts.Length; i++)
        {
            var go = new GameObject($"Heart{i + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(heartRow, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = heartSize;
            rect.anchoredPosition = new Vector2(i * (heartSize.x + spacing), 0f);
            hearts[i] = go.GetComponent<Image>();
            hearts[i].raycastTarget = false;
            hearts[i].preserveAspect = true;
        }
    }

    void OnEnable()
    {
        upAction.Enable();
        downAction.Enable();
    }

    void OnDisable()
    {
        upAction.Disable();
        downAction.Disable();
    }

    void OnDestroy()
    {
        upAction.Dispose();
        downAction.Dispose();
    }

    void Start() => Show();

    void Update()
    {
        if (!Locked)
        {
            if (upAction.WasPressedThisFrame()) Change(1);
            else if (downAction.WasPressedThisFrame()) Change(-1);
        }

        if (popIndex < 0) return;
        popTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(popTimer / popTime);
        hearts[popIndex].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, k * k);
        if (k <= 0f) popIndex = -1;
    }

    void Change(int delta)
    {
        if (Locked) return;
        int next = Mathf.Clamp(MatchSettings.Hearts + delta, MatchSettings.MinHearts, MatchSettings.MaxHearts);
        if (next == MatchSettings.Hearts) return;

        // Click before Show() may disable the arrow at its limit (which would silence it). A mouse click
        // also reaches UIButtonSound directly; its repeat guard counts the press once.
        UIButtonSound sound = delta < 0 ? decreaseSound : increaseSound;
        if (sound != null) sound.PlayClick();

        MatchSettings.Hearts = next;
        // Pop the heart that just lit up (or the new last one when going down).
        if (popIndex >= 0) hearts[popIndex].rectTransform.localScale = Vector3.one;
        popIndex = next - 1;
        popTimer = popTime;
        Show();
    }

    void Show()
    {
        int count = MatchSettings.Hearts;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].sprite = i < count ? fullHeart : emptyHeart;

        if (valueText != null)
        {
            valueText.Text = count.ToString();
            valueText.Rebuild();
        }
        SetArrow(decreaseButton, count > MatchSettings.MinHearts);
        SetArrow(increaseButton, count < MatchSettings.MaxHearts);
    }

    void SetArrow(Button button, bool on)
    {
        if (button == null) return;
        button.interactable = on;
        PixelText text = button.GetComponent<PixelText>();
        if (text == null) return;
        text.Color = on ? arrowColor : arrowDisabledColor;
        text.Rebuild();
    }
}
