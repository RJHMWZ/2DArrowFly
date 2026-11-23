// Copy to Assets/Editor in the isolated validation project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using HueRelay;

public static class HueGlobalBestChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Key = "cc_global_best_v1__validation";
    const string Campaign = "hue100_v2";
    static HueGame game;
    static readonly Dictionary<string,int?> saved = new Dictionary<string,int?>();
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    static object Call(string name, params object[] args) => typeof(HueGame).GetMethod(name,Flags).Invoke(game,args);
    static void Field(string name,object value) => typeof(HueGame).GetField(name,Flags).SetValue(game,value);
    static int Best => (int)typeof(HueGame).GetField("bestScore",Flags).GetValue(game);
    static bool Record => (bool)typeof(HueGame).GetField("record",Flags).GetValue(game);
    static void Property(string name,object value) => typeof(HueGame).GetProperty(name).SetValue(game,value);
    static void Track(string key)
    {
        saved[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
        PlayerPrefs.DeleteKey(key);
    }
    static void Settle(int score,bool win)
    {
        Field("tut",-1); Field("sheetVisible",false);
        Property("Score",score); Property("Won",win); Property("Over",true);
        Call("ShowSheet");
    }
    public static void Run()
    {
        int exitCode=0;
        try
        {
            Track(Key); Track("cc_best__validation");
            for(int i=0;i<100;i++) { Track("cc_"+Campaign+"_b"+i+"__validation"); Track("cc_b"+i+"__validation"); }
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            game=UnityEngine.Object.FindObjectOfType<HueGame>();
            Call("Awake");
            Require(Best==0,"Fresh save did not start with zero best.");
            if(game.IsTutorial) game.EndTutorial();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.SetInt("cc_"+Campaign+"_b2__validation",450);
            PlayerPrefs.SetInt("cc_"+Campaign+"_b99__validation",900);
            PlayerPrefs.SetInt("cc_b4__validation",650);
            PlayerPrefs.SetInt("cc_best__validation",750);
            Call("LoadGlobalBest",Campaign);
            Require(Best==900 && PlayerPrefs.GetInt(Key)==900,"Legacy records did not merge to their maximum.");
            for(int i=0;i<game.LevelCount;i++)
            { game.StartLevel(i); Require(Best==900,"Level switch changed global best at "+i); }
            game.StartLevel(0); Settle(899,true);
            Require(Best==900 && !Record,"Lower score replaced best or showed NEW BEST.");
            game.StartLevel(1); Settle(900,true);
            Require(Best==900 && !Record,"Equal score showed NEW BEST.");
            game.StartLevel(5); Settle(1200,true);
            Require(Best==1200 && Record && PlayerPrefs.GetInt(Key)==1200,"Later level did not refresh/save global best.");
            var view=UnityEngine.Object.FindObjectOfType<HueView>(); Call("RenderChrome");
            Require(view.best.text=="Best 1200","HUD did not display global best.");
            game.StartLevel(0); Settle(1800,false);
            Require(Best==1200 && PlayerPrefs.GetInt(Key)==1200,"Failure replaced completed-score record.");
            game.StartLevel(0); Property("Endless",true); Settle(1500,true);
            Require(Best==1500 && Record,"Endless play did not share the global record.");
            game.StartLevel(0); game.StartTutorial();
            Property("Score",9000); Property("Won",true); Property("Over",true); Call("ShowSheet");
            Require(Best==1500 && PlayerPrefs.GetInt(Key)==1500,"Tutorial contaminated global best.");
            game.EndTutorial(); game.StartLevel(0); game.ResetGame();
            Require(Best==1500,"Tutorial/retry reset global best.");
            // Reload uses the new save, rather than importing legacy keys again.
            PlayerPrefs.SetInt("cc_"+Campaign+"_b99__validation",99000);
            Field("bestScore",0); Call("LoadGlobalBest",Campaign);
            Require(Best==1500,"Reload failed to restore new global record.");
            Require(PlayerPrefs.GetInt("cc_"+Campaign+"_b2__validation")==450,"Migration overwrote legacy saves.");
            var compiled=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
                target=BuildTarget.WebGL,group=BuildTargetGroup.WebGL,options=ScriptCompilationOptions.None
            },Path.GetFullPath("Temp/GlobalBestPlayer"));
            Require(compiled.assemblies!=null && compiled.assemblies.Count>0,"WebGL C# compilation failed.");
            Directory.CreateDirectory("Logs/GlobalBest");
            File.WriteAllText("Logs/GlobalBest/checks.txt","PASS: fresh record; legacy record maximum migration without deleting old saves; shared best across 100 levels; lower/equal scores preserved; later-level and endless record refresh; NEW BEST and HUD; failed/tutorial scores excluded; retry/tutorial return retained record; persistent reload; WebGL Player C# compilation.\n");
        }
        catch(Exception e) { Debug.LogException(e); exitCode=1; }
        finally
        {
            foreach(var item in saved)
                if(item.Value.HasValue) PlayerPrefs.SetInt(item.Key,item.Value.Value); else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
        }
        EditorApplication.Exit(exitCode);
    }
}
