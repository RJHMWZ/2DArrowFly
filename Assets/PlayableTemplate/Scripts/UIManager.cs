using UnityEngine;
using UnityEngine.UI;

// Owns startup loading and its brand assets. HueGame owns the game flow.
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public bool BlocksGameplay => loadingActive;

    [Header("Game Settings")]
    [Range(1, 5)] public int startingLevel = 1;
    public bool showTutorial = true;

    [System.Serializable]
    public sealed class LogoBinding
    {
        public Image image;
        [Tooltip("Optional text shown when no logo is assigned.")]
        public Graphic fallback;
    }

    [Header("Brand Assets")]
    public Image[] brandIconTargets = new Image[0];
    public LogoBinding[] brandLogoTargets = new LogoBinding[0];

    [ContextMenu("Apply Brand Assets")]
    public void ApplyBranding()
    {
        Sprite icon = ResolveBrandTexture(BrandIconTexture, ref appliedIconTexture, ref generatedIcon);
        Sprite logo = ResolveBrandTexture(BrandLogoTexture, ref appliedLogoTexture, ref generatedLogo);
        foreach (var image in brandIconTargets)
            if (image != null) { image.sprite = icon; image.preserveAspect = true; image.enabled = icon != null; }
        foreach (var binding in brandLogoTargets)
        {
            if (binding == null || binding.image == null) continue;
            binding.image.sprite = logo;
            binding.image.preserveAspect = true;
            binding.image.enabled = logo != null;
            if (binding.fallback != null) binding.fallback.enabled = logo == null;
        }
        if (!string.IsNullOrEmpty(BrandName))
            foreach (var label in brandNameTargets) if (label != null) label.text = BrandName;
        appliedBrandName = BrandName;
    }

    [Header("Loading Brand")]
    public Texture2D BrandIconTexture;
    public Texture2D BrandLogoTexture;
    public string BrandName = "";
    public Text[] brandNameTargets = new Text[0];
    public GameObject loadingPanel;
    [Min(0f)] public float loadingDuration = 1f;
    Texture2D appliedIconTexture, appliedLogoTexture;
    Sprite generatedIcon, generatedLogo;
    string appliedBrandName;

    public Image loadingProgressFill;
    public CanvasGroup loadingGroup;
    [Min(0f)] public float loadingFadeDuration;
    [Header("Loading animation")]
    public Text loadingStatusText;
    public string loadingLabel = "Loading";
    [Min(.05f)] public float loadingDotInterval = .2f;
    private float loadingStarted;
    private bool loadingActive;
    private bool loadingClockStarted;
    public bool IsLoading => loadingActive;

    Sprite ResolveBrandTexture(Texture2D texture, ref Texture2D applied, ref Sprite generated)
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            return texture != null ? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GetAssetPath(texture)) : null;
#else
            return null;
#endif
        }
        if (applied != texture)
        {
            if (generated != null) { if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated); }
            generated = texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            applied = texture;
        }
        return generated;
    }


    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        loadingActive = loadingPanel != null && loadingDuration > 0;
        if (loadingPanel != null) loadingPanel.SetActive(loadingActive);
        loadingStarted = Time.unscaledTime;
        ApplyBranding();
    }

    void Update()
    {
        if (appliedIconTexture != BrandIconTexture || appliedLogoTexture != BrandLogoTexture || appliedBrandName != BrandName)
            ApplyBranding();
        if (loadingActive)
        {
            if (!loadingClockStarted) { loadingStarted = Time.unscaledTime; loadingClockStarted = true; }
            float age = Time.unscaledTime - loadingStarted;
            if (loadingProgressFill != null) loadingProgressFill.fillAmount = Mathf.Lerp(.06f, 1, Mathf.Clamp01(age / Mathf.Max(.01f, loadingDuration)));
            if (loadingStatusText != null)
            {
                int tick = Mathf.FloorToInt(age / Mathf.Max(.05f, loadingDotInterval));
                loadingStatusText.text = loadingLabel + new string('.', tick == 0 ? 3 : tick % 4);
            }
            if (loadingGroup != null) loadingGroup.alpha = 1 - Mathf.Clamp01((age - loadingDuration) / Mathf.Max(.001f, loadingFadeDuration));
            if (age >= loadingDuration + loadingFadeDuration)
            {
                loadingActive = false;
                loadingPanel.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (generatedIcon != null) Destroy(generatedIcon);
        if (generatedLogo != null) Destroy(generatedLogo);
        if (Instance == this) Instance = null;
    }
}
