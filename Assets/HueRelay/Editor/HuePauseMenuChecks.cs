using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePauseMenuChecks
    {
        static int phase;
        static double started;
        static float frozenTime;
        static int mask, score, taps;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static float Clock(HueGame game) => (float)typeof(HueGame).GetField("now",Flags).GetValue(game);
        static void Require(bool value,string message) { if (!value) throw new Exception(message); }

        public static bool Tick(HueGame game,HueView view)
        {
            if (phase == 4) return true;
            var pause = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
            Require(pause && pause.ResolveBindings(),"Pause card bindings are missing.");
            if (phase == 0)
            {
                game.StartLevel(0);
                typeof(HueGame).GetField("built",Flags).SetValue(game,Time.unscaledTime-10);
                game.TapArrow(game.P.L.solution[0]);
                view.helpButton.onClick.Invoke();
                Require(game.IsPaused && pause.gameObject.activeSelf,"Back button did not open pause.");
                Require(!view.retryButton.gameObject.activeSelf,"Old retry button is still visible in HUD.");
                frozenTime=Clock(game); mask=game.Mask; score=game.Score; taps=game.Taps;
                started=EditorApplication.timeSinceStartup; phase=1; return false;
            }
            if (phase == 1)
            {
                if (EditorApplication.timeSinceStartup-started < .65) return false;
                Require(game.IsPaused && Clock(game)==frozenTime && game.Mask==mask && game.Score==score && game.Taps==taps,
                    "Game state/clock advanced while paused.");
                game.TapArrow(2); game.Hint(); game.AddMoves(); game.ToggleErase();
                Require(game.Mask==mask && game.Taps==taps && !game.IsChoosingErase,"Paused board accepted gameplay input.");
                pause.continueButton.onClick.Invoke();
                float resumed=(float)typeof(HueGame).GetProperty("GameTime",Flags).GetValue(game);
                Require(!game.IsPaused && !pause.gameObject.activeSelf && resumed-frozenTime < .15f,
                    "Continue skipped paused time or left the modal open.");
                started=EditorApplication.timeSinceStartup; phase=2; return false;
            }
            if (phase == 2)
            {
                if (!game.Idle)
                {
                    Require(EditorApplication.timeSinceStartup-started < 6,"Relay did not finish after Continue.");
                    return false;
                }
                game.OpenPause();
                Require(pause.tutorialButton.interactable,"Tutorial was not enabled for an idle board.");
                pause.tutorialButton.onClick.Invoke();
                Require(!game.IsPaused && game.TutorialStepIndex==0,"Pause tutorial did not start.");
                game.OpenPause();
                Require(!pause.tutorialButton.interactable,"Tutorial can recursively reopen itself.");
                pause.retryButton.onClick.Invoke();
                Require(!game.IsPaused && game.TutorialStepIndex<0 && game.Taps==0 && game.Score==0 && game.Mask==game.P.Full,
                    "Pause retry did not restore the campaign board.");
                game.OpenPause(); pause.homeButton.onClick.Invoke();
                var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>();
                Require(!game.IsPaused && menu.IsVisible && !view.canvas.enabled,"Pause Home did not return to menu.");
                menu.levelButton.onClick.Invoke();
                Require(!menu.IsVisible && !game.IsPaused && view.canvas.enabled,"Cannot play after Pause Home.");
                phase=4;
                Directory.CreateDirectory("Logs/PauseMenu");
                File.WriteAllText("Logs/PauseMenu/checks.txt","PASS: back opens pause; relay, score, moves and game clock freeze; board/items blocked; Continue preserves time; relay resumes; tutorial, tutorial retry, Home and re-entry work.\n");
                return true;
            }
            return false;
        }
    }
}
