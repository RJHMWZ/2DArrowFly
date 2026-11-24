using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePauseMenuTools
    {
        [MenuItem("Tools/Hue Relay/创建暂停面板")]
        public static void Build()
        {
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            if (!view || !game) throw new Exception("Open the HueRelay scene first.");
            var previous = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var entry = view.helpButton;
            entry.name = "PauseButton";
            entry.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(entry.onClick, game.OpenPause);
            foreach (Transform child in entry.transform) child.gameObject.SetActive(false);
            var icon = Node("BackIcon",entry.transform).gameObject.AddComponent<HueBackIcon>();
            icon.color = HueGame.Ink; icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f,.5f);
            icon.rectTransform.sizeDelta = new Vector2(24,24);
            var entryRect = entry.transform as RectTransform;
            entryRect.anchorMin = entryRect.anchorMax = new Vector2(0,1);
            entryRect.pivot = new Vector2(0,.5f);
            entryRect.anchoredPosition = new Vector2(0,-22);
            entryRect.sizeDelta = new Vector2(38,38);
            view.retryButton.gameObject.SetActive(false);
            foreach (var pair in new[] {(view.scoreChip.rectTransform,.23f),(view.movesChip.rectTransform,.77f)})
            {
                pair.Item1.anchorMin = pair.Item1.anchorMax = new Vector2(pair.Item2,1);
                pair.Item1.pivot = new Vector2(.5f,1);
                pair.Item1.anchoredPosition = new Vector2(0,-3);
                pair.Item1.sizeDelta = new Vector2(76,38);
            }
            foreach (var rect in new[] {view.levelTitle.rectTransform,view.best.rectTransform})
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f,1);
                rect.sizeDelta = new Vector2(100,rect.sizeDelta.y);
            }

            var overlay = Node("PauseOverlay", view.canvas.transform);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var backdrop = overlay.gameObject.AddComponent<Image>();
            backdrop.color = HueModalBackdrop.Tint; backdrop.raycastTarget = true;
            var pause = overlay.gameObject.AddComponent<HuePauseMenu>();
            pause.card = Node("PauseCard",overlay);
            pause.card.anchorMin = pause.card.anchorMax = pause.card.pivot = new Vector2(.5f,.5f);
            pause.card.sizeDelta = new Vector2(326,322);
            var source = view.movesChip;
            var card = pause.card.gameObject.AddComponent<Image>();
            card.sprite = source.sprite; card.type = source.type;
            card.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            card.color = Color.white; card.raycastTarget = true;
            Label("Title",pause.card,view.resultTitle,"Paused",24,HueGame.Ink,24,24,278,38);
            pause.continueButton = Button("ContinueButton",pause.card,view.resultPrimary,"Continue",82);
            pause.retryButton = Button("RetryButton",pause.card,view.resultSecondary,"Retry",138);
            pause.settingsButton = Button("SettingsButton",pause.card,view.resultSecondary,"Settings",194);
            pause.homeButton = Button("HomeButton",pause.card,view.resultSecondary,"Home",250);
            pause.Hide();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }
        static RectTransform Node(string name,Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent,false); return rect;
        }
        static void Box(RectTransform rect,float x,float y,float w,float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h);
        }
        static Text Label(string name,Transform parent,Text style,string text,int size,Color color,float x,float y,float w,float h)
        {
            var label = Node(name,parent).gameObject.AddComponent<Text>();
            label.font = style.font; label.fontSize = size; label.color = color;
            label.text = text; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            Box(label.rectTransform,x,y,w,h); return label;
        }
        static Button Button(string name,Transform parent,Button style,string caption,float y)
        {
            var button = UnityEngine.Object.Instantiate(style,parent,false);
            button.name = name; button.gameObject.SetActive(true);
            button.transform.localScale = Vector3.one;
            button.interactable = true; button.onClick = new Button.ButtonClickedEvent();
            var opacity = button.GetComponent<CanvasGroup>();
            if (opacity) opacity.alpha = 1;
            Box(button.transform as RectTransform,24,y,278,48);
            var text = button.GetComponentInChildren<Text>(true);
            text.text = caption; text.fontSize = 17;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12,0); text.rectTransform.offsetMax = new Vector2(-12,0);
            return button;
        }
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                Build(); EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BatchUpgrade()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                var view = UnityEngine.Object.FindObjectOfType<HueView>();
                var pause = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
                if (!pause || !pause.ResolveBindings()) throw new Exception("Pause bindings missing.");
                EditorSceneManager.MarkSceneDirty(pause.gameObject.scene);
                EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
