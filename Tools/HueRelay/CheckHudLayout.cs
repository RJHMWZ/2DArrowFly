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
                float y = safeTop + (view.topPadding + view.toolbarHeight + view.sectionGap + view.HudHeight
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
                view.SetHud("12345",HueGame.Ink,1.12f,0,"Best 54321","Level 100","Moves 13/15",HueGame.Ink,.5f,false,0,6,12,false);
                Canvas.ForceUpdateCanvases();
                var title = view.HudBounds(view.levelTitle.rectTransform);
                var best = view.HudBounds(view.best.rectTransform);
                var center = Rect.MinMaxRect(Mathf.Min(title.xMin,best.xMin),Mathf.Min(title.yMin,best.yMin),
                    Mathf.Max(title.xMax,best.xMax),Mathf.Max(title.yMax,best.yMax));
                var boxes = new List<Rect> { view.HudBounds(view.helpButton.transform as RectTransform),
                    view.HudBounds(view.scoreChip.rectTransform),
                    center,view.HudBounds(view.movesChip.rectTransform) };
                string context = size + ", safeTop=" + safeTop;
                Require(Mathf.Abs(center.center.x-width/2)<.1f,"Level/best must be centered: " + context);
                Require(Mathf.Abs((boxes[1].center.x+boxes[3].center.x)/2-center.center.x)<.1f
                    && Mathf.Abs(boxes[1].width-boxes[3].width)<.1f
                    && Mathf.Abs(boxes[1].height-boxes[3].height)<.1f,
                    "Score and moves must be symmetric and equally sized: " + context);
                Require(!view.retryButton.gameObject.activeSelf,"Old retry must be hidden: " + context);
                Require(Mathf.Abs(boxes[0].center.y-boxes[1].center.y)<3*k,
                    "Back must stay in the score row, away from the top edge: " + context);
                Require(boxes[0].xMax < boxes[1].xMin && boxes[1].xMax < boxes[2].xMin
                    && boxes[2].xMax < boxes[3].xMin,
                    "HUD must remain ordered back/score/level/moves: " + context);
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
            File.WriteAllText("Logs/HudLayout/checks.txt","PASS: "+cases+" viewport/safe-area cases; back/score/level/moves share one row, never overlap, stay on screen and below platform UI.\n");
            if (exitOnCompletion) EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); if (exitOnCompletion) EditorApplication.Exit(1); else throw; }
    }
    static void Require(bool condition,string message) { if (!condition) throw new Exception(message); }
}
