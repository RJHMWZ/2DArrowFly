using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HueRelay.Editor
{
    [InitializeOnLoad]
    public static class HueFeatureChecks
    {
        private const string BatchKey = "HueFeatureChecks.pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double entered;
        static HueFeatureChecks()
        {
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.update += BatchTick;
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void BatchRun()
        {
            PlayerSettings.companyName = "HueRelayValidation";
            PlayerSettings.productName = "FeatureChecks";
            EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
            SessionState.SetBool(BatchKey, true);
            EditorApplication.update -= BatchTick;
            EditorApplication.update += BatchTick;
            EditorApplication.EnterPlaymode();
        }
        private static void BatchTick()
        {
            if (!EditorApplication.isPlaying) return;
            if (entered == 0) entered = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - entered < 1) return;
            EditorApplication.update -= BatchTick;
            SessionState.SetBool(BatchKey, false);
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [MenuItem("Tools/Hue Relay/Debug/Check new gameplay and analytics")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play mode.");
            HueAdChecks.PolicyChecks();
            HueAdChecks.RuntimeChecks();
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            Require(game.PowerUpSettings.initialHints == 0 && game.PowerUpSettings.initialErasers == 0
                && game.PowerUpSettings.initialExtraMoves == 0 && game.PowerUpSettings.rewardedAmount == 1,
                "Production power-up configuration is incorrect.");
            var tick = typeof(HueGame).GetMethod("TickIdleMoveGuide", Private);
            var guide = typeof(HueGame).GetField("idleMoveGuide", Private);
            var originalScale = view.extraMovesButton.transform.localScale;
            var originalRotation = view.extraMovesButton.transform.localRotation;
            for (int i = 0; i < 19; i++) tick.Invoke(game, new object[] { true, false, .25f });
            Require(!(bool)guide.GetValue(game), "Guide started before five seconds.");
            tick.Invoke(game, new object[] { true, false, .25f });
            tick.Invoke(game, new object[] { true, false, .25f });
            Require((bool)guide.GetValue(game) && view.extraMovesButton.transform.localScale.x > originalScale.x,
                "Move guide did not animate after five seconds.");
            tick.Invoke(game, new object[] { true, true, .01f });
            Require(!(bool)guide.GetValue(game) && view.extraMovesButton.transform.localScale == originalScale
                && view.extraMovesButton.transform.localRotation == originalRotation, "Input did not restore the button.");
            for (int i = 0; i < 30; i++) tick.Invoke(game, new object[] { false, false, .25f });
            Require(!(bool)guide.GetValue(game), "Unavailable gameplay accumulated idle time.");
            AnalyticsChecks();
            Directory.CreateDirectory("Logs/Features");
            File.WriteAllText("Logs/Features/checks.txt", "PASS: ad pacing and runtime integration; one-item rewards; win/failure ads before result; navigation has no ads; idle guide threshold/input restoration; analytics attempts, first action, result deduplication, background deduplication, retry/abandon, checkpoint recovery.\n");
        }
        private static void AnalyticsChecks()
        {
            string id = Guid.NewGuid().ToString("N"), prefix = "cc_feature_" + id;
            string storedPrefix = prefix + (Application.isBatchMode ? "__validation" : "");
            var events = new List<KeyValuePair<string, Dictionary<string, object>>>();
            Action<string, Dictionary<string, object>> observer = (name, data) =>
            {
                if ((string)data["campaign_id"] == id) events.Add(new KeyValuePair<string, Dictionary<string, object>>(name, data));
            };
            HueTelemetry.Observed += observer;
            try
            {
                var t = new HueTelemetry(id, prefix);
                t.Tick(true, true, 4, false, 0, 0, 8, 12);
                t.FirstAction(); t.FirstAction();
                t.Visibility(false, true); t.Visibility(false, true); t.Visibility(true, false);
                t.UpdateValues(120, 8, 0, 2); t.Finish(false); t.Finish(false); t.Leave("retry"); t.Leave("retry");
                t.Tick(true, true, 4, false, 0, 0, 8, 12); t.Leave("home"); t.EndSession(); t.EndSession();
                Require(events.Count(e => e.Key == "level_start") == 2 && events.Count(e => e.Key == "level_first_action") == 1,
                    "Attempts or first actions duplicated.");
                Require(events.Count(e => e.Key == "level_end") == 1 && events.Count(e => e.Key == "level_abandon") == 1
                    && events.Count(e => e.Key == "level_leave") == 2, "Result/abandon attribution is incorrect.");
                Require(events.Count(e => e.Key == "session_background") == 1 && events.Count(e => e.Key == "session_end") == 1,
                    "Lifecycle events duplicated.");
                var starts = events.Where(e => e.Key == "level_start").Select(e => e.Value).ToArray();
                Require((string)starts[0]["attempt_id"] != (string)starts[1]["attempt_id"] && (int)starts[1]["attempt_no"] == 2,
                    "Retry reused its attempt identity.");
                Require(events.All(e => e.Value.ContainsKey("event_id") && e.Value.ContainsKey("install_id")), "Missing event identity.");
                new HueTelemetry(id, prefix).EndSession();
                Require(events.Any(e => e.Key == "previous_session_checkpoint" && (int)e.Value["previous_level"] == 4),
                    "Previous-session level checkpoint was lost.");
            }
            finally
            {
                HueTelemetry.Observed -= observer;
                foreach (var suffix in new[] { "_install", "_checkpoint", "_attempts_" + id + "_4" }) PlayerPrefs.DeleteKey(storedPrefix + suffix);
                PlayerPrefs.Save();
            }
        }
    }
}
