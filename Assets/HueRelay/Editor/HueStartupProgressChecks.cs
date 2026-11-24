using System;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HueRelay.Editor
{
    public static class HueStartupProgressChecks
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string ProgressKey = HueCampaignProgress.KeyFor("hue100_v2") + "__validation";
        static int phase, previousGame;
        static double started;
        static int hints, erasers, extra;
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static void Finish(HueGame game,bool won) => typeof(HueGame).GetMethod("Finish",Flags).Invoke(game,new object[]{won});
        static void Sheet(HueGame game) => typeof(HueGame).GetMethod("ShowSheet",Flags).Invoke(game,null);

        public static void Prepare()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey("cc_seen_validation");
            PlayerPrefs.DeleteKey("cc_seen_validation_started");
            PlayerPrefs.Save();
            var manager=UnityEngine.Object.FindObjectOfType<UIManager>();
            manager.startingLevel=1; manager.showTutorial=true;
        }
        public static bool Tick(HueGame game,HueView view)
        {
            if(phase==3) return true;
            var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            if(phase==0)
            {
                Require(!menu.IsVisible && view.canvas.enabled && !game.IsTutorial && game.LevelIndex==0 && game.P.L.id==1,
                    "Fresh startup must enter campaign level one directly without the removed tutorial board.");
                Require(game.HasCompletedTutorial && game.Score==0 && game.Taps==0 && game.ResumeLevelIndex==0,
                    "Fresh startup did not initialize legacy gates and the first campaign state.");
                hints=game.HintsRemaining; erasers=game.ErasersRemaining; extra=game.ExtraMovesRemaining;
                game.StartLevel(3); Finish(game,true);
                Require(PlayerPrefs.GetInt(ProgressKey)==4 && game.LevelIndex==3,
                    "Victory must save level five immediately without changing the result's level four.");
                Finish(game,true);
                Require(PlayerPrefs.GetInt(ProgressKey)==4,"Duplicate victory advanced progress twice.");
                Require(new HueCampaignProgress(ProgressKey,100,0).NextLevelIndex==4,"Saved progress did not reconstruct.");
                Sheet(game); game.ReturnHome();
                Require(menu.IsVisible && menu.levelLabel.text=="Level 5","Win Home shows the completed level instead of the next level.");
                menu.levelButton.onClick.Invoke();
                Require(!menu.IsVisible && game.LevelIndex==4 && !game.IsTutorial,"Home Play failed to enter saved level five.");
                Finish(game,false); Sheet(game); game.ReturnHome();
                Require(menu.levelLabel.text=="Level 5" && PlayerPrefs.GetInt(ProgressKey)==4,"Failure changed saved progress.");
                phase=2;
                previousGame=game.GetInstanceID();
                started=UnityEditor.EditorApplication.timeSinceStartup;
                // A real scene reload exercises Awake, canvas input, disk progress and tutorial gating again.
                SceneManager.LoadScene(HueMiniGameTools.ScenePath,LoadSceneMode.Single);
                return false;
            }
            Require(!menu.IsVisible && view.canvas.enabled && game.LevelIndex==4 && !game.IsTutorial && game.HasCompletedTutorial,
                "Cold re-entry must directly restore saved level five without repeating tutorial. Menu="+menu.IsVisible
                +", canvas="+view.canvas.enabled+", level="+game.LevelIndex+", tutorial="+game.TutorialStepIndex
                +", seen="+game.HasCompletedTutorial+", stored="+PlayerPrefs.GetInt(ProgressKey));
            Require(game.HintsRemaining==hints && game.ErasersRemaining==erasers && game.ExtraMovesRemaining==extra,
                "Scene reload changed persistent item inventory.");
            game.StartLevel(0); Finish(game,true);
            Require(PlayerPrefs.GetInt(ProgressKey)==4,"Replaying an earlier level regressed progress.");
            game.BeginFromMenu(); game.OpenHelp();
            Require(!game.IsTutorial,"Tutorial replayed after first-run completion.");
            Require(game.LevelIndex==4 && game.ResumeLevelIndex==4,"Re-entry changed the current level.");
            PlayerPrefs.DeleteKey("cc_seen_validation");
            PlayerPrefs.DeleteKey("cc_seen_validation_started");
            game.BeginFromMenu(); game.OpenHelp();
            Require(game.HasCompletedTutorial && !game.IsTutorial && game.LevelIndex==4,
                "A save without legacy tutorial flags must still enter its campaign level directly.");
            const string boundsKey="cc_campaign_bounds_validation";
            PlayerPrefs.DeleteKey(boundsKey);
            var bounds=new HueCampaignProgress(boundsKey,100,0); bounds.Complete(99); bounds.Complete(0);
            Require(bounds.Completed && bounds.NextLevelIndex==99 && PlayerPrefs.GetInt(boundsKey)==100,
                "Campaign completion must use a bounded sentinel, not expose level 101.");
            PlayerPrefs.DeleteKey(boundsKey); PlayerPrefs.Save();
            Directory.CreateDirectory("Logs/Progress");
            File.WriteAllText("Logs/Progress/checks.txt","PASS: fresh startup directly enters campaign level one without any separate tutorial; missing legacy tutorial flags do not change entry; inventory untouched; victory saved before result/buttons; Home label and Play use next level; failure/replay do not change/regress progress; real scene reload resumes saved level directly; tutorial is not repeated and has no manual replay entry; level 100 bounded completion sentinel.\n");
            phase=3; return true;
        }
        public static bool WaitingForReload(HueGame game)
        {
            if(phase!=2 || (game && game.GetInstanceID()!=previousGame && game.P!=null)) return false;
            Require(UnityEditor.EditorApplication.timeSinceStartup-started<15,"Scene reload did not initialize a new game instance.");
            return true;
        }
    }
}
