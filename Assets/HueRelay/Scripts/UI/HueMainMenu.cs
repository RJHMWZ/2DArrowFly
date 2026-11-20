using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>场景中保存的主菜单，独立 Canvas 避免改动游戏布局。</summary>
    [ExecuteAlways]
    public sealed class HueMainMenu : MonoBehaviour
    {
        public HueGame game;
        public HueView view;
        public Canvas menuCanvas;
        public CanvasScaler scaler;
        public RectTransform logo;
        public Button levelButton;
        public Text levelLabel;
        public Button settingsButton;
        public RectTransform settingsSafeArea;
        public Text settingsLabel;
        public GameObject settingsOverlay;
        public RectTransform settingsCard;
        public Button soundButton;
        public Text soundState;
        public Button vibrationButton;
        public Text vibrationState;
        public Button closeButton;
        public bool IsVisible => menuCanvas && menuCanvas.enabled;

        // Unity may import the scene before a newly added script has compiled.
        // Recover the authored nodes in that already-open scene after script reload.
        public bool ResolveBindings()
        {
            if (!game) game = FindObjectOfType<HueGame>();
            if (!view) view = FindObjectOfType<HueView>();
            if (!menuCanvas) menuCanvas = GetComponent<Canvas>();
            if (!scaler) scaler = GetComponent<CanvasScaler>();
            if (!logo) logo = transform.Find("TitleLogoPlaceholder") as RectTransform;
            if (!levelButton) levelButton = Find<Button>("LevelButton");
            if (!levelLabel) levelLabel = Find<Text>("LevelButton/Label");
            if (!settingsSafeArea) settingsSafeArea = transform.Find("SettingsSafeArea") as RectTransform;
            if (!settingsButton) settingsButton = settingsSafeArea
                ? settingsSafeArea.GetComponentInChildren<Button>(true) : Find<Button>("SettingsButton");
            // Upgrade an already-open scene after script reload. Only the parent
            // receives the inset, so the button's authored transform stays editable.
            if (!settingsSafeArea && settingsButton && settingsButton.transform.parent == transform)
            {
                settingsSafeArea = new GameObject("SettingsSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                settingsSafeArea.gameObject.layer = settingsButton.gameObject.layer;
                settingsSafeArea.SetParent(transform, false);
                settingsSafeArea.SetSiblingIndex(settingsButton.transform.GetSiblingIndex());
                settingsSafeArea.anchorMin = Vector2.zero;
                settingsSafeArea.anchorMax = Vector2.one;
                settingsSafeArea.offsetMin = settingsSafeArea.offsetMax = Vector2.zero;
                var settingsRect = settingsButton.transform as RectTransform;
                Vector2 authoredPosition = settingsRect.anchoredPosition;
                settingsRect.SetParent(settingsSafeArea, false);
                settingsRect.anchoredPosition = authoredPosition;
            }
            if (!settingsLabel) settingsLabel = Find<Text>("SettingsLabel");
            if (!settingsOverlay)
            {
                var overlay = transform.Find("SettingsOverlay");
                if (overlay) settingsOverlay = overlay.gameObject;
            }
            if (!settingsCard) settingsCard = transform.Find("SettingsOverlay/SettingsCard") as RectTransform;
            if (!soundButton) soundButton = Find<Button>("SettingsOverlay/SettingsCard/SoundToggle");
            if (!soundState) soundState = Find<Text>("SettingsOverlay/SettingsCard/SoundToggle/Label");
            if (!vibrationButton) vibrationButton = Find<Button>("SettingsOverlay/SettingsCard/VibrationToggle");
            if (!vibrationState) vibrationState = Find<Text>("SettingsOverlay/SettingsCard/VibrationToggle/Label");
            if (!closeButton) closeButton = Find<Button>("SettingsOverlay/SettingsCard/CloseSettingsButton");
            var gearNode = settingsButton ? settingsButton.transform.Find("GearIcon") : null;
            var gear = gearNode ? gearNode.GetComponent<HueSettingsIcon>() : null;
            if (gearNode && !gear) gear = gearNode.gameObject.AddComponent<HueSettingsIcon>();
            if (gear && !gear.GetComponent<CanvasRenderer>())
            {
                gear.gameObject.AddComponent<CanvasRenderer>();
            }
            if (gear) { gear.color = HueGame.Mute; gear.raycastTarget = false; gear.SetAllDirty(); }
            return game && view && menuCanvas && scaler && logo && levelButton && levelLabel
                && settingsButton && settingsSafeArea && settingsOverlay && settingsCard
                && soundButton && soundState && vibrationButton && vibrationState && closeButton;
        }

        private T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node ? node.GetComponent<T>() : null;
        }

        private void OnEnable()
        {
            if (!ResolveBindings()) return;
            BindButtons();
            RefreshSettings();
        }

        private void BindButtons()
        {
            if (!Application.isPlaying) return;
            // Rebind after scene/script reload; a cached flag can survive while
            // UnityEvent's runtime listeners have already been discarded.
            OnDisable();
            levelButton.onClick.AddListener(Play);
            settingsButton.onClick.AddListener(OpenSettings);
            closeButton.onClick.AddListener(CloseSettings);
            soundButton.onClick.AddListener(ToggleSound);
            vibrationButton.onClick.AddListener(ToggleVibration);
        }

        private void OnDisable()
        {
            if (levelButton) levelButton.onClick.RemoveListener(Play);
            if (settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
            if (closeButton) closeButton.onClick.RemoveListener(CloseSettings);
            if (soundButton) soundButton.onClick.RemoveListener(ToggleSound);
            if (vibrationButton) vibrationButton.onClick.RemoveListener(ToggleVibration);
        }

        public void Show()
        {
            if (!ResolveBindings()) return;
            HueSafeArea.Refresh();
            BindButtons();
            CloseSettings();
            levelLabel.text = game.Endless ? "Endless Mode" : "Level " + (game.LevelIndex + 1);
            SetCanvas(view.canvas, false);
            SetCanvas(menuCanvas, true);
            Layout(Screen.width, Screen.height, Screen.safeArea);
        }

        public void Play()
        {
            if (!IsVisible || settingsOverlay.activeSelf) return;
            SetCanvas(menuCanvas, false);
            SetCanvas(view.canvas, true);
            game.BeginFromMenu();
        }

        private static void SetCanvas(Canvas canvas, bool visible)
        {
            canvas.enabled = visible;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = visible;
        }

        public void OpenSettings() { if (settingsOverlay) settingsOverlay.SetActive(true); RefreshSettings(); }
        public void CloseSettings() { if (settingsOverlay) settingsOverlay.SetActive(false); }
        public void ToggleSound()
        {
            HueAudio.SoundEnabled = !HueAudio.SoundEnabled;
            PlayerPrefs.SetInt("cc_sound", HueAudio.SoundEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettings();
        }
        public void ToggleVibration()
        {
            HueAudio.VibrationEnabled = !HueAudio.VibrationEnabled;
            PlayerPrefs.SetInt("cc_vibration", HueAudio.VibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            if (soundState) SetState(soundState, HueAudio.SoundEnabled);
            if (vibrationState) SetState(vibrationState, HueAudio.VibrationEnabled);
        }

        private static void SetState(Text text, bool on)
        {
            text.text = on ? "ON" : "OFF";
            text.color = on ? HueGame.Blue : HueGame.Mute;
        }

        private void Update()
        {
            if (!menuCanvas || !scaler || !logo || !levelButton || !settingsButton || !settingsSafeArea || !settingsCard)
            {
                if (!ResolveBindings()) return;
                BindButtons();
            }
            Layout(Screen.width, Screen.height, Screen.safeArea);
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape) && IsVisible && settingsOverlay.activeSelf)
                CloseSettings();
        }

        public void Layout(float width, float height, Rect safeArea)
        {
            float scale = Mathf.Min(width / 390f, height / 640f);
            scaler.scaleFactor = scale;
            float w = width / scale, h = height / scale;
            float top = HueSafeArea.TopPixels(width, height, safeArea) / scale, bottom = safeArea.yMin / scale;
            float usable = h - top - bottom;
            Place(logo, w * .5f, top + usable * .275f, 300, 112);
            logo.localScale = Vector3.one;
            Place(levelButton.GetComponent<RectTransform>(), w * .5f, top + usable * .72f, 296, 72);
            // Inset the parent, keeping the button's scene-authored position/size.
            settingsSafeArea.offsetMin = Vector2.zero;
            settingsSafeArea.offsetMax = new Vector2(0, -top);
            if (settingsLabel && settingsLabel.gameObject.activeSelf) settingsLabel.gameObject.SetActive(false);
            Place(settingsCard, w * .5f, top + usable * .5f, 326, 292);
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
