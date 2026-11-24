using System;
using System.Collections.Generic;
using UnityEngine;

namespace HueRelay
{
    /// <summary>Attempt-level funnel events. Background is a checkpoint, never a churn verdict.</summary>
    public sealed class HueTelemetry
    {
        [Serializable] private sealed class Attempt
        {
            public string id, session, mode, status;
            public int level, number, score, taps, movesLeft, arrowsLeft, revives;
            public float activeSeconds;
            public bool firstAction;
        }
        public static event Action<string, Dictionary<string, object>> Observed;
        private readonly string campaign, install, session = Guid.NewGuid().ToString("N"), storage;
        private readonly Queue<KeyValuePair<string, Dictionary<string, object>>> queue = new Queue<KeyValuePair<string, Dictionary<string, object>>>();
        private Attempt attempt;
        private bool visible = true, ended;
        private float checkpointTime, retryAt;

        public HueTelemetry(string campaignId, string storagePrefix = "cc_hue_analytics_v1")
        {
            campaign = campaignId ?? "campaign";
            storage = storagePrefix;
#if UNITY_EDITOR
            if (Application.isBatchMode) storage += "__validation";
#endif
            install = PlayerPrefs.GetString(storage + "_install", "");
            if (string.IsNullOrEmpty(install))
            {
                install = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(storage + "_install", install); PlayerPrefs.Save();
            }
            Track("session_start");
            var previous = PlayerPrefs.GetString(storage + "_checkpoint", "");
            if (!string.IsNullOrEmpty(previous))
            {
                try
                {
                    var saved = JsonUtility.FromJson<Attempt>(previous);
                    if (saved != null) Track("previous_session_checkpoint", "previous_level", saved.level,
                        "previous_attempt_id", saved.id, "previous_session_id", saved.session,
                        "previous_status", saved.status, "previous_active_sec", saved.activeSeconds);
                }
                catch (Exception e) { Debug.LogWarning("Analytics checkpoint ignored: " + e.Message); }
            }
        }

        public void Tick(bool playable, bool active, int level, bool endless, int score, int taps, int movesLeft, int arrowsLeft)
        {
            if (ended) return;
            if (playable && attempt == null)
            {
                string key = storage + "_attempts_" + campaign + "_" + (endless ? "endless" : level.ToString());
                int number = PlayerPrefs.GetInt(key, 0);
                if (number < int.MaxValue) number++;
                PlayerPrefs.SetInt(key, number);
                attempt = new Attempt { id = Guid.NewGuid().ToString("N"), session = session,
                    level = level, mode = endless ? "endless" : "campaign", number = number, status = "playing" };
                UpdateValues(score, taps, movesLeft, arrowsLeft);
                Track("level_start"); SaveCheckpoint();
            }
            if (attempt != null && playable)
            {
                UpdateValues(score, taps, movesLeft, arrowsLeft);
                if (active && visible) attempt.activeSeconds += Mathf.Clamp(Time.unscaledDeltaTime, 0, 1);
                checkpointTime += Time.unscaledDeltaTime;
                if (checkpointTime >= 10) SaveCheckpoint();
            }
            Flush();
        }

        public void UpdateValues(int score, int taps, int movesLeft, int arrowsLeft)
        {
            if (attempt == null) return;
            attempt.score = score; attempt.taps = taps; attempt.movesLeft = movesLeft; attempt.arrowsLeft = arrowsLeft;
        }
        public void FirstAction()
        {
            if (attempt == null || attempt.firstAction || attempt.status != "playing") return;
            attempt.firstAction = true; Track("level_first_action");
        }
        public void Finish(bool won)
        {
            if (attempt == null || attempt.status != "playing") return;
            attempt.status = won ? "won" : "failed";
            Track("level_end", "result", attempt.status); SaveCheckpoint();
        }
        public void Leave(string reason)
        {
            if (attempt == null) return;
            if (attempt.status == "playing")
            {
                Track("level_abandon", "reason", reason);
                attempt.status = "left_without_result";
            }
            Track("level_leave", "reason", reason); SaveCheckpoint();
            attempt = null;
        }
        public void Revive(int addedMoves)
        {
            if (attempt == null || attempt.status != "failed") return;
            attempt.status = "playing";
            attempt.revives++;
            Track("level_revive", "moves_added", addedMoves);
            SaveCheckpoint();
        }
        public void Visibility(bool value, bool adActive)
        {
            if (visible == value || ended) return;
            visible = value;
            Track(value ? "session_resume" : "session_background", "ad_in_progress", adActive ? 1 : 0);
            if (!value) SaveCheckpoint();
            Flush();
        }
        public void EndSession()
        {
            if (ended) return;
            Track("session_end"); SaveCheckpoint(); Flush(); ended = true;
        }
        private void SaveCheckpoint()
        {
            checkpointTime = 0;
            if (attempt == null) return;
            PlayerPrefs.SetString(storage + "_checkpoint", JsonUtility.ToJson(attempt)); PlayerPrefs.Save();
        }

        public void Track(string name, params object[] fields)
        {
            if (ended) return;
            var data = new Dictionary<string, object> {
                { "event_id", Guid.NewGuid().ToString("N") }, { "session_id", session }, { "install_id", install },
                { "campaign_id", campaign }, { "app_version", Application.version },
                { "timestamp_utc", DateTime.UtcNow.ToString("o") }, { "schema_version", 1 }
            };
            if (attempt != null)
            {
                data["level"] = attempt.level; data["mode"] = attempt.mode; data["attempt_id"] = attempt.id;
                data["attempt_no"] = attempt.number; data["status"] = attempt.status;
                data["revive_count"] = attempt.revives;
                data["score"] = attempt.score; data["taps"] = attempt.taps; data["moves_left"] = attempt.movesLeft;
                data["arrows_left"] = attempt.arrowsLeft; data["active_sec"] = Math.Round(attempt.activeSeconds, 2);
            }
            for (int i = 0; i + 1 < fields.Length; i += 2) data[(string)fields[i]] = fields[i + 1];
            try { Observed?.Invoke(name, new Dictionary<string, object>(data)); }
            catch (Exception e) { Debug.LogWarning("Analytics observer failed: " + e.Message); }
            if (queue.Count >= 256) queue.Dequeue();
            queue.Enqueue(new KeyValuePair<string, Dictionary<string, object>>(name, data));
        }

        private void Flush()
        {
            if (Time.realtimeSinceStartup < retryAt) return;
            while (queue.Count > 0)
            {
                var next = queue.Peek();
                try
                {
#if UNITY_EDITOR || UNITY_LUNA
                    Debug.Log("[HueAnalytics] " + next.Key + " " + Newtonsoft.Json.JsonConvert.SerializeObject(next.Value));
#else
                    IvySdk.Tracker.EventTracker.TrackEvent(next.Key, new Dictionary<string, object>(next.Value));
#endif
                    queue.Dequeue();
                }
                catch (Exception e)
                {
                    retryAt = Time.realtimeSinceStartup + 5;
                    Debug.LogWarning("Analytics report deferred: " + e.Message); return;
                }
            }
        }
    }
}
