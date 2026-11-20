// Run from Assets/Editor in the isolated validation project, with graphics enabled.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using HueRelay;

public static class HueNotchSafeAreaChecks
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            Require(menu && menu.ResolveBindings(), "Main menu bindings are missing.");
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.enabled = false;
            menu.gameObject.SetActive(true);
            menu.menuCanvas.enabled = true;
            menu.settingsOverlay.SetActive(false);
            var camera = new GameObject("SafeAreaPreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HueGame.Background;
            menu.menuCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            menu.menuCanvas.worldCamera = camera;
            menu.menuCanvas.planeDistance = 10;
            var button = (RectTransform)menu.settingsButton.transform;
            Vector2 position = button.anchoredPosition, sizeDelta = button.sizeDelta;
            Vector2 anchorMin = button.anchorMin, anchorMax = button.anchorMax;
            Directory.CreateDirectory("Logs/NotchSafeArea");
            int cases = 0;
            foreach (var size in new[] { new Vector2Int(390,844), new Vector2Int(390,667),
                new Vector2Int(1080,2400), new Vector2Int(1170,2532), new Vector2Int(844,390) })
            {
                var rt = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = rt;
                camera.orthographicSize = size.y / 2f;
                float scale = Mathf.Min(size.x / 390f, size.y / 640f);
                var baseline = new Rect(0,0,size.x,size.y);
                menu.Layout(size.x, size.y, baseline);
                Canvas.ForceUpdateCanvases();
                float normalTop = Top(menu, button);
                foreach (float ratio in new[] {0f, .05f, .09f})
                {
                    float topPixels = size.y * ratio;
                    var safe = new Rect(0,0,size.x,size.y-topPixels);
                    for (int repeat=0;repeat<4;repeat++) menu.Layout(size.x,size.y,safe);
                    Canvas.ForceUpdateCanvases();
                    Require(Mathf.Abs(HueSafeArea.TopPixels(size.x,size.y,safe)-topPixels)<.01f,
                        "Editor must preserve Unity's native safe area.");
                    Require(Mathf.Abs(Top(menu,button)-normalTop-topPixels)<.1f,
                        "Settings did not move by exactly the physical notch inset: " + size + "/" + ratio);
                    Require(Top(menu,button) >= topPixels, "Settings entered the notch area.");
                    Require(button.anchoredPosition==position && button.sizeDelta==sizeDelta
                        && button.anchorMin==anchorMin && button.anchorMax==anchorMax,
                        "Safe-area layout overwrote an authored settings transform.");
                    if (ratio == .09f)
                    {
                        camera.Render();
                        var previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        var texture = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                        texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply();
                        File.WriteAllBytes("Logs/NotchSafeArea/menu-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());
                        RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture);
                    }
                    cases++;
                }
                // A designer can move the button without layout restoring its old coordinates.
                button.anchoredPosition += new Vector2(7,-11);
                var edited = button.anchoredPosition;
                menu.Layout(size.x,size.y,baseline);
                Canvas.ForceUpdateCanvases();
                Require(button.anchoredPosition==edited && Mathf.Abs(Top(menu,button)-normalTop-11*scale)<.1f,
                    "Designer settings movement did not survive a safe-area reset.");
                button.anchoredPosition = position;
                camera.targetTexture=null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            // Reproduce an already-open pre-upgrade scene with the button under MainMenu.
            var oldParent = menu.settingsSafeArea;
            button.SetParent(menu.transform,false); button.anchoredPosition=position;
            UnityEngine.Object.DestroyImmediate(oldParent.gameObject);
            menu.settingsSafeArea=null;
            menu.settingsButton=null;
            Require(menu.ResolveBindings() && button.parent==menu.settingsSafeArea
                && button.anchoredPosition==position && button.sizeDelta==sizeDelta,
                "An older open scene did not recover its settings safe-area parent.");
            int hudCases = CheckGameplay(camera, menu.view, menu.menuCanvas);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            var compilation = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
                target=BuildTarget.WebGL, group=BuildTargetGroup.WebGL, options=ScriptCompilationOptions.None
            }, Path.GetFullPath("Temp/NotchSafeAreaPlayer"));
            Require(compilation.assemblies != null && compilation.assemblies.Count > 0,
                "WebGL Player compilation produced no assemblies.");
            File.WriteAllText("Logs/NotchSafeArea/checks.txt", "PASS: "+cases
                +" menu viewport/notch cases; exact physical-pixel displacement; no accumulating inset; zero-inset reset; authored button anchors/size/position preserved; designer movement and older-scene recovery.\n");
            File.AppendAllText("Logs/NotchSafeArea/checks.txt", "PASS: "+hudCases
                +" gameplay viewport/notch cases; all five HUD elements move together and preserve their authored geometry.\n"
                +"PASS: WebGL Player C# compilation, including TT-only native bridge declaration. Assemblies: "+compilation.assemblies.Count+"\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static int CheckGameplay(Camera camera,HueView view,Canvas menuCanvas)
    {
        menuCanvas.enabled=false;
        view.canvas.enabled=true;
        view.canvas.renderMode=RenderMode.ScreenSpaceCamera;
        view.canvas.worldCamera=camera;
        view.canvas.planeDistance=10;
        var scaler=view.canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        var targets=new[] {(RectTransform)view.helpButton.transform, view.scoreChip.rectTransform,
            view.levelTitle.rectTransform,view.best.rectTransform,view.movesChip.rectTransform};
        int cases=0;
        foreach(var size in new[] {new Vector2Int(390,844),new Vector2Int(390,667),
            new Vector2Int(1080,2400),new Vector2Int(1170,2532),new Vector2Int(844,390)})
        {
            var rt=new RenderTexture(size.x,size.y,24);
            camera.targetTexture=rt; camera.orthographicSize=size.y/2f;
            float pixels=size.x>size.y ? size.y/800f : size.x/390f;
            float width=size.x/pixels,height=size.y/pixels;
            float k=Mathf.Clamp(Mathf.Min(width/390,height/800),.75f,3.5f);
            float appWidth=Mathf.Min(width,460*k),appX=(width-appWidth)/2;
            scaler.scaleFactor=pixels;
            Canvas.ForceUpdateCanvases();
            Rect[] baseline=null;
            foreach(float ratio in new[] {0f,.05f,.09f})
            {
                float top=HueSafeArea.TopPixels(size.x,size.y,new Rect(0,0,size.x,size.y*(1-ratio)))/pixels;
                float stageY=top+(view.topPadding+view.toolbarHeight+view.sectionGap+view.HudHeight
                    +view.sectionGap+view.ProgressLayoutHeight)*k;
                float stageHeight=Mathf.Max(1,view.PowerUpTop(height,k,0)-view.powerUpBoardGap*k-stageY);
                view.ApplyLayout(width,height,k,appX,appWidth,top,0,stageY,stageHeight,
                    appX+appWidth/2-100,stageY+stageHeight/2-120,200,240,false,pixels);
                Canvas.ForceUpdateCanvases();
                var current=Array.ConvertAll(targets,t=>view.HudBounds(t));
                if(baseline==null) baseline=current;
                for(int i=0;i<current.Length;i++)
                    Require(current[i].yMin>=top-.1f && Mathf.Abs(current[i].yMin-baseline[i].yMin-top)<.1f
                        && Mathf.Abs(current[i].x-baseline[i].x)<.1f
                        && Vector2.Distance(current[i].size,baseline[i].size)<.1f,
                        "Gameplay HUD did not move together below the notch: "+size+"/"+ratio+"/"+targets[i].name);
                cases++;
                if(ratio==.09f)
                {
                    camera.Render();
                    var previous=RenderTexture.active; RenderTexture.active=rt;
                    var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply();
                    File.WriteAllBytes("Logs/NotchSafeArea/game-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());
                    RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            camera.targetTexture=null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
        return cases;
    }

    static float Top(HueMainMenu menu,RectTransform rect)
    {
        var corners=new Vector3[4]; rect.GetWorldCorners(corners);
        var root=(RectTransform)menu.menuCanvas.transform;
        return (root.rect.yMax-root.InverseTransformPoint(corners[1]).y)*menu.scaler.scaleFactor;
    }
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
}
