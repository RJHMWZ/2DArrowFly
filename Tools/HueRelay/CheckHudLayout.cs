// Run from Assets/Editor in an isolated project with graphics enabled.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HueRelay;

public static class HueHudLayoutChecks
{
    public static void Run()
    {
        RunChecks(true);
    }
    public static void RunChecks(bool exitOnCompletion)
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>();
            if (menu) menu.gameObject.SetActive(false);
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            Require(view.ValidateBindings(out var reason), reason);
            Directory.CreateDirectory("Logs/HudLayout");
            int cases = 0;
            foreach (var size in new[] { new Vector2Int(1080,1920), new Vector2Int(1080,2400),
                new Vector2Int(1080,2520), new Vector2Int(720,1600), new Vector2Int(1170,2532),
                new Vector2Int(390,667), new Vector2Int(844,390) })
            foreach (float safeTop in new[] { 0f, 44f, 76f })
            {
                float physicalScale = size.x > size.y ? size.y / 800f : size.x / 390f;
                float width = size.x / physicalScale, height = size.y / physicalScale;
                float k = Mathf.Clamp(Mathf.Min(width / 390, height / 800), .75f, 3.5f);
                float appWidth = Mathf.Min(width, 460 * k), appX = (width - appWidth) / 2;
                float y = safeTop + (view.topPadding + view.TopBarHeight + view.sectionGap + view.HudHeight
                    + view.sectionGap + view.ProgressLayoutHeight) * k;
                float h = Mathf.Max(1, view.PowerUpTop(height,k,20) - view.powerUpBoardGap*k-y);
                var rt = new RenderTexture(size.x,size.y,24);
                var camera = new GameObject("HudPreviewCamera",typeof(Camera)).GetComponent<Camera>();
                camera.targetTexture = rt; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = HueGame.Background;
                camera.transform.position = new Vector3(0,0,-100);
                camera.orthographic = true; camera.orthographicSize = size.y / 2f;
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; }
                view.canvas.GetComponent<CanvasScaler>().scaleFactor = physicalScale;
                Canvas.ForceUpdateCanvases();
                view.ApplyLayout(width,height,k,appX,appWidth,safeTop,20,y,h,
                    appX+appWidth/2-100,y+h/2-120,200,240,false,physicalScale);
                view.SetHud("12345",HueGame.Ink,1.12f,0,"Best 54321","Level 100","Left 13",HueGame.Ink,.5f,false,0,6,12,false);
                Canvas.ForceUpdateCanvases();
                var title = view.HudBounds(view.levelTitle.rectTransform);
                var best = view.HudBounds(view.best.rectTransform);
                var boxes = new List<Rect> { view.HudBounds(view.helpButton.transform as RectTransform),
                    view.HudBounds(view.scoreChip.rectTransform),
                    title,view.HudBounds(view.movesChip.rectTransform),best };
                string context = size + ", safeTop=" + safeTop;
                Require(Mathf.Abs(title.center.x-width/2)<.1f && Mathf.Abs(best.center.x-width/2)<.1f,
                    "Level/best must be centered: " + context);
                Require(Mathf.Abs((boxes[1].center.x+boxes[3].center.x)/2-title.center.x)<.1f
                    && Mathf.Abs(boxes[1].width-boxes[3].width)<.1f
                    && Mathf.Abs(boxes[1].height-boxes[3].height)<.1f,
                    "Score and moves must be symmetric and equally sized: " + context);
                Require(!view.retryButton.gameObject.activeSelf,"Old retry must be hidden: " + context);
                Require(Mathf.Abs(boxes[0].center.y-title.center.y)<.1f && title.yMax < best.yMin
                    && best.yMax < boxes[1].yMin && boxes[1].xMax < boxes[3].xMin,
                    "Level must align with back, with best immediately below: " + context);
                Require(view.score.fontSize > view.levelTitle.fontSize && view.levelTitle.fontSize == 25
                    && view.best.fontSize == 20, "HUD font sizes are incorrect: " + context);
                var hostControls = new Rect(appX+appWidth-(view.sidePadding+112)*k,safeTop,
                    112*k,(view.topPadding+view.TopBarHeight)*k);
                foreach(var box in boxes) Require(!hostControls.Overlaps(box),"HUD entered upper-right host controls: " + context);
                Require(view.HudBounds(view.progressTrack).yMax < y+h/2-120,
                    "Progress must remain above the board: " + context);
                for (int i=0;i<boxes.Count;i++)
                {
                    Require(boxes[i].yMin >= safeTop-.1f,"HUD entered platform capsule/safe area: " + context);
                    Require(boxes[i].xMin >= appX-.1f && boxes[i].xMax <= appX+appWidth+.1f,
                        "HUD escaped viewport: " + context);
                    for (int j=i+1;j<boxes.Count;j++)
                        Require(!boxes[i].Overlaps(boxes[j]),"HUD overlap " + i + "/" + j + ": " + context);
                }
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var texture = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
                File.WriteAllBytes("Logs/HudLayout/"+size.x+"x"+size.y+"-safe"+safeTop+".png",texture.EncodeToPNG());
                RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture);
                camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(camera.gameObject);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                cases++;
            }
            File.WriteAllText("Logs/HudLayout/checks.txt","PASS: "+cases+" viewport/safe-area cases; board-attached score/moves, secondary level/best, clear upper-right host area, no overlaps.\n");
            if (exitOnCompletion) EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); if (exitOnCompletion) EditorApplication.Exit(1); else throw; }
    }
    static void Require(bool condition,string message) { if (!condition) throw new Exception(message); }
}
