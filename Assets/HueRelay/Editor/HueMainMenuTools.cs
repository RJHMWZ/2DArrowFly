using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    /// <summary>复用已导入的按钮、字体和箭头，保存可直接编辑的主菜单节点。</summary>
    public static class HueMainMenuTools
    {
        static Font bold;
        static Sprite surface;

        [MenuItem("Tools/Hue Relay/创建主菜单")]
        public static void Build()
        {
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            if (!view || !game) throw new Exception("Open the HueRelay scene first.");
            var previous = UnityEngine.Object.FindObjectOfType<HueMainMenu>();
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/HueRelay/Resources/HueRelay/Fonts/Inter700.ttf");
            surface = view.movesChip.sprite;
            var root = Node("MainMenu", null);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var menu = root.gameObject.AddComponent<HueMainMenu>();
            menu.game = game; menu.view = view; menu.menuCanvas = canvas; menu.scaler = scaler;
            var bg = Image("Background", root, HueGame.Background, false);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            menu.logo = Node("TitleLogoPlaceholder", root);
            var titleLogo = Image("ComboArrowLogo", menu.logo, Color.white, false);
            titleLogo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HueRelay/Art/Brand/ComboArrowLogo.png");
            titleLogo.preserveAspect = true;
            Stretch(titleLogo.rectTransform);

            menu.levelButton = Button("LevelButton", root, HueGame.Blue, "Level 1", Color.white, 28, 296, 72);
            menu.levelLabel = menu.levelButton.GetComponentInChildren<Text>();
            var shadow = menu.levelButton.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.29f, .42f, .97f, .16f);
            shadow.effectDistance = new Vector2(0, -4);

            menu.settingsSafeArea = Node("SettingsSafeArea", root);
            Stretch(menu.settingsSafeArea);
            menu.settingsButton = Button("SettingsButton", menu.settingsSafeArea, Color.white, "", HueGame.Mute, 16, 52, 52);
            // Initial placement only; HueMainMenu never overwrites this transform.
            var settingsRect = menu.settingsButton.GetComponent<RectTransform>();
            settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(0, 1);
            settingsRect.anchoredPosition = new Vector2(36, -42);
            var gear = Node("GearIcon", menu.settingsButton.transform).gameObject.AddComponent<HueSettingsIcon>();
            gear.color = HueGame.Mute; gear.raycastTarget = false;
            Box(gear.rectTransform, 0, 0, 28, 28);
            menu.settingsLabel = Text("SettingsLabel", root, "Settings", 14, HueGame.Mute, 0, 0, 120, 24);

            var overlay = Image("SettingsOverlay", root, new Color(.11f, .13f, .25f, .22f), false);
            Stretch(overlay.rectTransform); overlay.raycastTarget = true;
            menu.settingsOverlay = overlay.gameObject;
            var card = Image("SettingsCard", overlay.transform, Color.white);
            menu.settingsCard = card.rectTransform;
            Text("SettingsTitle", card.transform, "Settings", 25, HueGame.Ink, 0, 101, 278, 38);
            Text("SoundLabel", card.transform, "Sound", 18, HueGame.Ink, -67, 38, 142, 42, TextAnchor.MiddleLeft);
            menu.soundButton = Button("SoundToggle", card.transform, HueGame.Background, "ON", HueGame.Blue, 16, 80, 42);
            Box(menu.soundButton.GetComponent<RectTransform>(), 99, 38, 80, 42);
            menu.soundState = menu.soundButton.GetComponentInChildren<Text>();
            Text("VibrationLabel", card.transform, "Vibration", 18, HueGame.Ink, -67, -18, 142, 42, TextAnchor.MiddleLeft);
            menu.vibrationButton = Button("VibrationToggle", card.transform, HueGame.Background, "ON", HueGame.Blue, 16, 80, 42);
            Box(menu.vibrationButton.GetComponent<RectTransform>(), 99, -18, 80, 42);
            menu.vibrationState = menu.vibrationButton.GetComponentInChildren<Text>();
            menu.closeButton = Button("CloseSettingsButton", card.transform, HueGame.Blue, "Done", Color.white, 18, 278, 48);
            Box(menu.closeButton.GetComponent<RectTransform>(), 0, -95, 278, 48);
            menu.CloseSettings();
            menu.Layout(390, 844, new Rect(0, 0, 390, 844));
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        static RectTransform Node(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            return rect;
        }
        static Image Image(string name, Transform parent, Color color, bool rounded = true)
        {
            var image = Node(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                image.sprite = surface;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = .55f;
            }
            return image;
        }
        static Button Button(string name, Transform parent, Color color, string caption, Color textColor, int size, float w, float h)
        {
            var image = Image(name, parent, color);
            image.raycastTarget = true;
            Box(image.rectTransform, 0, 0, w, h);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            image.gameObject.AddComponent<HueButtonFeedback>();
            Text("Label", image.transform, caption, size, textColor, 0, 0, w - 24, h);
            return button;
        }
        static Text Text(string name, Transform parent, string caption, int size, Color color, float x, float y, float w, float h, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Node(name, parent).gameObject.AddComponent<Text>();
            text.font = bold; text.fontSize = size; text.color = color; text.text = caption;
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Box(text.rectTransform, x, y, w, h);
            return text;
        }
        static void Box(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Run only in the isolated authoring project; save before rendering previews.
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                Build();
                EditorSceneManager.SaveOpenScenes();
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Capture()
        {
            Directory.CreateDirectory("Logs/MainMenu");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>();
            var camera = new GameObject("MenuPreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HueGame.Background;
            menu.view.canvas.enabled = false;
            menu.menuCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            menu.menuCanvas.worldCamera = camera; menu.menuCanvas.planeDistance = 10;
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(390, 667), new Vector2Int(844, 390) })
            {
                var rt = new RenderTexture(size.x * 2, size.y * 2, 24);
                camera.targetTexture = rt; camera.orthographicSize = size.y / 2f;
                foreach (bool settings in new[] { false, true })
                {
                    menu.settingsOverlay.SetActive(settings);
                    Canvas.ForceUpdateCanvases();
                    menu.Layout(size.x, size.y, new Rect(0, 0, size.x, size.y));
                    menu.scaler.scaleFactor *= 2;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = rt;
                    var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                    File.WriteAllBytes("Logs/MainMenu/" + size.x + "x" + size.y + (settings ? "-settings" : "-menu") + ".png", texture.EncodeToPNG());
                    RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture);
                }
                camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        public static void BatchCapture()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CheckRuntime(HueMainMenu menu)
        {
            if (!menu || menu.IsVisible || !menu.view.canvas.enabled || menu.game.TutorialStepIndex >= 0)
                throw new Exception("Returning startup must show gameplay and hide the menu after tutorial completion.");
            menu.Show();
            if (menu.GetComponentsInChildren<Button>().Length != 2)
                throw new Exception("Main menu must expose exactly two buttons.");
            if (menu.levelLabel.text != menu.game.ResumeButtonLabel)
                throw new Exception("Menu level label is incorrect.");
            // Reproduce UnityEvents losing runtime callbacks across reload while
            // all component fields are still populated. Show must rebind them.
            menu.levelButton.onClick.RemoveAllListeners();
            menu.settingsButton.onClick.RemoveAllListeners();
            menu.Show();
            bool hadSound = PlayerPrefs.HasKey("cc_sound"), hadVibration = PlayerPrefs.HasKey("cc_vibration");
            bool sound = HueAudio.SoundEnabled, vibration = HueAudio.VibrationEnabled;
            try
            {
                menu.settingsButton.onClick.Invoke();
                if (!menu.settingsOverlay.activeSelf) throw new Exception("Settings button is not bound.");
                menu.levelButton.onClick.Invoke();
                if (!menu.IsVisible) throw new Exception("Settings must block the level button.");
                menu.soundButton.onClick.Invoke(); menu.vibrationButton.onClick.Invoke();
                if (HueAudio.SoundEnabled == sound || HueAudio.VibrationEnabled == vibration
                    || PlayerPrefs.GetInt("cc_sound") != (sound ? 0 : 1)
                    || PlayerPrefs.GetInt("cc_vibration") != (vibration ? 0 : 1))
                    throw new Exception("Settings did not update and persist.");
            }
            finally
            {
                HueAudio.SoundEnabled = sound; HueAudio.VibrationEnabled = vibration;
                if (hadSound) PlayerPrefs.SetInt("cc_sound", sound ? 1 : 0); else PlayerPrefs.DeleteKey("cc_sound");
                if (hadVibration) PlayerPrefs.SetInt("cc_vibration", vibration ? 1 : 0); else PlayerPrefs.DeleteKey("cc_vibration");
                PlayerPrefs.Save();
            }
            menu.closeButton.onClick.Invoke();
            if (menu.settingsOverlay.activeSelf) throw new Exception("Settings close button is not bound.");
            menu.levelButton.onClick.Invoke();
            if (menu.IsVisible || !menu.view.canvas.enabled) throw new Exception("Level button did not enter gameplay.");
            if (menu.menuCanvas.GetComponent<GraphicRaycaster>().enabled
                || !menu.view.canvas.GetComponent<GraphicRaycaster>().enabled)
                throw new Exception("Hidden menu is still intercepting gameplay clicks.");
            if ((UIManager.Instance == null || UIManager.Instance.showTutorial) && !menu.game.HasCompletedTutorial
                && menu.game.TutorialStepIndex != 0)
                throw new Exception("First play did not launch the tutorial.");
            menu.game.EndTutorial();
            foreach (bool won in new[] { true, false })
            {
                menu.game.StartLevel(0);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(HueGame).GetMethod("Finish", flags).Invoke(menu.game, new object[] { won });
                typeof(HueGame).GetMethod("ShowSheet", flags).Invoke(menu.game, null);
                typeof(HueGame).GetMethod("Render", flags).Invoke(menu.game, null);
                if (menu.view.resultSecondaryText.text != "Home" || !menu.view.resultSecondary.gameObject.activeSelf
                    || (!won && menu.view.resultPrimaryText.text != "Retry"))
                    throw new Exception("Win/failure result actions are incorrect: won=" + won
                        + ", primary=" + menu.view.resultPrimaryText.text + ", secondary=" + menu.view.resultSecondaryText.text);
                if (!won)
                {
                    menu.view.resultPrimary.onClick.Invoke();
                    if (menu.game.Over || menu.game.ModalVisible || menu.IsVisible)
                        throw new Exception("Failure Retry did not restart gameplay.");
                    typeof(HueGame).GetMethod("Finish", flags).Invoke(menu.game, new object[] { false });
                    typeof(HueGame).GetMethod("ShowSheet", flags).Invoke(menu.game, null);
                }
                menu.view.resultSecondary.onClick.Invoke();
                if (!menu.IsVisible || menu.view.canvas.enabled || menu.game.ModalVisible)
                    throw new Exception("Home did not clear the result and return to the menu.");
                if (menu.view.canvas.GetComponent<GraphicRaycaster>().enabled
                    || !menu.menuCanvas.GetComponent<GraphicRaycaster>().enabled)
                    throw new Exception("Home did not restore menu input.");
                menu.levelButton.onClick.Invoke();
                if (menu.IsVisible || !menu.view.canvas.enabled || menu.game.Over)
                    throw new Exception("Cannot re-enter gameplay after Home.");
                menu.game.EndTutorial();
            }
            Directory.CreateDirectory("Logs/MainMenu");
            File.WriteAllText("Logs/MainMenu/checks.txt", "PASS: direct gameplay startup; Home menu shows saved entry; callbacks restored; settings and preferences; hidden canvases do not intercept input; win Home, failure Retry/Home and gameplay re-entry.\n");
        }
    }
}
