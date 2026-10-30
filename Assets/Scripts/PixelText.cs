using UnityEngine;
using UnityEngine.UI;

// Neon pixel text for the UI: renders the text with PixelFont into a small point-filtered texture,
// with a pixelated glow halo and an optional neon-tube frame, and shows it on a RawImage.
// Runs in the editor too, so the menus can be laid out without entering Play mode.
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class PixelText : MonoBehaviour
{
    [SerializeField, TextArea(1, 10)] string text = "TEXT";
    [Tooltip("UI units per font pixel.")]
    [SerializeField] float pixelScale = 6f;
    [SerializeField] int letterSpacing = 1;
    [SerializeField] int lineSpacing = 3;
    [Tooltip("Resizes the RectTransform to fit the text.")]
    [SerializeField] bool autoSize = true;

    [Header("Colors")]
    [SerializeField] Color color = new Color(0.85f, 1f, 1f);
    [SerializeField] Color glowColor = new Color(0f, 0.9f, 1f, 0.9f);
    [SerializeField, Range(0, 6)] int glowRadius = 2;

    [Header("Frame (neon sign)")]
    [SerializeField] bool frame;
    [SerializeField] Color frameColor = new Color(1f, 0.8f, 0.95f);
    [SerializeField] Color frameGlowColor = new Color(1f, 0.15f, 0.7f, 0.9f);
    [Tooltip("Font pixels between the text and the frame.")]
    [SerializeField] Vector2Int frameGap = new Vector2Int(4, 3);
    [Tooltip("Fill inside the frame.")]
    [SerializeField] Color background = new Color(0.03f, 0.01f, 0.08f, 0.85f);

    [Header("Flicker")]
    [SerializeField] bool flicker;
    [Tooltip("Average seconds between flickers.")]
    [SerializeField] float flickerInterval = 2.5f;

    RawImage image;
    Texture2D texture;
    bool dirty = true;
    float nextFlicker;
    float flickerEnd;

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value;
            dirty = true;
        }
    }

    public Color Color
    {
        get => color;
        set { color = value; dirty = true; }
    }

    public Color GlowColor
    {
        get => glowColor;
        set { glowColor = value; dirty = true; }
    }

    void OnEnable()
    {
        image = GetComponent<RawImage>();
        dirty = true;
        Rebuild();
    }

    void OnValidate() => dirty = true;

    void OnDestroy()
    {
        if (texture != null) DestroyImmediate(texture);
    }

    void Update()
    {
        if (dirty) Rebuild();
        if (flicker && Application.isPlaying) Flicker();
    }

    // Brief neon stutter: a couple of quick dips in brightness every few seconds.
    void Flicker()
    {
        float now = Time.unscaledTime;
        if (now >= nextFlicker)
        {
            flickerEnd = now + Random.Range(0.08f, 0.25f);
            nextFlicker = now + Random.Range(0.4f, 1.6f) * flickerInterval;
        }
        bool off = now < flickerEnd && Mathf.Repeat(now * 30f, 1f) < 0.5f;
        Color c = image.color;
        c.a = off ? 0.35f : 1f;
        image.color = c;
    }

    public void Rebuild()
    {
        dirty = false;
        if (image == null) image = GetComponent<RawImage>();

        string[] lines = (text ?? "").Replace("\r", "").Split('\n');
        int advance = PixelFont.Width + letterSpacing;
        int contentW = 0;
        foreach (string line in lines)
            contentW = Mathf.Max(contentW, line.Length * advance - letterSpacing);
        int contentH = lines.Length * (PixelFont.Height + lineSpacing) - lineSpacing;

        int frameInset = frame ? 1 : 0;
        Vector2Int gap = frame ? frameGap : Vector2Int.zero;
        int pad = glowRadius;
        int w = Mathf.Max(1, contentW + 2 * (pad + frameInset + gap.x));
        int h = Mathf.Max(1, contentH + 2 * (pad + frameInset + gap.y));

        // Lit masks, y = 0 at the top.
        bool[] textMask = new bool[w * h];
        bool[] frameMask = new bool[w * h];

        int top = pad + frameInset + gap.y;
        for (int l = 0; l < lines.Length; l++)
        {
            string line = lines[l];
            int lineW = line.Length * advance - letterSpacing;
            int left = pad + frameInset + gap.x + (contentW - lineW) / 2;
            int y0 = top + l * (PixelFont.Height + lineSpacing);
            for (int i = 0; i < line.Length; i++)
                for (int gy = 0; gy < PixelFont.Height; gy++)
                    for (int gx = 0; gx < PixelFont.Width; gx++)
                        if (PixelFont.IsLit(line[i], gx, gy))
                            textMask[(y0 + gy) * w + left + i * advance + gx] = true;
        }

        int fx0 = pad, fy0 = pad, fx1 = w - 1 - pad, fy1 = h - 1 - pad;
        if (frame)
        {
            for (int x = fx0 + 1; x < fx1; x++) { frameMask[fy0 * w + x] = true; frameMask[fy1 * w + x] = true; }
            for (int y = fy0 + 1; y < fy1; y++) { frameMask[y * w + fx0] = true; frameMask[y * w + fx1] = true; }
        }

        float[] textGlow = Glow(textMask, w, h);
        float[] frameGlow = Glow(frameMask, w, h);

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = frame && x > fx0 && x < fx1 && y > fy0 && y < fy1 ? background : Color.clear;
                c = Over(c, frameGlowColor, frameGlow[i]);
                c = Over(c, glowColor, textGlow[i]);
                if (frameMask[i]) c = frameColor;
                if (textMask[i]) c = color;
                // Texture rows go bottom-up.
                pixels[(h - 1 - y) * w + x] = c;
            }
        }

        if (texture == null || texture.width != w || texture.height != h)
        {
            if (texture != null) DestroyImmediate(texture);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
                name = "PixelText",
            };
        }
        texture.SetPixels(pixels);
        texture.Apply();
        image.texture = texture;

        if (autoSize)
            ((RectTransform)transform).sizeDelta = new Vector2(w, h) * pixelScale;
    }

    // Intensity 0..1 of the halo around lit pixels, falling off with distance.
    float[] Glow(bool[] mask, int w, int h)
    {
        float[] glow = new float[w * h];
        if (glowRadius <= 0) return glow;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[y * w + x]) continue;
                for (int dy = -glowRadius; dy <= glowRadius; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -glowRadius; dx <= glowRadius; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float t = 1f - d / (glowRadius + 1f);
                        if (t <= 0f) continue;
                        int n = ny * w + nx;
                        glow[n] = Mathf.Max(glow[n], t * t);
                    }
                }
            }
        }
        return glow;
    }

    // Alpha-blends `top` (with its alpha scaled by amount) over `bottom`.
    static Color Over(Color bottom, Color top, float amount)
    {
        float a = top.a * amount;
        if (a <= 0f) return bottom;
        float outA = a + bottom.a * (1f - a);
        Color rgb = (top * a + bottom * bottom.a * (1f - a)) / outA;
        rgb.a = outA;
        return rgb;
    }
}
