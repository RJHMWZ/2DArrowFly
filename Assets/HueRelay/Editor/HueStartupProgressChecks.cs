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
        static int phase, tappedStep = -1, previousGame;
        static double started;
        static int hints, erasers, extra;
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static void Finish(HueGame game,bool won) => typeof(HueGame).GetMethod("Finish",Flags).Invoke(game,new object[]{won});
        static void Sheet(HueGame game) => typeof(HueGame).GetMethod("ShowSheet",Flags).Invoke(game,null);

        public static void Prepare()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey("cc_seen_validation");
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
                Require(!menu.IsVisible && view.canvas.enabled && game.TutorialStepIndex==0,
                    "Fresh startup must immediately open full tutorial, not menu or the campaign chain guide.");
                Require(game.P.L.w==9 && game.P.L.h==12 && !game.HasCompletedTutorial,
                    "First startup did not use the question-mark tutorial board.");
                hints=game.HintsRemaining; erasers=game.ErasersRemaining; extra=game.ExtraMovesRemaining;
                typeof(HueGame).GetField("built",Flags).SetValue(game,
                    (float)typeof(HueGame).GetProperty("GameTime",Flags).GetValue(game)-10);
                started=UnityEditor.EditorApplication.timeSinceStartup;
                phase=1;
            }
            if(phase==1)
            {
                Require(UnityEditor.EditorApplication.timeSinceStartup-started<40,"First tutorial walkthrough timed out.");
                int step=game.TutorialStepIndex;
                if(step>=0)
                {
                    if(step<3 && tappedStep!=step) { tappedStep=step; game.TapArrow(step); }
                    else if(step>=3)
                    {
                        Require(!view.tutorialNext.gameObject.activeSelf,"Text tutorial still displays a Next/Play button.");
                        Vector2 point=step==3 ? view.HudBounds((RectTransform)view.tutorialGroup.transform).center
                            : step==4 ? Vector2.zero : game.BoardOrigin+(game.P.arrs[0].Head+Vector2.one*.5f)*game.CellSize;
                        int mask=game.Mask,score=game.Score,taps=game.Taps;
                        var pointer=typeof(HueGame).GetMethod("Pointer",Flags);
                        pointer.Invoke(game,new object[]{point});
                        if(game.TutorialStepIndex!=step)
                        {
                            int advanced=game.TutorialStepIndex;
                            pointer.Invoke(game,new object[]{point});
                            Require(game.TutorialStepIndex==advanced,"A single frame skipped multiple tutorial steps.");
                            if(advanced>=0) Require(game.Mask==mask && game.Score==score && game.Taps==taps,
                                "Advancing a text tutorial also moved an arrow.");
                        }
                    }
                    return false;
                }
                Require(game.HasCompletedTutorial && game.LevelIndex==0 && game.P.L.id==1 && !game.Over
                    && game.Taps==0 && game.Score==0 && game.ResumeLevelIndex==0,
                    "Completing the tutorial did not enter campaign level one without advancing progress.");
                Require(game.HintsRemaining==hints && game.ErasersRemaining==erasers && game.ExtraMovesRemaining==extra,
                    "Startup tutorial changed the persistent item inventory.");
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
            Require(game.TutorialStepIndex==0,"Manual tutorial stopped working after first-run completion.");
            game.EndTutorial();
            Require(game.LevelIndex==4 && game.ResumeLevelIndex==4,"Manual tutorial did not restore the current level.");
            const string boundsKey="cc_campaign_bounds_validation";
            PlayerPrefs.DeleteKey(boundsKey);
            var bounds=new HueCampaignProgress(boundsKey,100,0); bounds.Complete(99); bounds.Complete(0);
            Require(bounds.Completed && bounds.NextLevelIndex==99 && PlayerPrefs.GetInt(boundsKey)==100,
                "Campaign completion must use a bounded sentinel, not expose level 101.");
            PlayerPrefs.DeleteKey(boundsKey); PlayerPrefs.Save();
            Directory.CreateDirectory("Logs/Progress");
            File.WriteAllText("Logs/Progress/checks.txt","PASS: fresh startup goes directly into six-step full tutorial; arrow taps and anywhere taps on card/background/board complete it without Next/Play buttons or same-frame skipping; first campaign level restored; inventory untouched; victory saved before result/buttons; Home label and Play use next level; failure/replay do not change/regress progress; real scene reload resumes saved level directly; tutorial is not repeated but remains manually available; level 100 bounded completion sentinel.\n");
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
