using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class GameBackgroundConfig : MonoBehaviour
{
    [LunaPlaygroundAsset("GameBG Background Image", 1, "Game Background")]
    [LunaPlaygroundField("GameBG Background Image", 1, "Game Background")]
    [Tooltip("中台替换图片。运行时赋给本物体的 Image；留空或使用 1×1 占位图时，保留场景 Image 原图。")]
    public Texture2D BackgroundTexture;

    [LunaPlaygroundField("GameBG Background Tint", 2, "Game Background")]
    public Color BackgroundTint = Color.white;

    [LunaPlaygroundField("Show GameBG Background", 3, "Game Background")]
    public bool ShowBackground = true;
    [LunaPlaygroundField("Background Opacity", 4, "Game Background")]
    [Range(0f, 1f)] public float BackgroundOpacity = 1f;
    float appliedOpacity;

    [Tooltip("背景的专用 Canvas，排序固定在棋盘与 HUD 后方；GameBG 自身的 RectTransform 定义棋盘可用区域。")]
    public Canvas RenderCanvas;
    [Tooltip("Optional authored wide-screen background, on the same background canvas. Custom middle-platform images take priority.")]
    public Image wideBackground;
    [Min(0f), Tooltip("Canvas width in design units above which the optional wide background is shown.")]
    public float wideCanvasWidth = 720f;
    bool appliedWide;

    private Image background;
    private Sprite originalSprite;
    private Sprite generatedSprite;
    private Texture2D appliedTexture;
    private Color appliedTint;
    private bool appliedVisibility;
    private bool initialized;
    private Color authoredTint, authoredWideTint;

    void Awake()
    {
        ApplySettings();
    }

    void Update()
    {
        if (appliedTexture != BackgroundTexture || appliedTint != BackgroundTint || appliedVisibility != ShowBackground || appliedOpacity != BackgroundOpacity || appliedWide != UseWideBackground())
            ApplySettings();
    }

    bool UseWideBackground()
    {
        if (wideBackground == null || RenderCanvas == null || originalSprite == null) return false;
        var rect = RenderCanvas.transform as RectTransform;
        return rect != null && rect.rect.width > wideCanvasWidth + 1f && ReplacementTexture() == null;
    }

    Texture2D ReplacementTexture() => BackgroundTexture != null && (BackgroundTexture.width > 1 || BackgroundTexture.height > 1) ? BackgroundTexture : null;

    public void ApplySettings()
    {
        var replacement = ReplacementTexture();
        if (!initialized)
        {
            background = GetComponent<Image>();
            originalSprite = background.sprite;
            authoredTint = background.color;
            authoredWideTint = wideBackground != null ? wideBackground.color : Color.white;
            initialized = true;
        }

        if (RenderCanvas != null)
        {
            RenderCanvas.overrideSorting = true;
            RenderCanvas.sortingLayerID = 0;
            RenderCanvas.sortingOrder = -100;
        }
        background.raycastTarget = false;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            // Keep real asset references in saved scenes. Runtime Sprite.Create objects are export-only state.
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(replacement);
            var assetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            background.sprite = assetSprite != null ? assetSprite : originalSprite;
#endif
        }
        else if (appliedTexture != BackgroundTexture || background.sprite == null)
        {
            if (generatedSprite != null) { if (Application.isPlaying) Destroy(generatedSprite); else DestroyImmediate(generatedSprite); }
            generatedSprite = replacement == null ? null : Sprite.Create(
                replacement,
                new Rect(0, 0, replacement.width, replacement.height),
                new Vector2(0.5f, 0.5f), 100f);
            background.sprite = generatedSprite != null ? generatedSprite : originalSprite;
        }

        Color tint = BackgroundTint; tint.a *= BackgroundOpacity;
        background.color = authoredTint * tint;
        appliedOpacity = BackgroundOpacity;
        appliedWide = UseWideBackground();
        background.enabled = ShowBackground && !appliedWide;
        if (wideBackground != null)
        {
            wideBackground.enabled = ShowBackground && appliedWide;
            wideBackground.color = authoredWideTint * tint;
            wideBackground.raycastTarget = false;
        }
        appliedTexture = BackgroundTexture;
        appliedTint = BackgroundTint;
        appliedVisibility = ShowBackground;
    }

    void OnDestroy()
    {
        if (generatedSprite != null) Destroy(generatedSprite);
    }
}
