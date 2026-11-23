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
                Require((!pause.levelLabel || !pause.levelLabel.gameObject.activeSelf) && pause.settingsButton,
                    "Pause must hide the level label and expose Settings.");
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
                var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>();
                pause.settingsButton.onClick.Invoke();
                Require(game.IsPaused && menu.IsPauseSettingsOpen && !menu.IsVisible && menu.settingsOverlay.activeSelf,
                    "Pause Settings did not open the shared settings overlay.");
                Require(!menu.logo.gameObject.activeSelf && !menu.levelButton.gameObject.activeSelf
                    && !menu.settingsSafeArea.gameObject.activeSelf && !pause.card.gameObject.activeSelf && view.canvas.enabled
                    && !view.canvas.GetComponent<GraphicRaycaster>().enabled,
                    "Pause Settings exposed the menu or allowed game-canvas input.");
                pause.continueButton.onClick.Invoke(); pause.retryButton.onClick.Invoke();
                pause.tutorialButton.onClick.Invoke(); pause.homeButton.onClick.Invoke();
                Require(game.IsPaused && menu.IsPauseSettingsOpen && game.Mask==mask && game.Taps==taps,
                    "Underlying pause callbacks ran while Settings was open.");
                bool sound=HueAudio.SoundEnabled, vibration=HueAudio.VibrationEnabled;
                bool hadSound=PlayerPrefs.HasKey("cc_sound"),hadVibration=PlayerPrefs.HasKey("cc_vibration");
                try
                {
                    menu.soundButton.onClick.Invoke(); menu.vibrationButton.onClick.Invoke();
                    Require(HueAudio.SoundEnabled!=sound && HueAudio.VibrationEnabled!=vibration
                        && menu.soundState.text==(sound ? "OFF" : "ON")
                        && PlayerPrefs.GetInt("cc_sound")== (sound ? 0 : 1),
                        "Shared settings did not toggle/update/persist.");
                }
                finally
                {
                    HueAudio.SoundEnabled=sound; HueAudio.VibrationEnabled=vibration;
                    if(hadSound) PlayerPrefs.SetInt("cc_sound",sound ? 1 : 0); else PlayerPrefs.DeleteKey("cc_sound");
                    if(hadVibration) PlayerPrefs.SetInt("cc_vibration",vibration ? 1 : 0); else PlayerPrefs.DeleteKey("cc_vibration");
                    PlayerPrefs.Save();
                }
                started=EditorApplication.timeSinceStartup; phase=3; return false;
            }
            if (phase == 3)
            {
                if (EditorApplication.timeSinceStartup-started < .35) return false;
                var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>();
                Require(game.IsPaused && Clock(game)==frozenTime && game.Mask==mask && game.Taps==taps,
                    "Game advanced while shared Settings was open.");
                menu.closeButton.onClick.Invoke();
                Require(!menu.IsPauseSettingsOpen && !menu.IsVisible && !menu.settingsOverlay.activeSelf
                    && game.IsPaused && pause.gameObject.activeSelf && pause.card.gameObject.activeSelf && view.canvas.GetComponent<GraphicRaycaster>().enabled,
                    "Done did not return to the paused game.");
                pause.settingsButton.onClick.Invoke(); pause.settingsButton.onClick.Invoke(); menu.CloseSettings();
                Require(game.IsPaused && menu.logo.gameObject.activeSelf && menu.levelButton.gameObject.activeSelf
                    && menu.settingsSafeArea.gameObject.activeSelf,
                    "Repeated settings open/close did not restore the authored menu content.");
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
                File.WriteAllText("Logs/PauseMenu/checks.txt","PASS: no level label; five buttons including Settings; shared settings overlay over paused game without menu content; underlying actions blocked; sound/vibration toggle and persist; closing Settings returns to pause; repeated opens restore menu content; full pause/settings time excluded; Continue, tutorial, retry, Home and re-entry work.\n");
                return true;
            }
            return false;
        }
    }
}
