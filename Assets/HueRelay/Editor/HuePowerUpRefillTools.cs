using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePowerUpRefillTools
    {
        public const string PrefabPath = "Assets/HueRelay/Resources/HueRelay/UI/PowerUpRefill.prefab";
        [MenuItem("Tools/Hue Relay/创建道具补充面板预制体")]
        public static void Build()
        {
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            if (!view || !view.ValidateBindings(out _)) throw new Exception("Open the HueRelay scene first.");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = Node("PowerUpRefill", view.canvas.transform);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(.11f,.13f,.25f,.35f); backdrop.raycastTarget = true;
            var panel = root.gameObject.AddComponent<HuePowerUpRefillPanel>();
            panel.card = Node("RefillCard", root);
            panel.card.anchorMin = panel.card.anchorMax = panel.card.pivot = new Vector2(.5f,.5f);
            panel.card.sizeDelta = new Vector2(326,362);
            var card = panel.card.gameObject.AddComponent<Image>();
            card.sprite = view.movesChip.sprite; card.type = view.movesChip.type;
            card.pixelsPerUnitMultiplier = view.movesChip.pixelsPerUnitMultiplier;
            card.color = Color.white; card.raycastTarget = true;
            panel.title = Label("Title", panel.card, view.resultTitle.font, "Out of Hints",24,HueGame.Ink,24,24,278,38);
            panel.itemIcon = Node("ItemIcon", panel.card).gameObject.AddComponent<Image>();
            panel.itemIcon.sprite = view.hintButton.transform.Find("Icon").GetComponent<Image>().sprite;
            panel.itemIcon.preserveAspect = true; panel.itemIcon.raycastTarget = false;
            Box(panel.itemIcon.rectTransform,131,78,64,64);
            panel.amountLabel = Label("Amount", panel.card, view.resultTitle.font, "+2",28,HueGame.Blue,24,156,278,34);
            panel.message = Label("Message", panel.card, view.resultTitle.font, "Watch a video to get 2 hints.",13,HueGame.Mute,24,196,278,32);
            panel.watchButton = Button("WatchAdButton", panel.card, view.resultPrimary, "Watch Ad",238,48);
            panel.closeButton = Button("CloseButton", panel.card, view.resultSecondary, "Close",298,42);
            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            AssetDatabase.SaveAssets();
        }
        static RectTransform Node(string name,Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer=5; rect.SetParent(parent,false); return rect;
        }
        static void Box(RectTransform rect,float x,float y,float w,float h)
        {
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
        }
        static Text Label(string name,Transform parent,Font font,string caption,int size,Color color,float x,float y,float w,float h)
        {
            var label=Node(name,parent).gameObject.AddComponent<Text>();
            label.font=font; label.fontSize=size; label.text=caption; label.color=color;
            label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false;
            Box(label.rectTransform,x,y,w,h); return label;
        }
        static Button Button(string name,Transform parent,Button style,string caption,float y,float h)
        {
            var button=UnityEngine.Object.Instantiate(style,parent,false);
            button.name=name; button.gameObject.SetActive(true); button.transform.localScale=Vector3.one;
            button.interactable=true; button.onClick=new Button.ButtonClickedEvent();
            var opacity=button.GetComponent<CanvasGroup>(); if(opacity) opacity.alpha=1;
            Box((RectTransform)button.transform,24,y,278,h);
            var text=button.GetComponentInChildren<Text>(true); text.text=caption; text.fontSize=17;
            text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=new Vector2(12,0); text.rectTransform.offsetMax=new Vector2(-12,0);
            return button;
        }
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                Build(); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
