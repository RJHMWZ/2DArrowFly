using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HueRetentionChecks
    {
        private const string Key = "cc_retention_checks_temporary";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Platform : IHueRetentionPlatform
        {
            public event Action Resumed;
            public bool available = true, rewards = true;
            public readonly List<Action<HueRetentionResult>>[] queries = {
                new List<Action<HueRetentionResult>>(), new List<Action<HueRetentionResult>>() };
            public Action<HueRetentionResult> opened;
            public int opens;
            public bool Supports(HueRetentionKind kind, bool reward) => reward ? rewards : available;
            public void Open(HueRetentionKind kind, Action<HueRetentionResult> callback) { opens++; opened = callback; }
            public void CheckReward(HueRetentionKind kind, Action<HueRetentionResult> callback) { queries[(int)kind].Add(callback); }
            public void Resume() { Resumed?.Invoke(); }
            public void Dispose() { Resumed = null; }
            public Action<HueRetentionResult> Query(HueRetentionKind kind) => queries[(int)kind][queries[(int)kind].Count - 1];
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        private static void RequireCovered(Button button, Transform overlay)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(overlay),
                button.name + " still receives pointer input above the modal overlay.");
        }
        private static void ClickAt(Vector2 point)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "Guide click did not hit any UI.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [MenuItem("Tools/Hue Relay/Retention/Run runtime checks")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run these checks in Play mode.");
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var field = typeof(HueGame).GetField("inventory", Private);
            var original = field.GetValue(game);
            var config = game.PowerUpSettings;
            var temporary = new GameObject("RetentionChecks");
            var retention = temporary.AddComponent<HueRetention>(); retention.game = game;
            bool hadKey = PlayerPrefs.HasKey(Key);
            string previous = PlayerPrefs.GetString(Key, "");
            try
            {
                PlayerPrefs.DeleteKey(Key);
                var inventory = new HuePowerUpInventory(config, Key); field.SetValue(game, inventory);
                int baseline = game.ExtraMovesRemaining;
                int hintsBefore = game.HintsRemaining, erasersBefore = game.ErasersRemaining;
                var platform = new Platform(); retention.Initialize(platform);
                retention.Activate(HueRetentionKind.Profile);
                retention.Activate(HueRetentionKind.Profile);
                Require(platform.opens == 1, "Repeated taps opened two platform flows.");
                platform.opened(HueRetentionResult.Completed);
                Require(game.ExtraMovesRemaining == baseline, "Navigation success granted a reward without eligibility.");
                platform.Query(HueRetentionKind.Profile)(HueRetentionResult.Ineligible);
                Require(game.ExtraMovesRemaining == baseline, "Ineligible response granted Move tools.");
                retention.Refresh();
                platform.Query(HueRetentionKind.Profile)(HueRetentionResult.Failed);
                Require(game.ExtraMovesRemaining == baseline, "Failed qualification request granted Move tools.");
                retention.Refresh();
                var profileCallback = platform.Query(HueRetentionKind.Profile);
                profileCallback(HueRetentionResult.Eligible); profileCallback(HueRetentionResult.Eligible);
                Require(game.ExtraMovesRemaining == baseline + 1, "Profile reward missing or duplicated.");
                platform.Query(HueRetentionKind.Shortcut)(HueRetentionResult.Ineligible);
                retention.Activate(HueRetentionKind.Shortcut);
                platform.opened(HueRetentionResult.Completed);
                Require(game.ExtraMovesRemaining == baseline + 1, "Adding a shortcut granted Move tools without eligibility.");
                var shortcutCallback = platform.Query(HueRetentionKind.Shortcut);
                shortcutCallback(HueRetentionResult.Eligible); shortcutCallback(HueRetentionResult.Eligible);
                Require(game.ExtraMovesRemaining == baseline + 3, "Shortcut reward missing or duplicated.");
                Require(game.HintsRemaining == hintsBefore && game.ErasersRemaining == erasersBefore,
                    "Retention reward changed Hint or Eraser inventory.");
                var reloaded = new HuePowerUpInventory(config, Key);
                Require(reloaded.Count(HuePowerUpKind.ExtraMoves) == baseline + 3
                    && reloaded.HasClaimedRetentionReward(HueRetentionKind.Profile)
                    && reloaded.HasClaimedRetentionReward(HueRetentionKind.Shortcut), "Atomic reward save did not survive reloading.");
                Require(!reloaded.TryClaimRetentionReward(HueRetentionKind.Profile)
                    && !reloaded.TryClaimRetentionReward(HueRetentionKind.Shortcut), "Reloading permitted repeated rewards.");
                int queries = platform.queries[0].Count + platform.queries[1].Count;
                retention.Refresh();
                Require(platform.queries[0].Count + platform.queries[1].Count == queries, "Claimed rewards were queried again.");
                retention.Activate(HueRetentionKind.Shortcut);
                Require(platform.opens == 3, "A claimed shortcut cannot be added again.");

                // Legacy saves gain empty receipts without resetting the existing balance.
                PlayerPrefs.SetString(Key, "{\"version\":1,\"hints\":7,\"erasers\":5,\"extraMoves\":4}");
                inventory = new HuePowerUpInventory(config, Key); field.SetValue(game, inventory);
                Require(game.HintsRemaining == 7 && game.ErasersRemaining == 5 && game.ExtraMovesRemaining == 4,
                    "Legacy inventory migration reset balances.");
                var unavailable = new Platform { available = false, rewards = false };
                retention.Initialize(unavailable); retention.Activate(HueRetentionKind.Profile);
                Require(!retention.State(HueRetentionKind.Profile).opening && unavailable.opens == 0 && game.HintsRemaining == 7,
                    "Unsupported platforms opened an API or granted a reward.");

                // Reward checks still work when navigation is unavailable (e.g. a cold shortcut launch).
                var resumed = new Platform { available = false }; retention.Initialize(resumed);
                var oldQuery = resumed.Query(HueRetentionKind.Profile);
                resumed.Resume(); typeof(HueRetention).GetMethod("Update", Private).Invoke(retention, null);
                oldQuery(HueRetentionResult.Eligible);
                Require(game.HintsRemaining == 7 && game.ExtraMovesRemaining == 4, "An obsolete pre-resume callback granted a reward.");
                resumed.Query(HueRetentionKind.Profile)(HueRetentionResult.Eligible);
                Require(game.HintsRemaining == 7 && game.ExtraMovesRemaining == 5, "A resumed eligible launch failed to grant its Move reward.");

                // A missing callback must time out without locking subsequent actions.
                var timeout = new Platform(); retention.Initialize(timeout);
                retention.Activate(HueRetentionKind.Shortcut);
                var lateOpen = timeout.opened;
                typeof(HueRetention.EntryState).GetField("openDeadline", Private).SetValue(retention.State(HueRetentionKind.Shortcut), -1f);
                typeof(HueRetention).GetMethod("Update", Private).Invoke(retention, null);
                Require(!retention.State(HueRetentionKind.Shortcut).opening, "Missing callback locked the entry.");
                lateOpen(HueRetentionResult.Completed);
                Require(!retention.State(HueRetentionKind.Shortcut).opening && game.HintsRemaining == 7 && game.ExtraMovesRemaining == 5,
                    "A late navigation callback changed the reward balance.");

                // Use an unclaimed UI fixture after the receipt/legacy checks above.
                PlayerPrefs.SetString(Key, "{\"version\":1,\"hints\":7,\"erasers\":5,\"extraMoves\":5}");
                field.SetValue(game, new HuePowerUpInventory(config, Key));
                var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
                var ui = menu.GetComponent<HueRetentionMenu>();
                bool visible = menu.IsVisible;
                menu.Show(); ui.profileButton.onClick.Invoke();
                Require(ui.IsOpen && ui.closeButton.interactable, "Profile guide did not open or cannot be dismissed.");
                Require(ui.thirdStep.text == "Claim 1 Move tool" && ui.rewardLabel.text == "+1"
                    && ui.rewardIcon.sprite == menu.view.extraMovesButton.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite,
                    "Profile reward copy/icon did not use Move tools.");
                menu.Play(); Require(menu.IsVisible, "Play bypassed the guide overlay.");
                ui.closeButton.onClick.Invoke(); Require(!ui.IsOpen, "Close did not dismiss the guide.");
                ui.shortcutButton.onClick.Invoke(); Require(ui.IsOpen, "Shortcut guide did not open.");
                Require(ui.thirdStep.text == "Claim 2 Move tools" && ui.rewardLabel.text == "+2",
                    "Shortcut reward copy did not use Move tools.");
                ui.laterButton.onClick.Invoke(); Require(!ui.IsOpen, "Maybe later did not dismiss the guide.");
                ui.OpenProfile(); menu.OpenSettings();
                Require(!ui.IsOpen && menu.settingsOverlay.activeSelf, "Settings overlapped the retention guide.");
                menu.CloseSettings(); if (!visible) menu.HideForGameplay();

                Directory.CreateDirectory("Logs/Retention");
                File.WriteAllText("Logs/Retention/runtime-checks.txt", "PASS: canIUse fallback; no grant on navigation/add success, ineligible or obsolete callbacks; +1/+2 eligible grants; duplicate callback and restart deduplication; atomic receipts/balance; legacy saves; cold launch/resume; claimed shortcut can be re-added; timeout; guide buttons, dismissal and menu/settings event blocking.\n");
                Debug.Log("PASS: retention runtime checks.");
            }
            finally
            {
                field.SetValue(game, original);
                UnityEngine.Object.DestroyImmediate(temporary);
                if (hadKey) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
        }

        [MenuItem("Tools/Hue Relay/Retention/Run pointer checks")]
        public static void RunPointerChecks()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run these checks in Play mode.");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            menu.StartCoroutine(PointerChecks(menu));
        }

        private static IEnumerator PointerChecks(HueMainMenu menu)
        {
            bool visible = menu.IsVisible;
            var ui = menu.GetComponent<HueRetentionMenu>();
            try
            {
                menu.Show(); ui.OpenProfile();
                // Graphic.depth is assigned during a render. A same-call raycast
                // immediately after activating a canvas returns no hits in Unity.
                yield return null; yield return null;
                RequireCovered(menu.levelButton, ui.overlay.transform);
                RequireCovered(ui.shortcutButton, ui.overlay.transform);
                ClickAt(RectTransformUtility.WorldToScreenPoint(null, ui.card.TransformPoint(new Vector3(-140, 0, 0))));
                Require(ui.IsOpen, "A click inside the white card dismissed the guide.");
                ClickAt(new Vector2(Screen.width * .5f, Screen.safeArea.yMin + 8));
                Require(!ui.IsOpen, "A click on the outside backdrop did not dismiss the guide.");
                menu.OpenSettings();
                yield return null; yield return null;
                RequireCovered(ui.profileButton, menu.settingsOverlay.transform);
                Directory.CreateDirectory("Logs/Retention");
                File.WriteAllText("Logs/Retention/pointer-checks.txt", "PASS: rendered guide blocks Play and the other retention entry; card clicks stay open; outside backdrop dismisses; Settings blocks both retention entries.\n");
                Debug.Log("PASS: retention pointer checks.");
            }
            finally
            {
                ui.Close(); menu.CloseSettings();
                if (!visible) menu.HideForGameplay();
            }
        }
    }
}
