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
            HueUndoChecks.Run();
            MoveCountFeedbackChecks();
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            Require(game.PowerUpSettings.initialHints == 0 && game.PowerUpSettings.initialErasers == 0
                && game.PowerUpSettings.initialExtraMoves == 0 && game.PowerUpSettings.rewardedAmount == 1,
                "Production power-up configuration is incorrect.");
            var tick = typeof(HueGame).GetMethod("TickIdleMoveGuide", Private);
            var guide = typeof(HueGame).GetField("idleMoveGuide", Private);
            var originalScale = view.extraMovesButton.transform.localScale;
            var originalRotation = view.extraMovesButton.transform.localRotation;
            var moveRect = (RectTransform)view.extraMovesButton.transform;
            Require(moveRect.pivot == new Vector2(.5f, .5f), "Move button must rotate around its center.");
            Vector3 originalCenter = moveRect.TransformPoint(moveRect.rect.center);
            var hapticCycle = typeof(HueGame).GetField("moveGuideHapticCycle", Private);
            for (int i = 0; i < 19; i++) tick.Invoke(game, new object[] { true, false, .25f });
            Require(!(bool)guide.GetValue(game), "Guide started before five seconds.");
            tick.Invoke(game, new object[] { true, false, .25f });
            tick.Invoke(game, new object[] { true, false, .25f });
            Require((bool)guide.GetValue(game) && view.extraMovesButton.transform.localScale.x > originalScale.x,
                "Move guide did not animate after five seconds.");
            Require(Vector3.Distance(originalCenter, moveRect.TransformPoint(moveRect.rect.center)) < .001f,
                "Move animation displaced its center.");
            Require((int)hapticCycle.GetValue(game) == 0, "First shake did not schedule its haptic pulse.");
            var feedback = view.extraMovesButton.GetComponent<HueButtonFeedback>();
            if (feedback)
            {
                var animatedScale = moveRect.localScale;
                typeof(HueButtonFeedback).GetMethod("Update", Private).Invoke(feedback, null);
                Require(moveRect.localScale == animatedScale, "Press feedback overwrote the idle animation.");
            }
            tick.Invoke(game, new object[] { true, false, 1.6f });
            Require((int)hapticCycle.GetValue(game) == 1, "Repeated shake did not schedule its next haptic pulse.");
            tick.Invoke(game, new object[] { true, true, .01f });
            Require(!(bool)guide.GetValue(game) && view.extraMovesButton.transform.localScale == originalScale
                && view.extraMovesButton.transform.localRotation == originalRotation, "Input did not restore the button.");
            Require((int)hapticCycle.GetValue(game) == -1 && (!feedback || !feedback.ExternalAnimation),
                "Input did not release idle haptics/press feedback.");
            for (int i = 0; i < 30; i++) tick.Invoke(game, new object[] { false, false, .25f });
            Require(!(bool)guide.GetValue(game), "Unavailable gameplay accumulated idle time.");
            AnalyticsChecks();
            Directory.CreateDirectory("Logs/Features");
            File.WriteAllText("Logs/Features/checks.txt", "PASS: ad pacing and runtime integration; one-item rewards; step-by-step move feedback; rewarded-only formats; every-win videos from level 3 without rewards or time cooldown; sequential path-return undo and overlapping-chain accounting; no automatic failure/navigation ads; rewarded revive and victory delay; idle guide threshold/input restoration; analytics attempts, first action, result deduplication, background deduplication, retry/abandon, checkpoint recovery.\n");
        }
        private static void MoveCountFeedbackChecks()
        {
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            var inventory = typeof(HueGame).GetField("inventory", Private);
            var original = inventory.GetValue(game);
            const string key = "cc_move_count_feedback_checks";
            bool had = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key, "");
            var now = typeof(HueGame).GetField("now", Private);
            var render = typeof(HueGame).GetMethod("Render", Private);
            try
            {
                PlayerPrefs.SetString(key, "{\"version\":1,\"hints\":0,\"erasers\":0,\"extraMoves\":3}");
                inventory.SetValue(game, new HuePowerUpInventory(game.PowerUpSettings, key));
                game.StartLevel(3);
                float start = (float)now.GetValue(game);
                int initial = game.Limit;
                game.AddMoves();
                Require(game.Limit == initial + 3 && view.moves.text == "Left " + initial,
                    "Move tool must immediately grant its budget while starting the displayed count at the previous value.");
                for (int step = 1; step <= 3; step++)
                {
                    now.SetValue(game, start + step * .16f + .01f); render.Invoke(game, null);
                    Require(view.moves.text == "Left " + (initial + step), "Move count skipped its +" + step + " step.");
                }
                now.SetValue(game, start + .7f); render.Invoke(game, null);
                Require(Mathf.Abs(view.moves.rectTransform.localScale.x - 1) < .001f, "Move count pulse did not settle.");
                game.AddMoves(); game.AddMoves();
                Require(game.Limit == initial + 9 && game.ExtraMovesRemaining == 0, "Repeated tool uses lost or duplicated steps.");
                game.Retry();
                now.SetValue(game, start + 3); render.Invoke(game, null);
                Require(game.Limit == initial && view.moves.text == "Left " + initial, "Retry retained a previous count animation.");
            }
            finally
            {
                game.ResetGame(); inventory.SetValue(game, original);
                if (had) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
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
                var revived = new HueTelemetry(id, prefix);
                revived.Tick(true, true, 4, false, 240, 3, 0, 2);
                revived.Finish(false);
                string revivedAttempt = (string)events.Last(e => e.Key == "level_end").Value["attempt_id"];
                revived.UpdateValues(240, 3, 3, 2); revived.Revive(3); revived.Revive(3);
                Require(events.Count(e => e.Key == "level_revive") == 1
                    && (string)events.Last(e => e.Key == "level_revive").Value["attempt_id"] == revivedAttempt,
                    "Revive must resume the same attempt exactly once.");
                revived.Finish(true); revived.Finish(true);
                var final = events.Last(e => e.Key == "level_end").Value;
                Require((string)final["result"] == "won" && (int)final["revive_count"] == 1,
                    "A revived attempt did not record its eventual win.");
                revived.EndSession();
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
