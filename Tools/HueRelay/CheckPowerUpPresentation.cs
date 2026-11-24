// Run in the isolated Unity project with graphics enabled.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HueRelay;

public static class HuePowerUpPresentationChecks
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            var game=UnityEngine.Object.FindObjectOfType<HueGame>();
            var view=UnityEngine.Object.FindObjectOfType<HueView>();
            if(game.PowerUpSettings!=Resources.Load<HuePowerUpConfig>("HueRelay/Data/PowerUps"))
                throw new Exception("Scene must resolve the global PowerUps asset.");
            foreach(var menu in UnityEngine.Object.FindObjectsOfType<HueMainMenu>(true)) menu.gameObject.SetActive(false);
            var prefab=Resources.Load<HuePowerUpRefillPanel>("HueRelay/UI/PowerUpRefill");
            var panel=UnityEngine.Object.Instantiate(prefab,view.canvas.transform,false);
            var camera=new GameObject("RefillPreviewCamera",typeof(Camera)).GetComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-100); camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=HueGame.Background;
            view.canvas.renderMode=RenderMode.ScreenSpaceCamera; view.canvas.worldCamera=camera;
            view.canvas.planeDistance=10; view.canvas.enabled=true;
            var scaler=view.canvas.GetComponent<CanvasScaler>();
            Directory.CreateDirectory("Logs/PowerUps"); int cases=0;
            foreach(var size in new[] {new Vector2Int(390,844),new Vector2Int(390,667),new Vector2Int(844,390)})
            {
                var rt=new RenderTexture(size.x,size.y,24); camera.targetTexture=rt;
                camera.orthographicSize=size.y/2f;
                float pixels=size.x>size.y ? size.y/800f : size.x/390f;
                scaler.scaleFactor=pixels;
                Canvas.ForceUpdateCanvases();
                float width=size.x/pixels,height=size.y/pixels;
                float k=Mathf.Clamp(Mathf.Min(width/390,height/800),.75f,3.5f);
                float appWidth=Mathf.Min(width,460*k),appX=(width-appWidth)/2;
                float stageY=(view.topPadding+view.TopBarHeight+view.sectionGap+view.HudHeight
                    +view.sectionGap+view.ProgressLayoutHeight)*k;
                float stageHeight=Mathf.Max(1,view.PowerUpTop(height,k,0)-view.powerUpBoardGap*k-stageY);
                view.ApplyLayout(width,height,k,appX,appWidth,0,0,stageY,stageHeight,
                    appX+appWidth/2-100,stageY+stageHeight/2-120,200,240,false,pixels);
                Canvas.ForceUpdateCanvases();
                foreach(HuePowerUpKind kind in Enum.GetValues(typeof(HuePowerUpKind)))
                {
                    var button=kind==HuePowerUpKind.Hint ? view.hintButton : kind==HuePowerUpKind.Eraser ? view.eraseButton : view.extraMovesButton;
                    var icon=button.transform.Find("Icon").GetComponent<Image>();
                    panel.Show(game,kind,icon.sprite,icon.color,2);
                    Canvas.ForceUpdateCanvases(); panel.Layout(); Canvas.ForceUpdateCanvases();
                    var corners=new Vector3[4]; panel.card.GetWorldCorners(corners);
                    var root=(RectTransform)view.canvas.transform;
                    foreach(var corner in corners)
                        if(!root.rect.Contains((Vector2)root.InverseTransformPoint(corner)))
                            throw new Exception("Refill card is clipped: "+size+"/"+kind);
                    camera.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
                    var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply();
                    File.WriteAllBytes("Logs/PowerUps/"+kind+"-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());
                    RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture); cases++;
                }
                camera.targetTexture=null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            var compiled=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
                target=BuildTarget.WebGL,group=BuildTargetGroup.WebGL,options=ScriptCompilationOptions.None
            },Path.GetFullPath("Temp/PowerUpPlayerChecks"));
            if(compiled.assemblies==null || compiled.assemblies.Count==0) throw new Exception("WebGL Player compile failed.");
            File.WriteAllText("Logs/PowerUps/presentation.txt","PASS: "+cases+" refill card/item/viewport renders; card stays inside viewport; WebGL Player C# compilation without UNITY_EDITOR.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
