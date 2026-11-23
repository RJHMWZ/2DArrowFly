using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HueRelay.Editor
{
    public static class HueAdChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string InventoryKey = "cc_ads_check_inventory";
        private static int checks;
        private static void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static HueAdPolicy Config()
        {
            var config = ScriptableObject.CreateInstance<HueAdPolicy>();
            config.adsEnabled = true; config.rewardedAdUnitId = "test_rewarded"; config.interstitialAdUnitId = "test_interstitial";
            return config;
        }
        private static HueAdBreakContext Win(int level = 4) => new HueAdBreakContext { won = true, resultAction = true, levelNumber = level };
        private static HueAdPacing ReadyPolicy(HueAdPolicy config)
        {
            var pacing = new HueAdPacing(config); pacing.RecordModuleStarted(0);
            for (int i = 0; i < 3; i++) pacing.RecordWin();
            return pacing;
        }

        [MenuItem("Tools/Hue Relay/Ads/Check pacing rules")]
        public static void PolicyChecks()
        {
            checks = 0; var config = Config();
            try
            {
                var pacing = ReadyPolicy(config);
                Require(pacing.InterstitialBlock(120, Win(), false, true) == null, "Normal win boundary did not qualify.");
                foreach (int level in new[] { 1, 2, 3 }) Require(pacing.InterstitialBlock(999, Win(level), false, true) == "protected_level", "Early level was not protected.");
                Require(pacing.InterstitialBlock(119, Win(), false, true) == "session_warmup", "First interstitial warmup bypassed.");
                Require(pacing.InterstitialBlock(120, Win(), true, true) == "another_ad_active", "Overlapping ads allowed.");
                Require(pacing.InterstitialBlock(120, Win(), false, false) == "not_loaded", "Unloaded interstitial was eligible.");
                foreach (int flag in new[] { 0, 1, 2 })
                {
                    var context = Win(); if (flag == 0) context.won = false; else if (flag == 1) context.resultAction = false; else context.tutorialActive = true;
                    Require(pacing.InterstitialBlock(999, context, false, true) == "not_a_winning_result_action", "Failure/tutorial/non-result showed an ad.");
                }
                pacing = new HueAdPacing(config); pacing.RecordModuleStarted(119);
                for (int i = 0; i < 3; i++) pacing.RecordWin();
                Require(pacing.InterstitialBlock(120, Win(), false, true) == "sdk_module_warmup", "15-second SDK module floor bypassed.");
                Require(pacing.InterstitialBlock(134, Win(), false, true) == null, "Module warmup did not expire.");
                pacing = new HueAdPacing(config); pacing.RecordModuleStarted(0); pacing.RecordWin(); pacing.RecordWin();
                Require(pacing.InterstitialBlock(999, Win(), false, true) == "not_enough_wins", "Win spacing bypassed.");
                pacing.RecordWin(); pacing.RecordRewardedActivity(200);
                Require(pacing.InterstitialBlock(319, Win(), false, true) == "rewarded_grace", "Reward opt-in did not suppress interstitials.");
                Require(pacing.InterstitialBlock(320, Win(), false, true) == null, "Reward grace did not expire.");
                Require(pacing.RewardedBlock(209, false) == "rewarded_cooldown", "Reward retry cooldown bypassed.");
                Require(pacing.RewardedBlock(210, false) == null, "Reward cooldown did not expire.");
                pacing.RecordInterstitialAttempt(320);
                Require(pacing.InterstitialBlock(349, Win(), false, true) == "attempt_backoff", "Failed-attempt backoff bypassed.");
                pacing.RecordInterstitialClosed(350);
                for (int i = 0; i < 3; i++) pacing.RecordWin();
                Require(pacing.InterstitialBlock(439, Win(), false, true) == "interval", "90-second interval bypassed.");
                Require(pacing.InterstitialBlock(440, Win(), false, true) == null, "Interval did not expire.");
                for (int i = 1; i < 6; i++) pacing.RecordInterstitialClosed(440 + i * 90);
                for (int i = 0; i < 3; i++) pacing.RecordWin();
                Require(pacing.InterstitialBlock(9999, Win(), false, true) == "session_cap", "Session cap bypassed.");
                config.interstitialIntervalSeconds = 0; config.firstInterstitialAfterSeconds = 0;
                Require(config.InterstitialInterval == 30 && config.FirstInterstitialDelay == 15, "Official minimum timing floors were not clamped.");
                config.adsEnabled = false;
                Require(ReadyPolicy(config).InterstitialBlock(999, Win(), false, true) == "disabled_or_missing_placement", "Master switch ignored.");
                config.adsEnabled = true; config.interstitialAdUnitId = " ";
                Require(ReadyPolicy(config).InterstitialBlock(999, Win(), false, true) == "disabled_or_missing_placement", "Blank placement accepted.");
                Directory.CreateDirectory("Logs/Ads");
                File.WriteAllText("Logs/Ads/policy-checks.txt", "PASS: " + checks + " policy checks: startup/module floors, early levels, natural win boundaries, win spacing, readiness, one ad at a time, cooldown/backoff, rewarded grace, session cap, disabled and missing placements.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private sealed class Backend : IHueAdBackend
        {
            public event Action<bool> VisibilityChanged;
            public readonly bool[] ready = new bool[2];
            public int preloads, shows;
            public bool throwOnShow;
            public bool autoLoad = true;
            public HueAdFormat format;
            public Action<HueAdOutcome> complete;
            public bool Supports(HueAdFormat value) => true;
            public bool IsReady(HueAdFormat value) => ready[(int)value];
            public void Preload(HueAdFormat value) { preloads++; if (autoLoad) ready[(int)value] = true; }
            public void Show(HueAdFormat value, Action<HueAdOutcome> callback)
            {
                shows++; format = value; complete = callback;
                if (throwOnShow) throw new InvalidOperationException("simulated show error");
            }
            public void Release(HueAdFormat value) { ready[(int)value] = false; }
            public void SetVisible(bool value) { VisibilityChanged?.Invoke(value); }
            public void Dispose() { VisibilityChanged = null; }
        }

        private static void Inventory(HueGame game)
        {
            PlayerPrefs.SetString(InventoryKey, "{\"version\":1,\"hints\":0,\"erasers\":0,\"extraMoves\":0}");
            typeof(HueGame).GetField("inventory", Private).SetValue(game, new HuePowerUpInventory(game.PowerUpSettings, InventoryKey));
        }
        private static void Board(HueGame game)
        {
            game.StartLevel(3);
            float time = (float)typeof(HueGame).GetProperty("GameTime", Private).GetValue(game);
            typeof(HueGame).GetField("built", Private).SetValue(game, time - 10);
            typeof(HueGame).GetField("now", Private).SetValue(game, time);
        }
        private static void Result(HueGame game, bool won)
        {
            Board(game);
            typeof(HueGame).GetProperty("Over").SetValue(game, true);
            typeof(HueGame).GetProperty("Won").SetValue(game, won);
            typeof(HueGame).GetField("sheetVisible", Private).SetValue(game, true);
        }

        [MenuItem("Tools/Hue Relay/Ads/Check runtime integration")]
        public static void RuntimeChecks()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run ad integration checks in Play mode.");
            checks = 0;
            var game = UnityEngine.Object.FindObjectOfType<HueGame>(); var ads = game.GetComponent<HueAds>();
            var originalPolicy = ads.policy;
            var inventoryField = typeof(HueGame).GetField("inventory", Private); var oldInventory = inventoryField.GetValue(game);
            string seenKey = (string)typeof(HueGame).GetField("tutorialSeenKey", Private).GetValue(game);
            bool hadSeen = PlayerPrefs.HasKey(seenKey); int seen = PlayerPrefs.GetInt(seenKey);
            bool hadInventory = PlayerPrefs.HasKey(InventoryKey); string oldSave = PlayerPrefs.GetString(InventoryKey, "");
            bool oldAudio = AudioListener.pause;
            var focusField = typeof(HueAds).GetField("focused", Private);
            bool oldFocus = (bool)focusField.GetValue(ads);
            var config = Config(); double time = 0;
            try
            {
                game.EndTutorial(); PlayerPrefs.SetInt(seenKey, 1);
                focusField.SetValue(ads, true);
                var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true); menu.HideForGameplay();
                Inventory(game); Board(game);
                var backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                Require(backend.preloads == 2 && backend.shows == 0, "Preloading displayed an ad or missed a format.");
                game.Hint(); Require(game.IsPowerUpRefillOpen, "Empty Hint did not open opt-in UI.");
                game.WatchPowerUpAd(); game.WatchPowerUpAd();
                Require(backend.shows == 1 && backend.format == HueAdFormat.Rewarded && game.HintsRemaining == 0 && ads.IsBusy,
                    "Rewarded ad was not opt-in/single-flight or rewarded early.");
                Require(AudioListener.pause, "Game sound was not paused for the ad.");
                var complete = backend.complete; complete(HueAdOutcome.Completed); complete(HueAdOutcome.Completed);
                Require(game.HintsRemaining == 2 && !game.IsPowerUpRefillOpen && !ads.IsBusy && AudioListener.pause == oldAudio,
                    "Completed reward was missing/duplicated, UI locked or audio not restored.");
                Inventory(game); Board(game); time += 11; ads.Tick();
                game.Hint(); game.WatchPowerUpAd(); complete = backend.complete;
                complete(HueAdOutcome.Cancelled); complete(HueAdOutcome.Completed);
                Require(game.HintsRemaining == 0 && !game.IsPowerUpAdPending && game.IsPowerUpRefillOpen,
                    "Cancellation/stale callback rewarded the player or locked opt-in UI.");
                game.ClosePowerUpRefill();
                Inventory(game); Board(game); time += 11; ads.Tick(); backend.ready[0] = false;
                game.Hint(); game.WatchPowerUpAd();
                Require(game.HintsRemaining == 0 && !game.IsPowerUpAdPending && !ads.IsBusy, "No-fill rewarded or locked the game.");
                game.ClosePowerUpRefill();

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                foreach (var kind in new[] { HuePowerUpKind.Eraser, HuePowerUpKind.ExtraMoves })
                {
                    Inventory(game); Board(game); time += 11; ads.Tick();
                    if (kind == HuePowerUpKind.Eraser) game.ToggleErase(); else game.AddMoves();
                    game.WatchPowerUpAd(); backend.complete(HueAdOutcome.Completed);
                    Require((kind == HuePowerUpKind.Eraser ? game.ErasersRemaining : game.ExtraMovesRemaining) == 2
                        && game.HintsRemaining == 0 && !game.IsPowerUpRefillOpen, "Reward did not refill only the selected tool: " + kind);
                }

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                Inventory(game); Board(game); game.Hint(); game.WatchPowerUpAd(); complete = backend.complete;
                time += config.PlaybackTimeout + 1; ads.Tick(); complete(HueAdOutcome.Completed);
                Require(!ads.IsBusy && !game.IsPowerUpAdPending && game.HintsRemaining == 0 && game.IsPowerUpRefillOpen,
                    "Reward timeout or late success locked the UI or granted a reward.");
                game.ClosePowerUpRefill();

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time);
                backend.SetVisible(false); ads.Tick();
                Require(backend.preloads == 0 && backend.shows == 0, "Ads were requested while backgrounded.");
                backend.SetVisible(true); ads.Tick();
                Require(backend.preloads == 2 && backend.shows == 0, "Returning to foreground auto-displayed an ad.");

                time = 0; backend = new Backend { autoLoad = false }; ads.Initialize(game, config, backend, () => time); ads.Tick();
                time = config.LoadTimeout + 1; ads.Tick();
                Require(backend.preloads == 2 && backend.shows == 0, "Stalled preload retried without backoff.");
                time += config.RetryDelay + 1; ads.Tick();
                Require(backend.preloads == 4 && backend.shows == 0, "Stalled preload did not recover after backoff.");

                config.adsEnabled = false; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick();
                int continued = 0; ads.TryInterstitial(Win(), () => continued++);
                Require(backend.preloads == 0 && backend.shows == 0 && continued == 1, "Disabled policy called the backend or blocked navigation.");
                config.adsEnabled = true;

                // Winning result must wait for close exactly once; all other navigation stays immediate.
                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true);
                game.ContinueResult(); game.ContinueResult(); game.ReturnHome();
                Require(game.IsAdBreakActive && ads.IsBusy && backend.shows == 1 && game.LevelIndex == 3,
                    "Winning navigation was not gated or repeated the ad.");
                complete = backend.complete; complete(HueAdOutcome.Completed); complete(HueAdOutcome.Completed);
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.LevelIndex == 4 && ads.Pacing.InterstitialsThisSession == 1,
                    "Interstitial close did not navigate/count exactly once.");
                Result(game, false); int shown = backend.shows; game.ContinueResult();
                Require(backend.shows == shown && !game.Over && !game.IsAdBreakActive, "Failure Retry showed an ad.");

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); backend.ready[1] = false; Result(game, true); game.ContinueResult();
                Require(backend.shows == 0 && game.LevelIndex == 4 && !game.IsAdBreakActive, "Unloaded interstitial blocked navigation.");

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true); game.ContinueResult();
                complete = backend.complete; time += config.PlaybackTimeout + 1; ads.Tick(); complete(HueAdOutcome.Completed);
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.LevelIndex == 4 && ads.Pacing.InterstitialsThisSession == 0
                    && AudioListener.pause == oldAudio, "Timeout/late close locked, counted or navigated twice.");

                time = 0; backend = new Backend { throwOnShow = true }; ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true); game.ContinueResult();
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.LevelIndex == 4, "Show exception blocked result navigation.");
                Require(new HuePowerUpInventory(game.PowerUpSettings, InventoryKey).Count(HuePowerUpKind.Hint) == 0,
                    "Non-rewarded ad outcome changed inventory.");
                Directory.CreateDirectory("Logs/Ads");
                File.WriteAllText("Logs/Ads/runtime-checks.txt", "PASS: " + checks + " checks: preload without showing; explicit opt-in; no early grant; exactly-once +2 persisted reward; cancellation/stale callbacks; no-fill; result navigation single-flight; failure Retry has no ads; skip unloaded; timeout/late-close recovery; show exceptions; game audio restored. Player inventory and tutorial flag restored.\n");
            }
            finally
            {
                ads.enabled = false;
                typeof(HueGame).GetMethod("CancelAdNavigation", Private).Invoke(game, null);
                game.ClosePowerUpRefill(); inventoryField.SetValue(game, oldInventory);
                if (hadSeen) PlayerPrefs.SetInt(seenKey, seen); else PlayerPrefs.DeleteKey(seenKey);
                if (hadInventory) PlayerPrefs.SetString(InventoryKey, oldSave); else PlayerPrefs.DeleteKey(InventoryKey);
                AudioListener.pause = oldAudio; PlayerPrefs.Save();
                ads.policy = originalPolicy; ads.enabled = true;
                focusField.SetValue(ads, oldFocus);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
