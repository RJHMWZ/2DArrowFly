// Copy to Assets/Editor in the isolated validation project; run with graphics enabled.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HueRelay;

public static class HueTutorialPresentationChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static HueGame game;
    static HueView view;
    static object Call(string name, params object[] args) => typeof(HueGame).GetMethod(name, Flags).Invoke(game, args);
    static void Set(string name, object value) => typeof(HueGame).GetField(name, Flags).SetValue(game, value);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Logs/Tutorial");
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            view = UnityEngine.Object.FindObjectOfType<HueView>();
            game = UnityEngine.Object.FindObjectOfType<HueGame>();
            Require(view.ValidateBindings(out var reason), reason);
            Call("Awake");
            if (!game.IsTutorial) game.StartTutorial();
            Require(game.IsTutorial, "Tutorial did not initialize.");
            Set("built", -100f); Set("now", 10f);
            foreach (var size in new[] { new Vector2Int(390,844), new Vector2Int(390,667), new Vector2Int(844,390) })
            {
                var rt = new RenderTexture(size.x * 2, size.y * 2, 24);
                var camera = new GameObject("TutorialPreviewCamera", typeof(Camera)).GetComponent<Camera>();
                camera.targetTexture = rt; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = HueGame.Background; camera.transform.position = new Vector3(0,0,-100);
                camera.orthographic = true; camera.orthographicSize = size.y;
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; }
                Canvas.ForceUpdateCanvases();
                Call("LayoutViewport", (float)rt.width, (float)rt.height, new Rect(0,0,rt.width,rt.height));
                Canvas.ForceUpdateCanvases();
                for (int step = 0; step < 6; step++)
                {
                    Call("TutorialStep", step); Set("tutStepAt", 0f); Set("hintAt", 0f);
                    Call("Render"); Canvas.ForceUpdateCanvases();
                    var card = (RectTransform)view.tutorialGroup.transform;
                    Require(Inside(view.stage, card), "Card escaped Stage: " + size + ", step " + step);
                    Require(Inside(card, view.tutorialText.rectTransform), "Text escaped card.");
                    Require(view.tutorialText.fontSize == 20 && view.tutorialText.color == view.tutorialTextColor,
                        "Tutorial text styling was lost.");
                    Require(view.tutorialBackground.color == view.tutorialBackgroundColor
                        && view.tutorialBackground.color.r > .85f && view.tutorialText.color.r < .2f,
                        "Tutorial must use the light background and dark text palette.");
                    Require(!view.tutorialNext.gameObject.activeSelf, "Tutorial must not show Next/Play buttons.");
                    if (step < 3)
                    {
                        Vector2 target = game.BoardOrigin + (game.P.arrs[step].Head + Vector2.one * .5f) * game.CellSize;
                        CheckGap(target, game.CellSize * .7f);
                    }
                    Capture(camera, rt, "Logs/Tutorial/" + size.x + "x" + size.y + "-" + step + ".png");
                }
                // Exercise the collision path by asking for a card directly under the arrow.
                var group = (RectTransform)view.tutorialGroup.transform;
                var canvasRect = (RectTransform)view.canvas.transform;
                var center = canvasRect.InverseTransformPoint(group.TransformPoint(group.rect.center));
                Vector2 overlap = new Vector2(center.x - canvasRect.rect.xMin, canvasRect.rect.yMax - center.y);
                view.SetTutorial("Keep a clear gap from the highlighted arrow.", 0, Scale(), 1, overlap, 12 * Scale());
                Canvas.ForceUpdateCanvases(); CheckGap(overlap, 12 * Scale());
                view.tutorialPlacement = HueView.TutorialPlacement.Top;
                view.SetTutorial("Keep a clear gap from the highlighted arrow.", 0, Scale(), 1, overlap, 12 * Scale());
                Canvas.ForceUpdateCanvases(); CheckGap(overlap, 12 * Scale());
                Require(Inside(view.stage, group), "Top placement escaped Stage.");
                view.tutorialPlacement = HueView.TutorialPlacement.Manual;
                group.anchorMin = group.anchorMax = new Vector2(.25f,.8f); group.pivot = new Vector2(.3f,.7f);
                group.anchoredPosition = new Vector2(13,-29);
                var position = group.anchoredPosition; var anchors = group.anchorMin; var pivot = group.pivot;
                view.tutorialFontSize = 26; view.tutorialCardWidth = 260;
                view.SetTutorial("A longer tutorial message must wrap correctly after changing its font and width.", 4, Scale(), .4f, overlap);
                view.SetTutorial("Manual positioning remains authored in the Scene.", 5, Scale(), 1, overlap);
                Require(group.anchoredPosition == position && group.anchorMin == anchors && group.anchorMax == anchors && group.pivot == pivot,
                    "Manual placement was overwritten.");
                Require(view.tutorialText.fontSize == 26 && Inside(group,view.tutorialText.rectTransform), "Font/width changes did not remeasure text.");
                view.tutorialPlacement = HueView.TutorialPlacement.Bottom;
                view.tutorialFontSize = 20; view.tutorialCardWidth = 318;
                camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(camera.gameObject);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            var compiled = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
                target = BuildTarget.WebGL, group = BuildTargetGroup.WebGL, options = ScriptCompilationOptions.None
            }, Path.GetFullPath("Temp/TutorialPlayer"));
            Require(compiled.assemblies != null && compiled.assemblies.Count > 0, "WebGL C# compilation failed.");
            File.WriteAllText("Logs/Tutorial/presentation.txt", "PASS: six tutorial steps at tall/short portrait and landscape (18 renders); card/text/button bounds; target separation and collision avoidance; top placement; manual position/anchors/pivot preserved; font and width remeasurement; WebGL Player C# compilation.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static float Scale() => (float)typeof(HueGame).GetField("K", Flags).GetValue(game);
    static bool Inside(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners); Rect r = parent.rect;
        foreach (var p in corners)
        {
            var q = parent.InverseTransformPoint(p);
            if (q.x < r.xMin - .1f || q.x > r.xMax + .1f || q.y < r.yMin - .1f || q.y > r.yMax + .1f) return false;
        }
        return true;
    }
    static void CheckGap(Vector2 target, float radius)
    {
        var canvasRect = (RectTransform)view.canvas.transform;
        var card = (RectTransform)view.tutorialGroup.transform;
        Vector3 world = canvasRect.TransformPoint(new Vector3(canvasRect.rect.xMin + target.x, canvasRect.rect.yMax - target.y));
        Vector2 point = card.InverseTransformPoint(world);
        float distance = Vector2.Distance(point, new Vector2(Mathf.Clamp(point.x,card.rect.xMin,card.rect.xMax), Mathf.Clamp(point.y,card.rect.yMin,card.rect.yMax))) * card.lossyScale.x / canvasRect.lossyScale.x;
        Require(distance >= view.tutorialTargetGap * Scale() + radius - .2f, "Card too close to highlighted arrow: " + distance);
    }
    static void Capture(Camera camera, RenderTexture rt, string path)
    {
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG()); RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
