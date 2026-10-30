using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu feedback: a soft blip on hover and a confirm on click / Enter. Sounds go through
// AudioManager.PlayUI, which survives scene loads, so a click that loads a new scene is still heard.
// Hover plays once per enter, not again while the pointer stays on the same button.
// Purely cosmetic: missing clips are skipped and the button works the same.
// Screens that drive highlighting themselves (e.g. FighterSelect with the arrow keys) call SetHovered / PlayClick.
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
{
    // One press can reach PlayClick twice (pointer handler + the screen's own click handler); count it once.
    const float RepeatGuard = 0.1f;

    [SerializeField] AudioClip hoverClip;
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] float clickVolume = 0.3f;

    Selectable selectable;
    bool hovered;
    float lastClick = float.NegativeInfinity;

    // Buttons that are disabled (or in a non-interactable CanvasGroup) stay silent.
    bool Interactable => selectable == null || selectable.IsInteractable();

    void Awake() => selectable = GetComponent<Selectable>();

    void OnDisable() => hovered = false;

    public void SetHovered(bool on, bool playSound = true)
    {
        if (on == hovered) return;
        hovered = on;
        if (on && playSound && Interactable) AudioManager.PlayUI(hoverClip, hoverVolume);
    }

    public void PlayClick()
    {
        if (!Interactable || Time.unscaledTime - lastClick < RepeatGuard) return;
        lastClick = Time.unscaledTime;
        AudioManager.PlayUI(clickClip, clickVolume);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) PlayClick();
    }

    public void OnSubmit(BaseEventData eventData) => PlayClick();
}
