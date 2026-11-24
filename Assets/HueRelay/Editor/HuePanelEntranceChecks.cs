using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePanelEntranceChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        private static void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static object Animation(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Sample(object animation, float elapsed)
        {
            animation.GetType().GetField("started", Private).SetValue(animation, Time.unscaledTime - elapsed);
            animation.GetType().GetMethod("Apply").Invoke(animation, new object[] { Time.unscaledTime });
        }
        private static Vector2 Rest(object animation) => (Vector2)animation.GetType().GetProperty("RestPosition").GetValue(animation);
        private static void CheckStart(RectTransform card, Image backdrop, object animation, bool nested = false)
        {
            var group = card.GetComponent<CanvasGroup>();
            Require(group && group.alpha == 0 && !group.interactable, "Opening card flashed fully visible or accepted early taps.");
            Require(Mathf.Abs(backdrop.color.a - (nested ? HueModalBackdrop.Tint.a : 0)) < .001f
                && backdrop.raycastTarget && card.anchoredPosition.y < Rest(animation).y,
                "Opening must fade from below while the overlay continues to block underlying input.");
        }
        private static void CheckMotion(RectTransform card, Image backdrop, object animation, bool nested = false)
        {
            var group = card.GetComponent<CanvasGroup>();
            float startScale = card.localScale.x;
            Sample(animation, .1f);
            float alpha = group.alpha, position = card.anchoredPosition.y, shade = backdrop.color.a;
            Require(alpha > .4f && alpha < .9f && position < Rest(animation).y && card.localScale.x > startScale,
                "The card did not smoothly fade, rise and grow during the result-style entrance.");
            Sample(animation, .21f);
            Require(group.alpha > alpha && card.anchoredPosition.y > position
                && (nested ? Mathf.Abs(backdrop.color.a - shade) < .001f : backdrop.color.a > shade),
                "Card/backdrop entrance must progress together.");
            Sample(animation, .42f);
            Require(Mathf.Abs(group.alpha - 1) < .001f && group.interactable
                && Vector2.Distance(card.anchoredPosition, Rest(animation)) < .001f,
                "The card did not settle exactly into its authored layout.");
            Require(!((bool)animation.GetType().GetProperty("IsAnimating").GetValue(animation)),
                "Settled panels must stop their animation work.");
            Require(backdrop.color == HueModalBackdrop.Tint && !backdrop.materialForRendering.shader.name.Contains("Hue"),
                "Every settled modal must use the same grey-black tint and 55% opacity.");
        }

        public static void Run()
        {
            checks = 0;
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>(true);
            var inventory = typeof(HueGame).GetField("inventory", Private);
            object original = inventory.GetValue(game);
            const string key = "cc_panel_entrance_checks";
            bool had = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key, "");
            float timeScale = Time.timeScale;
            bool sound = HueAudio.SoundEnabled, vibration = HueAudio.VibrationEnabled;
            try
            {
                var fixture = new HuePowerUpInventory(game.PowerUpSettings, key);
                fixture.DebugSetCounts(0, 0, 0); inventory.SetValue(game, fixture);
                menu.HideForGameplay(); game.StartLevel(3);
                int mask = game.Mask;
                game.Hint();
                var panel = view.canvas.GetComponentInChildren<HuePowerUpRefillPanel>(true);
                var entrance = Animation(panel, "entrance");
                var backdrop = panel.GetComponent<Image>();
                CheckStart(panel.card, backdrop, entrance);
                Time.timeScale = 0;
                CheckMotion(panel.card, backdrop, entrance);
                panel.Layout();
                Vector2 settledPosition = panel.card.anchoredPosition; float settledScale = panel.card.localScale.x;
                for (int i = 0; i < 50; i++) panel.Layout();
                Require(panel.card.anchoredPosition == settledPosition && panel.card.localScale.x == settledScale,
                    "Refill layout accumulated animation offsets.");
                game.TapArrow(0); game.AddMoves();
                Require(game.Mask == mask && game.Taps == 0 && game.IsPowerUpRefillOpen,
                    "Animated refill failed to block the board.");
                game.ClosePowerUpRefill(); game.Hint(); CheckStart(panel.card, backdrop, entrance);
                Sample(entrance, .1f); float faded = panel.card.GetComponent<CanvasGroup>().alpha;
                panel.SetPending(true); panel.ShowUnavailable();
                Require(panel.card.GetComponent<CanvasGroup>().alpha == faded && panel.watchButton.interactable,
                    "Ad status refresh restarted the entrance or locked the panel.");
                game.ClosePowerUpRefill();
                Require(!panel.gameObject.activeSelf && panel.card.GetComponent<CanvasGroup>().alpha == 1,
                    "Closing midway did not clear the refill animation.");

                menu.Show(); menu.OpenSettings();
                var settings = Animation(menu, "settingsEntrance");
                var settingsBackdrop = menu.settingsOverlay.GetComponent<Image>();
                CheckStart(menu.settingsCard, settingsBackdrop, settings);
                float openedAt = (float)settings.GetType().GetField("started", Private).GetValue(settings);
                menu.OpenSettings();
                Require(openedAt == (float)settings.GetType().GetField("started", Private).GetValue(settings),
                    "Repeated Settings taps restarted the entrance.");
                CheckMotion(menu.settingsCard, settingsBackdrop, settings);
                foreach (var size in new[] { new Vector2(390,844), new Vector2(390,667), new Vector2(844,390) })
                {
                    menu.Layout(size.x, size.y, new Rect(Vector2.zero, size));
                    Vector2 position = menu.settingsCard.anchoredPosition; float scale = menu.settingsCard.localScale.x;
                    menu.Layout(size.x, size.y, new Rect(Vector2.zero, size));
                    Require(menu.settingsCard.anchoredPosition == position && menu.settingsCard.localScale.x == scale
                        && !float.IsNaN(position.x) && !float.IsNaN(position.y)
                        && !float.IsInfinity(position.x) && !float.IsInfinity(position.y),
                        "Settings entrance drifted after layout/rotation.");
                }
                menu.CloseSettings(); menu.OpenSettings(); CheckStart(menu.settingsCard, settingsBackdrop, settings);
                menu.CloseSettings(); menu.HideForGameplay(); game.StartLevel(3); game.OpenPause();
                var pause = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
                var pauseEntrance = Animation(pause, "entrance");
                var pauseBackdrop = pause.GetComponent<Image>();
                CheckStart(pause.card, pauseBackdrop, pauseEntrance);
                CheckMotion(pause.card, pauseBackdrop, pauseEntrance);
                menu.OpenSettingsFromPause(); CheckStart(menu.settingsCard, settingsBackdrop, settings, true);
                Require(pauseBackdrop.color.a == 0, "Settings stacked its dimmer over the pause dimmer.");
                Require(game.IsPaused && menu.IsPauseSettingsOpen && !view.canvas.GetComponent<GraphicRaycaster>().enabled,
                    "Animated pause Settings released the underlying game.");
                CheckMotion(menu.settingsCard, settingsBackdrop, settings, true);
                menu.CloseSettings();
                Require(game.IsPaused && !menu.IsPauseSettingsOpen && view.canvas.GetComponent<GraphicRaycaster>().enabled,
                    "Closing animated Settings did not return to pause.");
                CheckStart(pause.card, pauseBackdrop, pauseEntrance, true);
                CheckMotion(pause.card, pauseBackdrop, pauseEntrance, true);
                game.ResumePause(); menu.Show();
                var retention = menu.GetComponent<HueRetentionMenu>();
                foreach (var kind in new[] { HueRetentionKind.Profile, HueRetentionKind.Shortcut })
                {
                    if (kind == HueRetentionKind.Profile) retention.OpenProfile(); else retention.OpenShortcut();
                    var guideEntrance = Animation(retention, "entrance");
                    var guideBackdrop = retention.overlay.GetComponent<Image>();
                    CheckStart(retention.card, guideBackdrop, guideEntrance);
                    Sample(guideEntrance, .1f);
                    float currentAlpha = retention.card.GetComponent<CanvasGroup>().alpha;
                    retention.Refresh();
                    Require(retention.card.GetComponent<CanvasGroup>().alpha == currentAlpha,
                        "Retention qualification refresh restarted the animation.");
                    Sample(guideEntrance, 0);
                    CheckMotion(retention.card, guideBackdrop, guideEntrance);
                    retention.Close();
                    Require(!retention.IsOpen && retention.card.GetComponent<CanvasGroup>().alpha == 1,
                        "Closing the guide did not reset its entrance.");
                }
                retention.OpenProfile(); retention.OpenShortcut();
                Require(retention.title.text == "Add to Home Screen", "Switching guide kinds lost its content.");
                CheckStart(retention.card, retention.overlay.GetComponent<Image>(), Animation(retention, "entrance"));
                retention.Close(); menu.HideForGameplay(); game.StartLevel(3);
                var debugPanel = game.GetComponentInChildren<HueRuntimeDebugPanel>(true);
                Require(debugPanel, "Development debug panel was not attached.");
                debugPanel.Open();
                var debugCard = (RectTransform)typeof(HueRuntimeDebugPanel).GetField("card", Private).GetValue(debugPanel);
                var debugOverlay = (GameObject)typeof(HueRuntimeDebugPanel).GetField("overlay", Private).GetValue(debugPanel);
                var debugEntrance = Animation(debugPanel, "entrance");
                CheckStart(debugCard, debugOverlay.GetComponent<Image>(), debugEntrance);
                Require(game.IsRuntimeDebugOpen, "Debug entrance did not block the game.");
                CheckMotion(debugCard, debugOverlay.GetComponent<Image>(), debugEntrance);
                debugPanel.Close();
                Require(!game.IsRuntimeDebugOpen, "Debug close retained the input lock.");
                var curve = typeof(HueGame).Assembly.GetType("HueRelay.HuePanelEntrance").GetMethod("Evaluate");
                foreach (bool won in new[] { true, false })
                {
                    foreach (float elapsed in new[] { 0f, .1f, .21f, .42f })
                    {
                        float easing = (float)curve.Invoke(null, new object[] { elapsed });
                        view.SetResult(won, false, 1, easing, "Result", "0", "", "Next", "Home");
                        Require(Mathf.Abs(view.resultGroup.alpha - easing) < .001f
                            && view.resultGroup.interactable == (easing > .9f)
                            && Mathf.Abs(view.resultBackdrop.color.a - .55f * easing) < .001f,
                            "Win/failure did not use the shared modal curve and input threshold.");
                    }
                }
                debugPanel.Open();
                Sample(debugEntrance, .42f);
                Require(view.resultBackdrop.color.a == 0 && debugOverlay.GetComponent<Image>().color == HueModalBackdrop.Tint,
                    "Debug over a result stacked two dark overlays.");
                // Updating a lower modal must never take over the active dimmer.
                view.SetResult(false, false, 1, 1, "Result", "0", "", "Retry", "Home");
                Require(view.resultBackdrop.color.a == 0, "A result refresh re-enabled the lower dimmer.");
                debugPanel.Close();
                Require(view.resultBackdrop.color == HueModalBackdrop.Tint,
                    "Closing the top panel failed to restore the result dimmer.");
                game.StartLevel(3);
                Require(!HueModalBackdrop.Contains(view.resultBackdrop), "Restart retained a result dimmer registration.");
                Require(HueAudio.SoundEnabled == sound && HueAudio.VibrationEnabled == vibration,
                    "Panel entrance changed sound or vibration settings.");
                Directory.CreateDirectory("Logs/PanelEntrance");
                File.WriteAllText("Logs/PanelEntrance/checks.txt", "PASS: " + checks + " checks: shared 420ms fade/rise/scale; win/failure, refill, Settings, pause, Profile, Shortcut and development debug panels; synchronized dimmers/input gating; unscaled time; stable layout; repeated/midway close/reopen; no drift; ad/qualification refresh; pause-Settings-pause restoration; viewport changes; player settings preserved.\n");
            }
            finally
            {
                Time.timeScale = timeScale;
                game.ClosePowerUpRefill(); menu.CloseSettings(); menu.HideForGameplay();
                game.ResumePause(); game.StartLevel(3); inventory.SetValue(game, original);
                if (had) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
