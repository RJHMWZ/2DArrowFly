#if UNITY_EDITOR || DEVELOPMENT_BUILD || (IVY_SDK_TT && TTSDK_LOG_DEBUG)
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    // Uses the SDK's existing Debug build symbol; never changes SDK settings or initialization.
    public sealed class HueRuntimeDebugPanel : MonoBehaviour
    {
        private HueGame game;
        private HueView view;
        private RectTransform canvasRect, card;
        private GameObject overlay;
        private Button entry, fresh, jump, win;
        private InputField level;
        private Text status;
        private Font font;
        private float nextRefresh;
        private string message;
        private readonly HuePanelEntrance entrance = new HuePanelEntrance();

        public bool IsOpen => overlay && overlay.activeSelf;

        public bool BlocksPointer(Vector2 logical, float pixelScale)
        {
            if (!isActiveAndEnabled) return false;
            if (IsOpen) return true;
            Vector2 screen = new Vector2(logical.x * pixelScale, Screen.height - logical.y * pixelScale);
            return entry && entry.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)entry.transform, screen);
        }

        public static HueRuntimeDebugPanel Attach(HueGame game, HueView view)
        {
            var existing = game.GetComponentInChildren<HueRuntimeDebugPanel>(true);
            if (existing) return existing;
            var root = new GameObject("DeviceDebugCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HueRuntimeDebugPanel));
            root.layer = view.canvas.gameObject.layer;
            root.transform.SetParent(game.transform, false);
            var panel = root.GetComponent<HueRuntimeDebugPanel>();
            panel.Initialize(game, view);
            return panel;
        }

        private void Initialize(HueGame owner, HueView bindings)
        {
            game = owner; view = bindings; canvasRect = (RectTransform)transform;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = .5f;
            font = Resources.Load<Font>("HueRelay/Fonts/ChineseGame");
            if (!font) font = view.resultPrimaryText.font;

            entry = MakeButton("DebugEntry", transform, "DBG", new Rect(0, 0, 56, 34), Open, true);
            var entryRect = (RectTransform)entry.transform;
            entryRect.anchorMin = entryRect.anchorMax = Vector2.zero;
            entryRect.pivot = Vector2.zero;
            entry.GetComponentInChildren<Text>().fontSize = 14;

            overlay = Node("DebugOverlay", transform).gameObject;
            Stretch((RectTransform)overlay.transform);
            overlay.AddComponent<Image>().color = HueModalBackdrop.Tint;
            card = Node("DebugCard", overlay.transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(326, 400);
            var background = card.gameObject.AddComponent<Image>();
            StyleImage(background, Color.white);
            Label("Title", card, "真机调试", new Rect(24, 20, 278, 34), 24, HueGame.Ink);
            status = Label("Status", card, "", new Rect(24, 62, 278, 40), 13, HueGame.Mute);
            fresh = MakeButton("FreshStart", card, "模拟首次进入", new Rect(24, 112, 278, 48),
                () => Execute(game.DebugFreshStart), true);
            Label("FreshDescription", card, "重置关卡、道具与最高分，直接进入第一关。",
                new Rect(24, 166, 278, 32), 12, HueGame.Mute);

            var input = Node("LevelInput", card);
            Box(input, new Rect(24, 212, 100, 46));
            StyleImage(input.gameObject.AddComponent<Image>(), new Color(.91f, .93f, 1));
            var text = Label("Text", input, "", new Rect(8, 0, 84, 46), 20, HueGame.Ink);
            var placeholder = Label("Placeholder", input, "关卡号", new Rect(8, 0, 84, 46), 16, HueGame.Mute);
            level = input.gameObject.AddComponent<InputField>();
            level.textComponent = text; level.placeholder = placeholder;
            level.contentType = InputField.ContentType.IntegerNumber;
            level.keyboardType = TouchScreenKeyboardType.NumberPad;
            level.characterLimit = 3;
            jump = MakeButton("JumpLevel", card, "直接跳关", new Rect(136, 212, 166, 46), Jump, false);
            win = MakeButton("WinLevel", card, "当前关直接胜利", new Rect(24, 272, 278, 48),
                () => Execute(() => game.DebugFinish(true)), false);
            MakeButton("Close", card, "关闭", new Rect(24, 338, 278, 42), Close, false);
            overlay.SetActive(false);
            Layout();
        }

        public void Open()
        {
            if (!game || !game.DebugCanEdit || IsOpen) return;
            game.DebugSetPanelOpen(true);
            message = null;
            level.text = (game.LevelIndex + 1).ToString();
            overlay.SetActive(true); entry.gameObject.SetActive(false);
            Refresh(); Layout();
            entrance.Begin(card, overlay.GetComponent<Image>(), Time.unscaledTime);
        }

        public void Close()
        {
            entrance.Reset();
            if (game) game.DebugSetPanelOpen(false);
            if (overlay) overlay.SetActive(false);
            if (entry) entry.gameObject.SetActive(true);
        }

        private void Jump()
        {
            if (!int.TryParse(level.text, out int number) || number < 1 || number > game.LevelCount)
            {
                message = "请输入 1–" + game.LevelCount + " 的关卡号。";
                Refresh(); return;
            }
            Execute(() => game.DebugJumpToLevel(number));
        }

        private void Execute(Action action)
        {
            if (!game || !game.DebugCanEdit) { message = "请等待广告或关卡加载结束。"; Refresh(); return; }
            Close();
            try { action(); }
            catch (Exception e) { Open(); message = e.Message; Refresh(); }
        }

        private void Update()
        {
            if (!game) return;
            Layout();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            entry.interactable = game.DebugCanEdit;
            if (IsOpen) Refresh();
        }


        private void Refresh()
        {
            bool ready = game && game.DebugCanEdit;
            fresh.interactable = jump.interactable = win.interactable = ready;
            string value = message ?? ("当前第 " + (game.LevelIndex + 1) + " 关 · 存档第 " + (game.ResumeLevelIndex + 1)
                + " 关\n" + (ready ? "仅修改游戏测试数据" : "请等待广告或关卡加载结束"));
            if (status.text != value) status.text = value;
        }

        private void Layout()
        {
            if (!canvasRect || !card) return;
            Vector2 size = canvasRect.rect.size;
            float scale = Mathf.Max(.1f, Mathf.Min(1, Mathf.Min((size.x - 24) / 326, (size.y - 24) / 400)));
            if (entrance.IsActive) entrance.SetLayout(entrance.RestPosition, scale, Time.unscaledTime);
            else if (card.localScale.x != scale) card.localScale = Vector3.one * scale;
            var rect = (RectTransform)entry.transform;
            Rect safe = Screen.safeArea;
            float x = Screen.width > 0 ? safe.xMin / Screen.width * size.x : 0;
            float y = Screen.height > 0 ? safe.yMin / Screen.height * size.y : 0;
            rect.anchoredPosition = new Vector2(x + 10, y + 104);
        }

        private RectTransform Node(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private Text Label(string name, Transform parent, string caption, Rect box, int size, Color tint)
        {
            var label = Node(name, parent).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = caption;
            label.color = tint; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            Box(label.rectTransform, box);
            return label;
        }

        private Button MakeButton(string name, Transform parent, string caption, Rect box, UnityEngine.Events.UnityAction clicked, bool primary)
        {
            var rect = Node(name, parent); Box(rect, box);
            var background = rect.gameObject.AddComponent<Image>();
            StyleImage(background, primary ? HueGame.Blue : new Color(.9f, .91f, .94f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(clicked);
            var label = Label("Label", rect, caption, new Rect(8, 0, box.width - 16, box.height), 18,
                primary ? Color.white : HueGame.Ink);
            Stretch(label.rectTransform);
            return button;
        }

        private void StyleImage(Image image, Color tint)
        {
            image.sprite = view.movesChip.sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = view.movesChip.pixelsPerUnitMultiplier;
            image.color = tint;
        }

        private static void Box(RectTransform rect, Rect box)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(box.x, -box.y);
            rect.sizeDelta = box.size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDisable()
        {
            entrance.Reset();
            if (game && game.IsRuntimeDebugOpen) game.DebugSetPanelOpen(false);
        }
    }
}
#endif
