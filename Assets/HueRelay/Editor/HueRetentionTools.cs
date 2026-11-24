using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HueRetentionTools
    {
        private static Font font;
        private static Sprite surface;

        [MenuItem("Tools/Hue Relay/Retention/Build home entries")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop play mode before authoring retention UI.");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            if (!menu || !menu.ResolveBindings()) throw new InvalidOperationException("Open the HueRelay scene first.");
            var ui = menu.GetComponent<HueRetentionMenu>();
            if (ui && ui.entries && ui.overlay) { UpdateHomeEntries(); return; }
            font = menu.levelLabel.font;
            surface = menu.view.movesChip.sprite;
            var retention = menu.GetComponent<HueRetention>();
            if (!retention) retention = menu.gameObject.AddComponent<HueRetention>();
            retention.game = menu.game;
            if (!ui) ui = menu.gameObject.AddComponent<HueRetentionMenu>();
            ui.menu = menu; ui.retention = retention;

            ui.entries = Node("RetentionEntries", menu.transform);
            ui.profileButton = Entry("ProfileEntry", ui.entries, -.5f, HueRetentionIcon.Shape.Profile);
            ui.shortcutButton = Entry("HomeScreenEntry", ui.entries, .5f, HueRetentionIcon.Shape.Phone);

            BuildGuides(ui);
            ui.Refresh(); ui.Close();
            menu.Layout(Screen.width, Screen.height, Screen.safeArea);
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log("Retention home entries and three-step guides authored.");
        }

        [MenuItem("Tools/Hue Relay/Retention/Rebuild guide windows")]
        public static void RebuildGuides()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop play mode before authoring the guide windows.");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            var ui = menu ? menu.GetComponent<HueRetentionMenu>() : null;
            if (!ui) throw new InvalidOperationException("Build the home entries first.");
            if (ui.overlay) UnityEngine.Object.DestroyImmediate(ui.overlay);
            BuildGuides(ui);
            ui.Refresh(); ui.Close(); menu.Layout(Screen.width, Screen.height, Screen.safeArea);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        }

        private static void BuildGuides(HueRetentionMenu ui)
        {
            font = ui.menu.levelLabel.font;
            surface = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var ink = HueGame.Hex("50587f");
            var overlay = Image("RetentionOverlay", ui.menu.transform, HueModalBackdrop.Tint, false);
            Stretch(overlay.rectTransform); overlay.raycastTarget = true;
            ui.overlay = overlay.gameObject;
            ui.laterButton = overlay.gameObject.AddComponent<Button>();
            ui.laterButton.targetGraphic = overlay; ui.laterButton.transition = Selectable.Transition.None;
            ui.laterButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var card = Rounded("RetentionCard", overlay.transform, Color.white, 18);
            card.raycastTarget = true; ui.card = card.rectTransform;
            // Catch clicks on the white card so only the outside backdrop dismisses it.
            var blocker = card.gameObject.AddComponent<Button>();
            blocker.targetGraphic = card; blocker.transition = Selectable.Transition.None;
            blocker.navigation = new Navigation { mode = Navigation.Mode.None };
            Box(ui.card, 0, 0, 326, 596);
            ui.closeButton = Button("Close", ui.card, Color.clear, "", 1, Color.clear, 143, 273, 40, 40);
            Icon("CloseIcon", ui.closeButton.transform, HueRetentionIcon.Shape.Close, new Color(ink.r, ink.g, ink.b, .6f), 0, 0, 15);
            ui.title = Text("Title", ui.card, "Revisit from Profile", 21, ink, 0, 254, 282, 34);
            ui.stepNumbers = new Text[3];
            var steps = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                ui.stepNumbers[i] = Text("Step" + (i + 1) + "Number", ui.card, (i + 1).ToString(), 18, ink, -121, 0, 22, 40);
                steps[i] = Text("Step" + (i + 1) + "Text", ui.card, "", 14, ink, 17, 0, 226, 40, TextAnchor.MiddleLeft);
            }
            ui.firstStep = steps[0]; ui.secondStep = steps[1]; ui.thirdStep = steps[2];

            ui.profilePreview = Node("ProfileMenuPreview", ui.card); Box(ui.profilePreview, 0, 142, 232, 100);
            Icon("Profile", ui.profilePreview, HueRetentionIcon.Shape.Person, Color.black, -104, 24, 17);
            Text("TikTok", ui.profilePreview, "TikTok", 11, Color.black, -2, 24, 90, 20);
            var dropdown = Icon("Dropdown", ui.profilePreview, HueRetentionIcon.Shape.Chevron, Color.black, 24, 23, 8);
            dropdown.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            var highlight = Rounded("MenuHighlight", ui.profilePreview, HueGame.Background, 8);
            Box(highlight.rectTransform, 103, 24, 28, 28);
            Icon("Menu", ui.profilePreview, HueRetentionIcon.Shape.Menu, Color.black, 103, 24, 18);
            var avatar = Rounded("Avatar", ui.profilePreview, Color.black, 27);
            Box(avatar.rectTransform, 0, -17, 54, 54);
            Icon("NoteCyan", avatar.transform, HueRetentionIcon.Shape.MusicNote, new Color(.15f, .96f, .95f), -2, -1, 37);
            Icon("NotePink", avatar.transform, HueRetentionIcon.Shape.MusicNote, new Color(1, .15f, .32f), 2, 1, 37);
            Icon("Note", avatar.transform, HueRetentionIcon.Shape.MusicNote, Color.white, 0, 0, 37);
            Hand("TapMenu", ui.profilePreview, 119, 6);

            ui.minisPreview = Node("MinisMenuPreview", ui.card); Box(ui.minisPreview, 0, -12, 232, 104);
            Icon("QRCode", ui.minisPreview, HueRetentionIcon.Shape.Minis, Color.black, -104, 25, 16);
            Text("QRCodeLabel", ui.minisPreview, "Your QR code", 12, Color.black, -7, 25, 164, 26, TextAnchor.MiddleLeft);
            Icon("QRCodeArrow", ui.minisPreview, HueRetentionIcon.Shape.Chevron, HueGame.Mute, 108, 25, 12);
            var divider = Image("Divider", ui.minisPreview, new Color(0, 0, 0, .12f), false);
            Box(divider.rectTransform, 0, 0, 232, 1);
            Icon("Minis", ui.minisPreview, HueRetentionIcon.Shape.Cube, Color.black, -104, -25, 16);
            Text("MinisLabel", ui.minisPreview, "TikTok Minis", 12, Color.black, -7, -25, 164, 26, TextAnchor.MiddleLeft);
            Icon("MinisArrow", ui.minisPreview, HueRetentionIcon.Shape.Chevron, HueGame.Mute, 108, -25, 12);
            Hand("TapMinis", ui.minisPreview, 56, -45);

            ui.rewardGroup = Node("Reward", ui.card); Box(ui.rewardGroup, 0, -144, 100, 40);
            ui.rewardIcon = Image("MoveIcon", ui.rewardGroup, Color.white, false);
            ui.rewardIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HueRelay/Art/PowerUps/ExtraMoves.png");
            ui.rewardIcon.preserveAspect = true; Box(ui.rewardIcon.rectTransform, -25, 0, 24, 30);
            ui.rewardLabel = Text("Amount", ui.rewardGroup, "+1", 22, ink, 19, 0, 62, 36);
            ui.message = Text("Status", ui.card, "", 11, HueGame.Mute, 0, -188, 278, 32);
            var action = Rounded("Action", ui.card, HueGame.Blue, 25);
            action.raycastTarget = true; Box(action.rectTransform, 0, -237, 254, 50);
            ui.actionButton = action.gameObject.AddComponent<Button>(); ui.actionButton.targetGraphic = action;
            ui.actionButton.navigation = new Navigation { mode = Navigation.Mode.None };
            action.gameObject.AddComponent<HueButtonFeedback>();
            Text("Label", action.transform, "Get started", 23, Color.white, 0, 0, 242, 50);
            ui.actionLabel = ui.actionButton.GetComponentInChildren<Text>();
        }

        private static void Hand(string name, Transform parent, float x, float y)
        {
            var hand = Node(name, parent).gameObject.AddComponent<RawImage>();
            hand.texture = Resources.Load<Texture2D>("HueRelay/Textures/Finger");
            hand.raycastTarget = false; Box(hand.rectTransform, x, y, 34, 34);
            hand.rectTransform.localEulerAngles = new Vector3(0, 0, 35);
        }

        [MenuItem("Tools/Hue Relay/Retention/Update home entry style")]
        public static void UpdateHomeEntries()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop play mode before updating the home entries.");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            var ui = menu ? menu.GetComponent<HueRetentionMenu>() : null;
            if (!ui || !ui.profileButton || !ui.shortcutButton) return;
            var buttons = new[] { ui.profileButton, ui.shortcutButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var group = (RectTransform)button.transform.parent;
                group.anchorMin = group.anchorMax = new Vector2(i == 0 ? 0 : 1, .5f);
                group.pivot = new Vector2(.5f, .5f);
                group.anchoredPosition = Vector2.zero; group.sizeDelta = new Vector2(72, 96);
                var reward = group.Find("Reward");
                if (reward) UnityEngine.Object.DestroyImmediate(reward.gameObject);
                var captionNode = group.Find("Caption");
                if (captionNode) UnityEngine.Object.DestroyImmediate(captionNode.gameObject);
                Box((RectTransform)button.transform, 0, 0, 52, 52);
                var shadow = button.GetComponent<Shadow>();
                if (shadow) UnityEngine.Object.DestroyImmediate(shadow);
                var image = button.GetComponent<Image>();
                var settingsImage = menu.settingsButton.GetComponent<Image>();
                image.sprite = settingsImage.sprite; image.type = settingsImage.type;
                image.color = settingsImage.color; image.pixelsPerUnitMultiplier = settingsImage.pixelsPerUnitMultiplier;
            }
            menu.Layout(Screen.width, Screen.height, Screen.safeArea);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        }

        private static Button Entry(string name, RectTransform parent, float side,
            HueRetentionIcon.Shape shape)
        {
            var group = Node(name + "Group", parent);
            group.anchorMin = group.anchorMax = new Vector2(side < 0 ? 0 : 1, .5f);
            group.pivot = new Vector2(.5f, .5f); group.sizeDelta = new Vector2(72, 96);
            var button = Button(name, group, Color.white, "", 1, Color.clear, 0, 0, 52, 52);
            Icon("Icon", button.transform, shape, HueGame.Mute, 0, 0, 28);
            return button;
        }

        private static RectTransform Node(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false); return rect;
        }
        private static Image Image(string name, Transform parent, Color tint, bool rounded = true)
        {
            var image = Node(name, parent).gameObject.AddComponent<Image>();
            image.color = tint; image.raycastTarget = false;
            if (rounded) { image.sprite = surface; image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = .55f; }
            return image;
        }
        private static HueRoundedGraphic Rounded(string name, Transform parent, Color tint, float radius)
        {
            var graphic = Node(name, parent).gameObject.AddComponent<HueRoundedGraphic>();
            graphic.color = tint; graphic.cornerRadius = radius; graphic.raycastTarget = false;
            return graphic;
        }
        private static Button Button(string name, Transform parent, Color tint, string caption, int size, Color textColor,
            float x, float y, float width, float height)
        {
            var image = Image(name, parent, tint); image.raycastTarget = true;
            Box(image.rectTransform, x, y, width, height);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.gameObject.AddComponent<HueButtonFeedback>();
            Text("Label", image.transform, caption, size, textColor, 0, 0, width - 12, height);
            return button;
        }
        private static Text Text(string name, Transform parent, string caption, int size, Color tint,
            float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Node(name, parent).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = tint; label.text = caption;
            label.raycastTarget = false; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            Box(label.rectTransform, x, y, width, height); return label;
        }
        private static HueRetentionIcon Icon(string name, Transform parent, HueRetentionIcon.Shape shape,
            Color tint, float x, float y, float size)
        {
            var icon = Node(name, parent).gameObject.AddComponent<HueRetentionIcon>();
            icon.shape = shape; icon.color = tint; icon.raycastTarget = false;
            Box(icon.rectTransform, x, y, size, size); return icon;
        }
        private static void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        [MenuItem("Tools/Hue Relay/Retention/Capture previews")]
        public static void Capture()
        {
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            var ui = menu.GetComponent<HueRetentionMenu>();
            var mode = menu.menuCanvas.renderMode;
            var oldCamera = menu.menuCanvas.worldCamera;
            var oldDistance = menu.menuCanvas.planeDistance;
            bool menuVisible = menu.menuCanvas.enabled, gameVisible = menu.view.canvas.enabled;
            bool settings = menu.settingsOverlay.activeSelf;
            var camera = new GameObject("RetentionPreviewCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HueGame.Background;
            Directory.CreateDirectory("Logs/Retention");
            try
            {
                menu.view.canvas.enabled = false; menu.menuCanvas.enabled = true;
                menu.menuCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                menu.menuCanvas.worldCamera = camera; menu.menuCanvas.planeDistance = 10;
                menu.settingsOverlay.SetActive(false);
                foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(320, 480), new Vector2Int(844, 390) })
                {
                    var target = new RenderTexture(size.x * 2, size.y * 2, 24);
                    camera.targetTexture = target; camera.orthographicSize = size.y / 2f;
                    try
                    {
                        for (int guide = 0; guide < 3; guide++)
                        {
                            if (guide == 0) ui.Close(); else if (guide == 1) ui.OpenProfile(); else ui.OpenShortcut();
                            Canvas.ForceUpdateCanvases();
                            menu.Layout(size.x, size.y, new Rect(0, 0, size.x, size.y));
                            menu.scaler.scaleFactor *= 2;
                            Canvas.ForceUpdateCanvases(); camera.Render();
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                            try
                            {
                                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                                File.WriteAllBytes("Logs/Retention/" + size.x + "x" + size.y + "-" + guide + ".png", texture.EncodeToPNG());
                            }
                            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
                        }
                    }
                    finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                }
            }
            finally
            {
                ui.Close();
                menu.menuCanvas.renderMode = mode; menu.menuCanvas.worldCamera = oldCamera; menu.menuCanvas.planeDistance = oldDistance;
                menu.menuCanvas.enabled = menuVisible; menu.view.canvas.enabled = gameVisible;
                menu.settingsOverlay.SetActive(settings);
                menu.Layout(Screen.width, Screen.height, Screen.safeArea);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
            Debug.Log("Retention previews saved under Logs/Retention.");
        }
    }
}
