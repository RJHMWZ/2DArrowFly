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
        private static HueAdBreakContext Win(int level = 4) => new HueAdBreakContext { won = true, beforeResult = true, levelNumber = level };
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
                ProductionPolicyChecks();
                using (var live = new HueTikTokAdBackend(config))
                {
                    Require(!live.Supports(HueAdFormat.Interstitial) && !live.IsReady(HueAdFormat.Interstitial),
                        "The SDK backend must reject the retired interstitial format.");
                    live.Preload(HueAdFormat.Interstitial);
                    bool rejected = false;
                    live.Show(HueAdFormat.Interstitial, result => rejected = result == HueAdOutcome.Unavailable);
                    Require(rejected, "A legacy format request must return unavailable without touching the SDK.");
                }
                var pacing = new HueAdPacing(config);
                Require(pacing.InterstitialBlock(0, Win(), false, true) == "not_enough_wins", "A result video requires a win.");
                pacing.RecordWin();
                Require(pacing.InterstitialBlock(0, Win(1), false, true) == "protected_level"
                    && pacing.InterstitialBlock(0, Win(2), false, true) == "protected_level", "Levels one and two must remain protected.");
                pacing.RecordRewardedActivity(0);
                Require(pacing.InterstitialBlock(0, Win(3), false, true) == null, "Level three must qualify immediately, even after a tool ad or fresh launch.");
                Require(pacing.InterstitialBlock(0, Win(), true, true) == "another_ad_active", "Overlapping videos allowed.");
                Require(pacing.InterstitialBlock(0, Win(), false, false) == "not_loaded", "Unloaded video qualified.");
                var failure = Win(); failure.won = false;
                Require(pacing.InterstitialBlock(0, failure, false, true) == "not_a_win", "Failure must not show a result video.");
                foreach (int flag in new[] { 1, 2 })
                {
                    var context = Win(); if (flag == 1) context.beforeResult = false; else context.tutorialActive = true;
                    Require(pacing.InterstitialBlock(0, context, false, true) == "not_a_result_boundary", "Non-result/tutorial video allowed.");
                }
                for (int group = 0; group < 9; group++)
                {
                    Require(pacing.InterstitialBlock(0, Win(), false, true) == null, "A time gate or session cap suppressed a win.");
                    pacing.RecordInterstitialAttempt(0); pacing.RecordInterstitialClosed(0);
                    Require(pacing.WinsSinceInterstitial == 0, "Video opportunity did not reset the win counter.");
                    Require(pacing.InterstitialBlock(0, Win(), false, true) == "not_enough_wins", "Video repeated without another win.");
                    pacing.RecordWin();
                }
                Require(pacing.InterstitialsThisSession == 9, "More than six result videos must be allowed without a session cap.");
                pacing.RecordRewardedActivity(0);
                Require(pacing.RewardedBlock(9, false) == "rewarded_cooldown", "Tool-ad cooldown unexpectedly changed.");
                Require(pacing.RewardedBlock(10, false) == null, "Tool-ad cooldown did not expire.");
                config.adsEnabled = false;
                Require(ReadyPolicy(config).InterstitialBlock(999, Win(), false, true) == "disabled_or_missing_placement", "Master switch ignored.");
                config.adsEnabled = true; config.rewardedAdUnitId = " ";
                Require(ReadyPolicy(config).InterstitialBlock(999, Win(), false, true) == "disabled_or_missing_placement", "Blank placement accepted.");
                Directory.CreateDirectory("Logs/Ads");
                File.WriteAllText("Logs/Ads/policy-checks.txt", "PASS: " + checks + " policy checks: every victory from level 3 including a fresh session at level 3; levels 1/2 protected; no result cooldown/session cap; readiness, single-flight, placement switches; tool-ad cooldown retained.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void ProductionPolicyChecks()
        {
            var config = Resources.Load<HueAdPolicy>("HueRelay/Data/AdPolicy");
            Require(config && config.winsBetweenInterstitials == 1 && config.protectedCampaignLevels == 2 && config.FirstInterstitialDelay == 0
                && config.InterstitialInterval == 0 && config.rewardedGraceSeconds == 0,
                "Production result videos must begin on level 3 and run every win without a time cooldown.");
            Require(string.IsNullOrWhiteSpace(config.interstitialAdUnitId) && config.RewardedConfigured,
                "Only the rewarded video placement may be configured.");
        }

        private sealed class Backend : IHueAdBackend
        {
            public event Action<bool> VisibilityChanged;
            public readonly bool[] ready = new bool[2];
            public int preloads, shows;
            public bool throwOnShow;
            public bool autoLoad = true;
            public Action onRelease;
            public HueAdFormat format;
            public Action<HueAdOutcome> complete;
            public bool Supports(HueAdFormat value) => value == HueAdFormat.Rewarded;
            public bool IsReady(HueAdFormat value) => ready[(int)value];
            public void Preload(HueAdFormat value)
            {
                Require(value == HueAdFormat.Rewarded, "A non-rewarded format was preloaded.");
                preloads++; if (autoLoad) ready[(int)value] = true;
            }
            public void Show(HueAdFormat value, Action<HueAdOutcome> callback)
            {
                Require(value == HueAdFormat.Rewarded, "A non-rewarded format was shown.");
                shows++; format = value; complete = callback;
                if (throwOnShow) throw new InvalidOperationException("simulated show error");
            }
            public void Release(HueAdFormat value) { ready[(int)value] = false; onRelease?.Invoke(); }
            public void SetVisible(bool value) { VisibilityChanged?.Invoke(value); }
            public void Dispose() { VisibilityChanged = null; }
        }

        private static void Inventory(HueGame game)
        {
            PlayerPrefs.SetString(InventoryKey, "{\"version\":1,\"hints\":0,\"erasers\":0,\"extraMoves\":0}");
            typeof(HueGame).GetField("inventory", Private).SetValue(game, new HuePowerUpInventory(game.PowerUpSettings, InventoryKey));
        }
        private static void Board(HueGame game, int level = 3)
        {
            game.StartLevel(level);
            float time = (float)typeof(HueGame).GetProperty("GameTime", Private).GetValue(game);
            typeof(HueGame).GetField("built", Private).SetValue(game, time - 10);
            typeof(HueGame).GetField("now", Private).SetValue(game, time);
        }
        private static void Result(HueGame game, bool won, int level = 3)
        {
            Board(game, level);
            typeof(HueGame).GetProperty("Over").SetValue(game, true);
            typeof(HueGame).GetProperty("Won").SetValue(game, won);
            typeof(HueGame).GetField("sheetVisible", Private).SetValue(game, false);
            typeof(HueGame).GetMethod("ShowSheet", Private).Invoke(game, null);
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
                Require(backend.preloads == 1 && backend.shows == 0, "Only one rewarded video must be preloaded without showing.");
                game.Hint(); Require(game.IsPowerUpRefillOpen, "Empty Hint did not open opt-in UI.");
                game.WatchPowerUpAd(); game.WatchPowerUpAd();
                Require(backend.shows == 1 && backend.format == HueAdFormat.Rewarded && game.HintsRemaining == 0 && ads.IsBusy,
                    "Rewarded ad was not opt-in/single-flight or rewarded early.");
                Require(AudioListener.pause, "Game sound was not paused for the ad.");
                var complete = backend.complete; complete(HueAdOutcome.Completed); complete(HueAdOutcome.Completed);
                Require(game.HintsRemaining == 1 && !game.IsPowerUpRefillOpen && !ads.IsBusy && AudioListener.pause == oldAudio,
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
                    Require((kind == HuePowerUpKind.Eraser ? game.ErasersRemaining : game.ExtraMovesRemaining) == 1
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
                Require(backend.preloads == 1 && backend.shows == 0, "Returning to foreground auto-displayed an ad.");

                time = 0; backend = new Backend { autoLoad = false }; ads.Initialize(game, config, backend, () => time); ads.Tick();
                time = config.LoadTimeout + 1; ads.Tick();
                Require(backend.preloads == 1 && backend.shows == 0, "Stalled preload retried without backoff.");
                time += config.RetryDelay + 1; ads.Tick();
                Require(backend.preloads == 2 && backend.shows == 0, "Stalled preload did not recover after backoff.");

                config.adsEnabled = false; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick();
                int continued = 0; ads.TryInterstitial(Win(), () => continued++);
                Require(backend.preloads == 0 && backend.shows == 0 && continued == 1, "Disabled policy called the backend or blocked navigation.");
                config.adsEnabled = true;

                // Levels 1/2 are protected. Level 3 and every later win qualify.
                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 30;
                ads.RecordWin(); Result(game, true, 0);
                Require(!game.IsAdBreakActive && game.ModalVisible && backend.shows == 0, "First win must not show an interstitial.");
                game.ContinueResult();
                ads.RecordWin(); Result(game, true, 1);
                Require(!game.IsAdBreakActive && game.ModalVisible && backend.shows == 0, "Second win must not show a result video.");
                game.ContinueResult();
                ads.RecordWin(); Result(game, true, 2);
                game.ContinueResult(); game.ContinueResult(); game.ReturnHome();
                Require(game.IsAdBreakActive && ads.IsBusy && backend.shows == 1 && backend.format == HueAdFormat.Rewarded
                    && game.LevelIndex == 2 && !game.ModalVisible,
                    "Winning result must use a rewarded video at the original boundary exactly once.");
                complete = backend.complete; complete(HueAdOutcome.Completed); complete(HueAdOutcome.Completed);
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.ModalVisible && game.LevelIndex == 2 && ads.Pacing.InterstitialsThisSession == 1,
                    "Interstitial close did not reveal/count exactly once.");
                Require(game.HintsRemaining == 0 && game.ErasersRemaining == 0 && game.ExtraMovesRemaining == 0 && game.Score == 0,
                    "The replacement result video must grant no reward or score bonus.");
                game.ContinueResult();
                Require(game.LevelIndex == 3 && backend.shows == 1, "Next Level requested another ad.");
                Result(game, false); int shown = backend.shows; game.ContinueResult();
                Require(backend.shows == shown && !game.Over && !game.IsAdBreakActive, "Failure Retry showed an ad.");

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, false);
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.ModalVisible && backend.shows == 0,
                    "Failure must reveal its panel without an interstitial, even after previous wins.");
                game.ContinueResult(); Require(backend.shows == 0 && !game.Over,"Retry requested another ad.");

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); backend.ready[0] = false; Result(game, true); game.ContinueResult();
                Require(backend.shows == 0 && game.LevelIndex == 4 && !game.IsAdBreakActive, "Unloaded interstitial blocked navigation.");

                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true); game.ContinueResult();
                complete = backend.complete; time += config.PlaybackTimeout + 1; ads.Tick(); complete(HueAdOutcome.Completed);
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.ModalVisible && game.LevelIndex == 3 && ads.Pacing.InterstitialsThisSession == 0
                    && AudioListener.pause == oldAudio, "Timeout/late close locked, counted or revealed twice.");

                time = 0; backend = new Backend { throwOnShow = true }; ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true); game.ContinueResult();
                Require(!game.IsAdBreakActive && !ads.IsBusy && game.LevelIndex == 4, "Show exception blocked result navigation.");
                Require(new HuePowerUpInventory(game.PowerUpSettings, InventoryKey).Count(HuePowerUpKind.Hint) == 0,
                    "Non-rewarded ad outcome changed inventory.");
                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true);
                complete = backend.complete; complete(HueAdOutcome.Cancelled); complete(HueAdOutcome.Completed);
                Require(game.ModalVisible && !game.IsAdBreakActive && !ads.IsBusy && ads.Pacing.InterstitialsThisSession == 1
                    && game.HintsRemaining == 0 && game.ErasersRemaining == 0 && game.ExtraMovesRemaining == 0,
                    "Closing the result video early must resume the result once without granting a reward.");
                // No simulated time passes across 27 levels: videos on 3, 4, ... 27.
                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick();
                ads.Pacing.RecordRewardedActivity(0);
                for (int victory = 1; victory <= 27; victory++)
                {
                    ads.Tick(); ads.RecordWin(); Result(game, true, victory - 1);
                    Require(backend.shows == Mathf.Max(0, victory - 2), "Result video did not follow the level-3/every-win cadence at time zero.");
                    if (victory >= 3)
                    {
                        Require(game.IsAdBreakActive && !game.ModalVisible, "Eligible victory video did not precede the result.");
                        backend.complete(HueAdOutcome.Completed);
                    }
                    Require(game.ModalVisible && !ads.IsBusy && !game.IsAdBreakActive, "Result did not recover after the victory video.");
                    game.ContinueResult();
                }
                Require(ads.Pacing.InterstitialsThisSession == 25 && game.HintsRemaining == 0 && game.ExtraMovesRemaining == 0,
                    "A session cap or unexpected reward affected every-win videos.");
                ReviveChecks(game, ads, config);
                VictoryTimingChecks(game, config);
                AudioRecoveryChecks(game, ads, config);
                Directory.CreateDirectory("Logs/Ads");
                File.WriteAllText("Logs/Ads/runtime-checks.txt", "PASS: " + checks + " checks: preload without showing; explicit opt-in; no early grant; exactly-once +1 persisted reward; cancellation/stale callbacks; no-fill; rewarded-only preload/show; every-win pre-result video from level 3 without rewards or time cooldown; no automatic failure/Retry ads; opt-in revive +3, duplicate/cancel/no-fill/timeout/stale callback protection; half-second post-jet victory delay; skip unloaded; timeout/late-close recovery; show exceptions; audio recovery across all outcomes and both OnShow/OnClose orders; foreground gating; disabled sound/vibration preferences preserved. Player inventory and tutorial flag restored.\n");
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

        private static void AudioRecoveryChecks(HueGame game, HueAds ads, HueAdPolicy config)
        {
            var audio = UnityEngine.Object.FindObjectOfType<HueAudio>();
            var canOutput = typeof(HueAudio).GetProperty("CanOutput", Private);
            var focus = typeof(HueAudio).GetField("focused", Private);
            var paused = typeof(HueAudio).GetField("applicationPaused", Private);
            bool oldFocus = (bool)focus.GetValue(audio), oldPaused = (bool)paused.GetValue(audio);
            bool sound = HueAudio.SoundEnabled, vibration = HueAudio.VibrationEnabled, listenerPaused = AudioListener.pause;
            int soundPref = PlayerPrefs.GetInt("cc_sound", 1), vibrationPref = PlayerPrefs.GetInt("cc_vibration", 1);
            try
            {
                focus.SetValue(audio, true); paused.SetValue(audio, false);
                foreach (var outcome in new[] { HueAdOutcome.Completed, HueAdOutcome.Cancelled,
                    HueAdOutcome.Failed, HueAdOutcome.Unavailable, HueAdOutcome.TimedOut })
                foreach (bool showBeforeClose in new[] { false, true })
                {
                    double time = 0;
                    var backend = new Backend(); ads.Initialize(game, config, backend, () => time);
                    AudioListener.pause = false;
                    HueAudio.SoundEnabled = false; HueAudio.VibrationEnabled = false;
                    ads.Tick(); ads.RecordWin(); ads.RecordWin(); ads.RecordWin();
                    Result(game, true);
                    Require(ads.IsBusy && !(bool)canOutput.GetValue(audio), "Feedback remained active during a video.");
                    backend.SetVisible(false);
                    if (showBeforeClose) backend.SetVisible(true);
                    Require(!(bool)canOutput.GetValue(audio), "OnShow re-enabled feedback before the video closed.");
                    // Model synchronous host cleanup during ad destruction.
                    backend.onRelease = () => AudioListener.pause = true;
                    var close = backend.complete;
                    if (outcome == HueAdOutcome.TimedOut) { time += config.PlaybackTimeout + 1; ads.Tick(); }
                    else close(outcome);
                    backend.onRelease = null;
                    Require(!ads.IsBusy && !AudioListener.pause, "Ad exit left the listener paused: " + outcome);
                    Require((bool)canOutput.GetValue(audio) == showBeforeClose,
                        "Audio recovery ignored host visibility: " + outcome);
                    backend.SetVisible(true);
                    close(HueAdOutcome.Completed); // stale success must not change sound or rewards.
                    Require((bool)canOutput.GetValue(audio) && !HueAudio.SoundEnabled && !HueAudio.VibrationEnabled,
                        "Ad recovery failed or overwrote disabled settings.");
                    HueAudio.SoundEnabled = true; HueAudio.VibrationEnabled = true;
                    Require((bool)canOutput.GetValue(audio), "Feedback did not resume with enabled settings.");
                    game.ContinueResult();
                }
                typeof(HueAudio).GetMethod("OnApplicationPause", Private).Invoke(audio, new object[] { true });
                Require(!(bool)canOutput.GetValue(audio), "Paused app allowed feedback.");
                typeof(HueAudio).GetMethod("OnApplicationFocus", Private).Invoke(audio, new object[] { false });
                typeof(HueAudio).GetMethod("OnApplicationPause", Private).Invoke(audio, new object[] { false });
                Require(!(bool)canOutput.GetValue(audio), "Pause exit ignored missing focus.");
                typeof(HueAudio).GetMethod("OnApplicationFocus", Private).Invoke(audio, new object[] { true });
                Require((bool)canOutput.GetValue(audio), "Foreground return did not restore feedback.");
                double pausedTime = 0;
                var pausedBackend = new Backend(); ads.Initialize(game, config, pausedBackend, () => pausedTime);
                AudioListener.pause = true;
                ads.Tick(); ads.RecordWin(); ads.RecordWin(); ads.RecordWin(); Result(game, true);
                pausedBackend.complete(HueAdOutcome.Cancelled);
                Require(AudioListener.pause, "Ad recovery overrode another owner's existing audio pause.");
                game.ContinueResult();
                Require(PlayerPrefs.GetInt("cc_sound", 1) == soundPref && PlayerPrefs.GetInt("cc_vibration", 1) == vibrationPref,
                    "Audio recovery changed the player's saved preferences.");
            }
            finally
            {
                focus.SetValue(audio, oldFocus); paused.SetValue(audio, oldPaused);
                HueAudio.SoundEnabled = sound; HueAudio.VibrationEnabled = vibration;
                HueAudio.SetAdPlayback(false); HueAudio.SetHostVisible(true);
                AudioListener.pause = listenerPaused;
            }
        }

        private static void ReviveFailure(HueGame game)
        {
            Result(game, false);
            typeof(HueGame).GetProperty("Mask").SetValue(game, HueEngine.Chain(game.P, game.Mask, game.P.L.solution[0]));
            typeof(HueGame).GetProperty("Taps").SetValue(game, game.Limit);
            typeof(HueGame).GetProperty("Score").SetValue(game, 240);
            typeof(HueGame).GetMethod("Render", Private).Invoke(game, null);
        }

        private static void ReviveChecks(HueGame game, HueAds ads, HueAdPolicy config)
        {
            double time = 0;
            var backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
            Inventory(game); ReviveFailure(game);
            int mask = game.Mask, taps = game.Taps, limit = game.Limit, level = game.LevelIndex;
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            Require(view.resultRevive && view.resultRevive.gameObject.activeSelf, "Failure is missing its revive button.");
            Require(view.resultPrimary.GetComponent<UnityEngine.UI.Image>().color == view.resultSecondary.GetComponent<UnityEngine.UI.Image>().color,
                "Retry must use the Home background.");
            view.resultRevive.onClick.Invoke(); game.WatchReviveAd();
            Require(backend.shows == 1 && backend.format == HueAdFormat.Rewarded && game.IsReviveAdPending
                && game.Over && game.ModalVisible && game.Limit == limit, "Revive was not opt-in, single-flight or rewarded early.");
            game.Retry(); game.ContinueResult(); game.ReturnHome();
            Require(game.IsReviveAdPending && game.Mask == mask && game.LevelIndex == level,
                "Navigation changed the board during a revive ad.");
            var complete = backend.complete; complete(HueAdOutcome.Completed); complete(HueAdOutcome.Completed);
            Require(!game.IsReviveAdPending && !game.Over && !game.ModalVisible && game.Limit == limit + 3
                && game.Taps == taps && game.Mask == mask && game.Score == 240 && game.LevelIndex == level,
                "Completed revive must add exactly three moves and preserve the current board/score.");
            Require(game.HintsRemaining == 0 && game.ErasersRemaining == 0 && game.ExtraMovesRemaining == 0,
                "Revive must not add inventory tools.");
            game.Retry();
            Require(game.Limit == limit && game.Taps == 0 && game.Mask == game.P.Full, "Retry retained revive moves.");

            foreach (var outcome in new[] { HueAdOutcome.Cancelled, HueAdOutcome.Failed, HueAdOutcome.TimedOut })
            {
                time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
                ReviveFailure(game); game.WatchReviveAd(); complete = backend.complete;
                if (outcome == HueAdOutcome.TimedOut) { time += config.PlaybackTimeout + 1; ads.Tick(); }
                else complete(outcome);
                complete(HueAdOutcome.Completed);
                Require(game.Over && game.ModalVisible && !game.IsReviveAdPending && game.Limit == limit && game.Mask == mask,
                    "Cancelled/failed/timed-out revive or late success changed the board: " + outcome);
                Require(view.resultPrimary.interactable && view.resultSecondary.interactable && view.resultRevive.interactable,
                    "Unsuccessful revive left result buttons locked.");
            }
            time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
            ReviveFailure(game); backend.ready[0] = false; game.WatchReviveAd();
            Require(backend.shows == 0 && !game.IsReviveAdPending && game.Over && game.Limit == limit, "No-fill revive blocked the UI or granted moves.");

            time = 0; backend = new Backend(); ads.Initialize(game, config, backend, () => time); ads.Tick(); time = 120;
            ReviveFailure(game); game.WatchReviveAd(); complete = backend.complete;
            game.ResetGame(); complete(HueAdOutcome.Completed);
            Require(!game.Over && !game.ModalVisible && game.Limit == limit && game.Mask == game.P.Full,
                "A stale revive callback changed a restarted board.");
        }

        private static void VictoryTimingChecks(HueGame game, HueAdPolicy config)
        {
            bool enabled = config.adsEnabled; config.adsEnabled = false;
            try
            {
                Board(game);
                typeof(HueGame).GetProperty("Mask").SetValue(game, 0);
                typeof(HueGame).GetProperty("Taps").SetValue(game, game.Limit);
                float start = (float)typeof(HueGame).GetField("now", Private).GetValue(game);
                typeof(HueGame).GetMethod("Finish", Private).Invoke(game, new object[] { true });
                var now = typeof(HueGame).GetField("now", Private);
                var show = typeof(HueGame).GetMethod("ShowVictorySheet", Private);
                now.SetValue(game, start + .8f); show.Invoke(game, null);
                Require(!game.ModalVisible, "Victory panel appeared before the side jets.");
                now.SetValue(game, start + 1.3f);
                typeof(HueGame).GetMethod("ConfettiFire", Private).Invoke(game, new object[] { -1, 1 });
                now.SetValue(game, start + 1.79f); show.Invoke(game, null);
                Require(!game.ModalVisible, "Victory panel did not wait half a second after the actual jet.");
                now.SetValue(game, start + 1.81f); show.Invoke(game, null);
                Require(game.ModalVisible && game.Won, "Victory panel did not appear after its celebration delay.");
            }
            finally { config.adsEnabled = enabled; game.ResetGame(); }
        }
    }
}
