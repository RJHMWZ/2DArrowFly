using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePowerUpChecks
    {
        const string TestKey = "cc_powerups_validation_v1";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static int phase;
        static double opened;
        static float frozenTime;
        static int mask;
        public static HuePowerUpInventory InstallInventory(HueGame game,int hints,int erasers,int extra)
        {
            PlayerPrefs.SetString(TestKey,"{\"version\":1,\"hints\":"+hints+",\"erasers\":"+erasers+",\"extraMoves\":"+extra+"}");
            PlayerPrefs.Save();
            var inventory = new HuePowerUpInventory(game.PowerUpSettings,TestKey);
            typeof(HueGame).GetField("inventory",Flags).SetValue(game,inventory);
            return inventory;
        }
        static void Draw(HueGame game) => typeof(HueGame).GetMethod("Render",Flags).Invoke(game,null);
        static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
        static Button Button(HueView view,HuePowerUpKind kind) => kind==HuePowerUpKind.Hint ? view.hintButton
            : kind==HuePowerUpKind.Eraser ? view.eraseButton : view.extraMovesButton;
        static int Count(HueGame game,HuePowerUpKind kind) => kind==HuePowerUpKind.Hint ? game.HintsRemaining
            : kind==HuePowerUpKind.Eraser ? game.ErasersRemaining : game.ExtraMovesRemaining;
        static HuePowerUpRefillPanel Panel(HueView view) => view.canvas.GetComponentInChildren<HuePowerUpRefillPanel>(true);

        public static bool Tick(HueGame game,HueView view)
        {
            if(phase==2) return true;
            if(phase==0)
            {
                PlayerPrefs.DeleteKey(TestKey);
                var fresh = new HuePowerUpInventory(game.PowerUpSettings,TestKey);
                Require(fresh.Count(HuePowerUpKind.Hint)==0 && fresh.Count(HuePowerUpKind.Eraser)==0
                    && fresh.Count(HuePowerUpKind.ExtraMoves)==0,"First launch must start with no tools.");
                // Seed a spend fixture after checking the real first-launch balance.
                fresh.DebugSetCounts(1,1,1);
                typeof(HueGame).GetField("inventory",Flags).SetValue(game,fresh);
                game.StartLevel(3); game.Hint();
                Require(game.HintsRemaining==1 && !game.CanUndo,"Undo must not spend inventory on a fresh board.");
                fresh.DebugSetCounts(0,1,1);
                game.Retry(); game.StartLevel(10);
                Require(game.HintsRemaining==0,"Retry/next level replenished inventory.");
                game.StartTutorial(); game.Hint(); game.AddMoves(); game.ToggleErase(); game.EndTutorial();
                Require(game.HintsRemaining==0 && game.ErasersRemaining==1 && game.ExtraMovesRemaining==1,
                    "Tutorial consumed/restored global inventory incorrectly.");
                game.OpenPause(); view.canvas.GetComponentInChildren<HuePauseMenu>(true).homeButton.onClick.Invoke();
                var menu=UnityEngine.Object.FindObjectOfType<HueMainMenu>(); menu.levelButton.onClick.Invoke(); game.EndTutorial();
                Require(game.HintsRemaining==0,"Home/menu re-entry reset inventory.");
                var reloaded=new HuePowerUpInventory(game.PowerUpSettings,TestKey);
                Require(reloaded.Count(HuePowerUpKind.Hint)==0,"Inventory failed persistence/reconstruction.");
                InstallInventory(game,0,0,0); game.StartLevel(3); Draw(game);
                Require(view.hintButton.interactable && view.eraseButton.interactable && view.extraMovesButton.interactable,
                    "Empty tools must remain clickable during active gameplay.");
                view.hintButton.onClick.Invoke();
                Require(game.IsPowerUpRefillOpen && Panel(view),"Empty tool did not open the authored refill panel.");
                frozenTime=(float)typeof(HueGame).GetField("now",Flags).GetValue(game);
                mask=game.Mask; opened=EditorApplication.timeSinceStartup; phase=1; return false;
            }
            if(EditorApplication.timeSinceStartup-opened<.35) return false;
            Require((float)typeof(HueGame).GetField("now",Flags).GetValue(game)==frozenTime,"Refill did not freeze the game clock.");
            game.TapArrow(0); game.OpenPause(); game.Hint(); game.AddMoves(); game.ToggleErase();
            Require(game.Mask==mask && game.Taps==0 && !game.IsPaused,"Refill modal accepted gameplay input.");
            Panel(view).closeButton.onClick.Invoke();
            Require(!game.IsPowerUpRefillOpen && game.HintsRemaining==0,"Closing refill granted a reward.");

            // Exercise one adapter at a time. The real scene adapter may return
            // no-fill synchronously before the injected callback runs in Editor.
            var ads = game.GetComponent<HueAds>();
            bool adsEnabled = ads && ads.enabled;
            if (ads) ads.enabled = false;
            try
            {
            Action<bool> callback=null;
            int requests=0;
            HuePowerUpKind received=HuePowerUpKind.Hint;
            Action<HuePowerUpKind,Action<bool>> provider=(kind,done)=>{ received=kind; callback=done; requests++; };
            game.RewardedAdRequested+=provider;
            try
            {
                foreach(HuePowerUpKind kind in Enum.GetValues(typeof(HuePowerUpKind)))
                {
                    InstallInventory(game,0,0,0); game.StartLevel(3); Draw(game);
                    Button(view,kind).onClick.Invoke(); var panel=Panel(view);
                    Require(panel.gameObject.activeSelf && panel.amountLabel.text=="+1"
                        && panel.itemIcon.sprite==Button(view,kind).transform.Find("Icon").GetComponent<Image>().sprite,
                        "Refill icon/quantity is incorrect for "+kind);
                    int before=requests, limit=game.Limit;
                    panel.watchButton.onClick.Invoke(); panel.watchButton.onClick.Invoke();
                    Require(requests==before+1 && received==kind && game.IsPowerUpAdPending && Count(game,kind)==0,
                        "An ad request duplicated or granted before completion.");
                    game.ClosePowerUpRefill(); Require(game.IsPowerUpRefillOpen,"Pending ad allowed the panel to close.");
                    var failed=callback; failed(false); failed(true);
                    Require(game.IsPowerUpRefillOpen && !game.IsPowerUpAdPending && Count(game,kind)==0,
                        "Failure/cancellation or a stale callback granted an item.");
                    panel.watchButton.onClick.Invoke(); var success=callback; success(true); success(true);
                    Require(!game.IsPowerUpRefillOpen && Count(game,kind)==1 && game.Limit==limit && game.Taps==0,
                        "Completed ad must add exactly one to the selected inventory without auto-using it.");
                    foreach(HuePowerUpKind other in Enum.GetValues(typeof(HuePowerUpKind)))
                        if(other!=kind) Require(Count(game,other)==0,"Ad granted the wrong tool.");
                    game.Retry(); game.StartLevel(10);
                    Require(Count(game,kind)==1,"Reward was lost/reset on retry or next level.");
                    var saved=new HuePowerUpInventory(game.PowerUpSettings,TestKey);
                    Require(saved.Count(kind)==1,"Reward was not persisted.");
                }
                InstallInventory(game,0,0,0); game.StartLevel(3); game.Hint(); game.WatchPowerUpAd();
                var late=callback; game.Retry(); late(true);
                Require(game.HintsRemaining==0 && !game.IsPowerUpRefillOpen,"Late ad callback changed a restarted game.");
            }
            finally { game.RewardedAdRequested-=provider; }
            // A missing real adapter must never silently grant in player builds.
            var configField=typeof(HueGame).GetField("powerUpConfig",Flags);
            var original=game.PowerUpSettings;
            var config=UnityEngine.Object.Instantiate(original);
            configField.SetValue(game,config);
            try
            {
                config.simulateAdsInEditor=false;
                game.Hint(); game.WatchPowerUpAd();
                Require(game.HintsRemaining==0 && game.IsPowerUpRefillOpen && !game.IsPowerUpAdPending,
                    "Unavailable ad granted a reward or locked the UI.");
                config.simulateAdsInEditor=true; game.WatchPowerUpAd();
                Require(game.HintsRemaining==1 && !game.IsPowerUpRefillOpen,"Explicit Editor simulation did not work.");
                config.initialHints=99;
                Require(new HuePowerUpInventory(config,TestKey).Count(HuePowerUpKind.Hint)==1,
                    "Changing initial gifts replenished an existing save.");
                InstallInventory(game,0,0,0);
                Require(new HuePowerUpInventory(config,TestKey).Count(HuePowerUpKind.Hint)==0,
                    "Reloading an empty save repeated first-launch gifts.");
            }
            finally { configField.SetValue(game,original); UnityEngine.Object.DestroyImmediate(config); }
            InstallInventory(game,3,3,3); game.StartLevel(0);
            Directory.CreateDirectory("Logs/PowerUps");
            File.WriteAllText("Logs/PowerUps/checks.txt","PASS: first-launch 0/0/0; no reset on retry/level/menu/tutorial/reload; all three empty icons clickable; modal blocks input and clock; correct icon/+1; pending request deduplication; failed/cancelled/duplicate/late callbacks grant nothing; completion pays selected item exactly once and persists; no auto-use; missing adapter safe; Editor simulation; config edits do not repeat gifts.\n");
            phase=2; return true;
            }
            finally { if (ads) ads.enabled = adsEnabled; }
        }
    }
}
