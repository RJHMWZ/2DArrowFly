using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    /// <summary>Regression checks for simulator resizes, corrupt geometry and device safe areas.</summary>
    public static class HueMenuLayoutChecks
    {
        [MenuItem("Tools/Hue Relay/Retention/Check menu adaptation")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run menu adaptation checks outside Play mode.");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            if (!menu || !menu.ResolveBindings()) throw new InvalidOperationException("Open the HueRelay scene first.");
            var ui = menu.GetComponent<HueRetentionMenu>();
            var canvas = menu.menuCanvas;
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            bool menuVisible = canvas.enabled, gameVisible = menu.view.canvas.enabled;
            bool settingsVisible = menu.settingsOverlay.activeSelf, guideVisible = ui.overlay.activeSelf;
            var settings = (RectTransform)menu.settingsButton.transform;
            menu.Layout(Screen.width, Screen.height, Screen.safeArea);
            Vector2 settingsPosition = settings.anchoredPosition, settingsSize = settings.sizeDelta;
            var camera = new GameObject("MenuAdaptationCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HueGame.Background;
            Directory.CreateDirectory("Logs/MenuAdaptation");
            int checks = 0;
            try
            {
                canvas.enabled = true; menu.view.canvas.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                menu.settingsOverlay.SetActive(false); ui.overlay.SetActive(false);
                var viewports = new[] { new Vector2Int(1170, 2532), new Vector2Int(1080, 2400),
                    new Vector2Int(390, 844), new Vector2Int(320, 480), new Vector2Int(844, 390), new Vector2Int(2532, 1170) };
                var safeAreas = new[] { new Rect(0, 102, 1170, 2289), new Rect(0, 96, 1080, 2160),
                    new Rect(0, 34, 390, 763), new Rect(0, 0, 320, 480), new Rect(47, 21, 750, 369), new Rect(141, 63, 2250, 1107) };
                for (int i = 0; i < viewports.Length; i++)
                {
                    var size = viewports[i]; var safe = safeAreas[i];
                    var target = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = target; camera.orthographicSize = size.y / 2f;
                    try
                    {
                        menu.Layout(size.x, size.y, safe); Canvas.ForceUpdateCanvases();
                        foreach (var bad in new[] { new Vector2(0, 0), new Vector2(0, size.y), new Vector2(size.x, 0),
                            new Vector2(float.NaN, size.y), new Vector2(size.x, float.PositiveInfinity), new Vector2(-1, size.y) })
                        {
                            Vector2 before = menu.settingsSafeArea.anchoredPosition;
                            float scale = menu.scaler.scaleFactor;
                            menu.Layout(bad.x, bad.y, Rect.zero);
                            Require(menu.settingsSafeArea.anchoredPosition == before && menu.scaler.scaleFactor == scale,
                                "An invalid viewport changed the layout.");
                            AssertFinite(menu); checks++;
                        }
                        menu.Layout(size.x, size.y, new Rect(float.NaN, 0, size.x, size.y));
                        AssertFinite(menu); checks++;
                        menu.Layout(size.x, size.y, Rect.zero); AssertFinite(menu); checks++;

                        // Reproduce the poisoned state observed in the user's live simulator.
                        menu.settingsSafeArea.sizeDelta = new Vector2(0, float.NaN);
                        menu.settingsSafeArea.anchoredPosition = new Vector2(0, float.NaN);
                        settings.sizeDelta = new Vector2(52, float.NaN);
                        settings.anchoredPosition = new Vector2(40, float.NaN);
                        ((RectTransform)ui.profileButton.transform.parent).anchoredPosition = new Vector2(float.NaN, float.NaN);
                        ((RectTransform)ui.profileButton.transform).sizeDelta = new Vector2(52, float.NaN);
                        ((RectTransform)ui.shortcutButton.transform).anchoredPosition = new Vector2(float.NaN, float.NaN);
                        for (int repeat = 0; repeat < 4; repeat++) menu.Layout(size.x, size.y, safe);
                        Canvas.ForceUpdateCanvases(); AssertFinite(menu); checks++;
                        Require(settings.anchoredPosition == settingsPosition && settings.sizeDelta == settingsSize,
                            "Recovery lost the authored settings position/size.");
                        foreach (var button in new[] { menu.settingsButton, ui.profileButton, ui.shortcutButton, menu.levelButton })
                            AssertInside((RectTransform)button.transform, safe, camera);
                        AssertInside(menu.logo, safe, camera);
                        Capture(camera, target, "Logs/MenuAdaptation/" + size.x + "x" + size.y + "-safe-menu.png"); checks++;
                        ui.OpenProfile(); menu.Layout(size.x, size.y, safe); Canvas.ForceUpdateCanvases();
                        AssertGuide(ui, safe, camera); ui.Close(); checks++;
                        ui.OpenShortcut(); menu.Layout(size.x, size.y, safe); Canvas.ForceUpdateCanvases();
                        AssertGuide(ui, safe, camera); ui.Close(); checks++;
                        menu.OpenSettings(); menu.Layout(size.x, size.y, safe); Canvas.ForceUpdateCanvases();
                        AssertInside(menu.settingsCard, safe, camera); menu.CloseSettings(); checks++;
                    }
                    finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                }
                File.WriteAllText("Logs/MenuAdaptation/checks.txt", "PASS: " + checks
                    + " checks over 6 viewport/safe-area configurations; empty/NaN/infinite resize frames do not mutate geometry; "
                    + "invalid safe areas fall back safely; poisoned transforms recover; authored settings position/size survive; "
                    + "all home buttons, logo, guide illustrations and modals remain inside portrait/landscape safe areas; guide text fits.\n");
                Debug.Log("PASS: menu adaptation checks (" + checks + ").");
            }
            finally
            {
                ui.overlay.SetActive(guideVisible); menu.settingsOverlay.SetActive(settingsVisible);
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                canvas.enabled = menuVisible; menu.view.canvas.enabled = gameVisible;
                settings.sizeDelta = settingsSize; settings.anchoredPosition = settingsPosition;
                menu.Layout(Screen.width, Screen.height, Screen.safeArea);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        private static void AssertFinite(HueMainMenu menu)
        {
            foreach (var rect in menu.GetComponentsInChildren<RectTransform>(true))
            {
                var p = rect.anchoredPosition; var s = rect.sizeDelta;
                Require(Finite(p.x) && Finite(p.y) && Finite(s.x) && Finite(s.y), "Non-finite geometry: "
                    + (rect.parent ? rect.parent.name + "/" : "") + rect.name);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners) Require(Finite(corner.x) && Finite(corner.y), "Non-finite world corners: " + rect.name);
            }
        }
        private static void AssertInside(RectTransform rect, Rect safe, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
                Require(p.x >= safe.xMin - 1 && p.x <= safe.xMax + 1 && p.y >= safe.yMin - 1 && p.y <= safe.yMax + 1,
                    rect.name + " falls outside safe area " + safe + ": " + p);
            }
        }
        private static void AssertGuide(HueRetentionMenu ui, Rect safe, Camera camera)
        {
            foreach (var rect in ui.card.GetComponentsInChildren<RectTransform>()) AssertInside(rect, safe, camera);
            foreach (var label in ui.card.GetComponentsInChildren<Text>())
                Require(label.preferredHeight <= label.rectTransform.rect.height + .5f,
                    "Guide text overflows its box: " + label.name + ": " + label.text);
        }
        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
