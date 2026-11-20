// Run from Assets/Editor in an isolated project with graphics enabled.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HueRelay;

public static class HuePausePresentationChecks
{
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Logs/PauseMenu");
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>();
            if (menu) menu.gameObject.SetActive(false);
            var view=UnityEngine.Object.FindObjectOfType<HueView>();
            var game=UnityEngine.Object.FindObjectOfType<HueGame>();
            var pause=view.canvas.GetComponentInChildren<HuePauseMenu>(true);
            if (!view.ValidateBindings(out var reason) || !pause || !pause.ResolveBindings()) throw new Exception(reason??"Pause bindings missing.");
            foreach(var size in new[] {new Vector2Int(390,844),new Vector2Int(390,667),new Vector2Int(844,390)})
            {
                var rt=new RenderTexture(size.x*2,size.y*2,24);
                var camera=new GameObject("PausePreviewCamera",typeof(Camera)).GetComponent<Camera>();
                camera.targetTexture=rt; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=HueGame.Background; camera.transform.position=new Vector3(0,0,-100);
                camera.orthographic=true; camera.orthographicSize=size.y/2f;
                foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;}
                view.canvas.GetComponent<CanvasScaler>().scaleFactor=2;
                Canvas.ForceUpdateCanvases();
                float k=Mathf.Clamp(Mathf.Min(size.x/390f,size.y/800f),.75f,3.5f),appW=Mathf.Min(size.x,460*k);
                float y=(view.topPadding+view.toolbarHeight+view.sectionGap+view.HudHeight+view.sectionGap+view.ProgressLayoutHeight)*k;
                float h=Mathf.Max(1,view.PowerUpTop(size.y,k,0)-view.powerUpBoardGap*k-y);
                view.ApplyLayout(size.x,size.y,k,(size.x-appW)/2,appW,0,0,y,h,size.x/2-100,y+h/2-120,200,240,false,2);
                view.SetHud("120",HueGame.Ink,1,0,"Best 360","Level 1","Moves 2/6",HueGame.Ink,.5f,false,0,2,4,false);
                pause.Show(game,true); Canvas.ForceUpdateCanvases(); pause.Layout(); Canvas.ForceUpdateCanvases();
                var canvasRect=view.canvas.transform as RectTransform;
                var corners=new Vector3[4]; pause.card.GetWorldCorners(corners);
                foreach(var p in corners)
                {
                    var point=canvasRect.InverseTransformPoint(p);
                    if (!canvasRect.rect.Contains(point)) throw new Exception("Pause card escaped viewport: "+size);
                }
                camera.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
                var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
                File.WriteAllBytes("Logs/PauseMenu/"+size.x+"x"+size.y+".png",texture.EncodeToPNG());
                RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);
                camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(camera.gameObject);
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
            File.WriteAllText("Logs/PauseMenu/presentation.txt","PASS: authored pause card and four buttons rendered in tall portrait, short portrait and landscape; card stays inside viewport.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
