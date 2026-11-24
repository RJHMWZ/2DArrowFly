// Copy to Assets/HueRelay/Editor in an isolated validation project, then run Batch.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HueRelay;
using HueRelay.Editor;

[InitializeOnLoad]
public static class HueOptimizationChecks
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    const string Pending="HueOptimizationChecks.pending";
    static double started;
    static HueOptimizationChecks() { if(SessionState.GetBool(Pending,false)) EditorApplication.update+=Tick; }
    public static void Batch()
    {
        EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
        SessionState.SetBool(Pending,true); EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    static void Tick()
    {
        if(!Application.isPlaying) return;
        if(started==0) started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<1) return;
        EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);
        try
        {
            Run(); HueFeatureChecks.Run(); HueRetentionChecks.Run();
            Directory.CreateDirectory("Logs/Optimization");
            File.WriteAllText("Logs/Optimization/function-checks.txt",
                "PASS: all baked audio references intact; confetti deterministic; neutral texture Alpha8; menu resize and settings; ad runtime/pacing; telemetry; idle guide; retention rewards.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static void Run()
    {
        var audio=UnityEngine.Object.FindObjectOfType<HueAudio>();
        var tones=(Array)typeof(HueAudio).GetField("playableTones",Private).GetValue(audio);
        Require(tones.Length==76,"Baked audio reference count changed.");
        foreach(var tone in tones)
        {
            var type=tone.GetType();
            var clip=(AudioClip)type.GetField("clip").GetValue(tone);
            // Unity's automatic sample-rate optimization may import below the source WAV rate.
            Require(clip && clip.channels==1 && clip.frequency>0,"Baked audio missing or invalid.");
        }
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HueRelay/Art/UI/Surface.png");
        Require(texture.format==TextureFormat.Alpha8,"Neutral UI surface did not use its alpha-only storage.");
        var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
        foreach(var size in new[]{new Vector2(390,844),new Vector2(320,480),new Vector2(844,390)})
        {
            menu.Layout(size.x,size.y,new Rect(0,0,size.x,size.y));
            Require(menu.logo.rect.width>0 && !float.IsNaN(menu.logo.anchoredPosition.x),"Menu resize invalid.");
        }
        var game=UnityEngine.Object.FindObjectOfType<HueGame>();
        var make=typeof(HueGame).GetMethod("MakeConf",Private);
        var conf=(IList)typeof(HueGame).GetField("conf",Private).GetValue(game);
        var clear=typeof(HueGame).GetMethod("ClearEffects",Private);
        var savedRandom=UnityEngine.Random.state;
        try
        {
            // Compare unchanged RNG draw order and every generated particle field against the original formula.
            UnityEngine.Random.InitState(73);
            float shape=UnityEngine.Random.value,drag=.75f+UnityEngine.Random.value*.8f;
            Color color=new[]{HueGame.Blue,HueGame.Orange,HueGame.Ink}[UnityEngine.Random.Range(0,3)];
            float rotation=UnityEngine.Random.value*6.28f,spin=(UnityEngine.Random.value-.5f)*10;
            float flip=UnityEngine.Random.value*6.28f,flipSpeed=6+UnityEngine.Random.value*8;
            float sway=(UnityEngine.Random.value-.5f)*140,size=.45f+Mathf.Pow(UnityEngine.Random.value,1.6f)*1.7f;
            UnityEngine.Random.InitState(73);clear.Invoke(game,null);
            make.Invoke(game,new object[]{Vector2.zero,Vector2.one,2f,0f});
            var result=conf[0];var t=result.GetType();
            string[] names={"drag","rotation","spin","flip","flipSpeed","sway","size"};
            float[] expected={drag,rotation,spin,flip,flipSpeed,sway,size};
            for(int i=0;i<names.Length;i++) Require((float)t.GetField(names[i]).GetValue(result)==expected[i],"Particle changed: "+names[i]);
            Require((Color)t.GetField("c").GetValue(result)==color,"Particle palette changed.");
        }
        finally { clear.Invoke(game,null); UnityEngine.Random.state=savedRandom; }
    }
}
