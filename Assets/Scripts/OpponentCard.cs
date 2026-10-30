using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One neon card on the fighter select screen: portrait, name, difficulty stars and bio, filled
// from an OpponentData. Grows and lights up when highlighted; reports hover and click to FighterSelect.
// Runs in the editor too, so the cards show their content while laying out the scene.
[ExecuteAlways]
public class OpponentCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    static readonly string[] DifficultyNames = { "EASY", "MEDIUM", "HARD" };

    [SerializeField] OpponentData opponent;

    [Header("Parts")]
    [SerializeField] Image frame;
    [SerializeField] Image portrait;
    [SerializeField] PixelText nameText;
    [SerializeField] Image[] stars;
    [SerializeField] PixelText difficultyText;
    [SerializeField] PixelText bioText;
    [Tooltip("Characters per bio line; the bio is word-wrapped to fit the card.")]
    [SerializeField] int bioLineLength = 22;

    [Header("Highlight")]
    [SerializeField] float highlightScale = 1.06f;
    [SerializeField] float scaleSpeed = 12f;
    [SerializeField, Range(0f, 1f)] float dimmedFrame = 0.35f;
    [SerializeField] Color dimmedPortrait = new Color(0.45f, 0.45f, 0.55f);
    [SerializeField] Color emptyStar = new Color(0.25f, 0.22f, 0.32f, 0.8f);
    [SerializeField] float pickFlashTime = 0.35f;

    bool highlighted;
    float pickTimer;

    public OpponentData Opponent => opponent;

    public event Action<OpponentCard> Hovered;
    public event Action<OpponentCard> Clicked;

    void OnEnable() => Refresh();
    void OnValidate() => Refresh();

    // Fills the card from its OpponentData.
    public void Refresh()
    {
        if (opponent == null) return;

        if (portrait != null) portrait.sprite = opponent.portrait;
        if (nameText != null)
        {
            nameText.Text = opponent.displayName;
            nameText.GlowColor = WithAlpha(opponent.accent, 0.9f);
        }
        if (difficultyText != null)
        {
            difficultyText.Text = DifficultyNames[Mathf.Clamp(opponent.difficulty, 1, 3) - 1];
            difficultyText.Color = opponent.accent;
            difficultyText.GlowColor = WithAlpha(opponent.accent, 0.5f);
        }
        if (bioText != null) bioText.Text = Wrap(opponent.bio, bioLineLength);
        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].color = i < opponent.difficulty ? opponent.accent : emptyStar;
        ApplyHighlight();
    }

    public void SetHighlighted(bool on)
    {
        highlighted = on;
        ApplyHighlight();
    }

    // Flashes the card white as it is picked.
    public void PlayPicked() => pickTimer = pickFlashTime;

    void Update()
    {
        if (!Application.isPlaying) return;

        float target = highlighted ? highlightScale : 1f;
        float k = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(transform.localScale.x, target, k);

        if (pickTimer > 0f)
        {
            pickTimer -= Time.unscaledDeltaTime;
            bool white = Mathf.Repeat(pickTimer * 20f, 1f) < 0.5f;
            if (frame != null) frame.color = white && pickTimer > 0f ? Color.white : opponent.accent;
        }
    }

    void ApplyHighlight()
    {
        if (opponent == null) return;
        Color accent = opponent.accent;
        if (frame != null) frame.color = highlighted ? accent : Color.Lerp(Color.black, accent, dimmedFrame);
        if (portrait != null) portrait.color = highlighted ? Color.white : dimmedPortrait;
        if (nameText != null) nameText.Color = highlighted ? Color.white : new Color(0.6f, 0.6f, 0.7f);
    }

    public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);
    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static string Wrap(string text, int lineLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        StringBuilder result = new StringBuilder();
        int lineStart = 0;
        foreach (string word in text.Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int lineLen = result.Length - lineStart;
            if (lineLen > 0 && lineLen + 1 + word.Length > lineLength)
            {
                result.Append('\n');
                lineStart = result.Length;
            }
            else if (lineLen > 0)
            {
                result.Append(' ');
            }
            result.Append(word);
        }
        return result.ToString();
    }
}
