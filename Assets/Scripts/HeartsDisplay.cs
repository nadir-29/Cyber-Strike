using UnityEngine;
using UnityEngine.UI;

// HUD row of pixel hearts for one fighter. Display only: reads Lives, never changes it.
// Builds one Image per heart under this object (left to right, or right to left for the CPU side).
// A lost heart blinks, pops and goes dark; the last heart left beats.
public class HeartsDisplay : MonoBehaviour
{
    [SerializeField] Lives target;
    [SerializeField] Sprite fullHeart;
    [SerializeField] Sprite emptyHeart;
    [SerializeField] Vector2 heartSize = new Vector2(44f, 40f);
    [SerializeField] float spacing = 6f;
    [Tooltip("Lay the hearts out from the right edge leftwards (the CPU's side of the HUD).")]
    [SerializeField] bool rightToLeft;

    [Header("Feel")]
    [SerializeField] float lostTime = 0.45f;
    [SerializeField] float lostPopScale = 1.6f;
    [SerializeField] float beatSpeed = 7f;
    [SerializeField] float beatScale = 0.12f;

    Image[] hearts = new Image[0];
    int shown;
    int lostIndex = -1;
    float lostTimer;

    void OnEnable()
    {
        if (target != null) target.Changed += OnChanged;
    }

    void OnDisable()
    {
        if (target != null) target.Changed -= OnChanged;
    }

    void Start()
    {
        if (target != null) Build(target.Current, target.Max);
    }

    void OnChanged(int current, int max)
    {
        if (max != hearts.Length)
        {
            Build(current, max);
            return;
        }
        if (current < shown)
        {
            lostIndex = current; // the rightmost lit heart (leftmost on the CPU side) goes out
            lostTimer = lostTime;
        }
        shown = current;
        Refresh();
    }

    void Build(int current, int max)
    {
        foreach (Image heart in hearts)
            if (heart != null) Destroy(heart.gameObject);

        hearts = new Image[max];
        Vector2 anchor = rightToLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        for (int i = 0; i < max; i++)
        {
            var go = new GameObject($"Heart{i + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = heartSize;
            float x = (i * (heartSize.x + spacing)) * (rightToLeft ? -1f : 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            hearts[i] = image;
        }
        shown = current;
        lostIndex = -1;
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].sprite = i < shown ? fullHeart : emptyHeart;
            hearts[i].color = Color.white;
            hearts[i].rectTransform.localScale = Vector3.one;
        }
    }

    void Update()
    {
        // The heart that just went out: blinks and pops, then it settles dark.
        if (lostIndex >= 0 && lostIndex < hearts.Length)
        {
            lostTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(lostTimer / lostTime);
            Image heart = hearts[lostIndex];
            bool flash = k > 0f && Mathf.Repeat(lostTimer * 16f, 1f) < 0.5f;
            heart.sprite = flash ? fullHeart : emptyHeart;
            heart.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, lostPopScale, k * k);
            if (k <= 0f) lostIndex = -1;
        }

        // Last heart beats.
        if (shown == 1 && hearts.Length > 1 && hearts[0] != null)
        {
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * beatSpeed)), 8f);
            hearts[0].rectTransform.localScale = Vector3.one * (1f + beatScale * beat);
        }
    }
}
