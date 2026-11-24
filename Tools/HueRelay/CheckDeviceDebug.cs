// Copy into Editor in an isolated validation project, then run HueDeviceDebugChecks.Run.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using HueRelay;
using HueRelay.Editor;

[InitializeOnLoad]
public static class HueDeviceDebugChecks
{
    const string Pending = "HueDeviceDebugChecks.pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string ProgressKey = "cc_device_debug_progress", InventoryKey = "cc_device_debug_inventory";
    const string TutorialKey = "cc_device_debug_tutorial", BestKey = "cc_device_debug_best";
    static double entered, opened;
    static int phase, mask, taps;
    static float frozen;
    static HueGame game;
    static HueRuntimeDebugPanel panel;
    static FieldInfo Field(string name) => typeof(HueGame).GetField(name, Private);
    static float Clock() => (float)Field("now").GetValue(game);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static HueDeviceDebugChecks() { if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        PlayerSettings.companyName = "HueRelayValidation";
        PlayerSettings.productName = "DeviceDebugChecks";
        EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
        SessionState.SetBool(Pending, true); EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    static Button Button(string path) => panel.transform.Find("DebugOverlay/DebugCard/" + path).GetComponent<Button>();
    static void Tick()
    {
        if (!Application.isPlaying) return;
        if (entered == 0) entered = EditorApplication.timeSinceStartup;
        try
        {
            Require(EditorApplication.timeSinceStartup - entered < 30, "Device debug checks timed out.");
            if (phase == 0)
            {
                if (EditorApplication.timeSinceStartup - entered < 1) return;
                game = UnityEngine.Object.FindObjectOfType<HueGame>();
                game.EndTutorial();
                HueFeatureChecks.Run();
                var view = UnityEngine.Object.FindObjectOfType<HueView>();
                panel = game.GetComponentInChildren<HueRuntimeDebugPanel>(true);
                Require(panel && HueRuntimeDebugPanel.Attach(game, view) == panel, "Debug canvas is missing or duplicated.");
                foreach (string key in new[] { ProgressKey, InventoryKey, TutorialKey, TutorialKey + "_started", BestKey }) PlayerPrefs.DeleteKey(key);
                Field("campaignProgress").SetValue(game, new HueCampaignProgress(ProgressKey, game.LevelCount, 18));
                PlayerPrefs.SetString(InventoryKey, "{\"version\":1,\"hints\":7,\"erasers\":8,\"extraMoves\":9,\"profileRewardClaimed\":true}");
                Field("inventory").SetValue(game, new HuePowerUpInventory(game.PowerUpSettings, InventoryKey));
                Field("tutorialSeenKey").SetValue(game, TutorialKey);
                Field("bestScoreKey").SetValue(game, BestKey);
                Field("bestScore").SetValue(game, 600); PlayerPrefs.SetInt(BestKey, 600);
                PlayerPrefs.SetInt(TutorialKey, 1);
                PlayerPrefs.SetString("cc_device_debug_sdk_sentinel", "preserve");
                game.DebugJumpToLevel(19);
                var ads = game.GetComponent<HueAds>();
                var policy = UnityEngine.Object.Instantiate(ads.policy); policy.adsEnabled = false;
                ads.Initialize(game, policy, new HueTikTokAdBackend(policy));
                panel.Open();
                Require(panel.IsOpen && game.IsRuntimeDebugOpen, "DBG entry did not open its modal.");
                Require(panel.transform.Find("DebugOverlay/DebugCard").GetComponentsInChildren<Button>().Length == 4,
                    "Panel must contain only the three requested actions and Close.");
                mask = game.Mask; taps = game.Taps; frozen = Clock(); opened = EditorApplication.timeSinceStartup;
                game.TapArrow(game.P.L.solution[0]); game.Hint(); game.AddMoves(); game.ToggleErase();
                Require(game.Mask == mask && game.Taps == taps && !game.IsChoosingErase, "Debug modal accepted underlying gameplay input.");
                phase = 1; return;
            }
            if (phase == 1)
            {
                if (EditorApplication.timeSinceStartup - opened < .3) return;
                Require(Clock() == frozen && game.Mask == mask && game.Taps == taps, "Debug modal did not freeze the board.");
                panel.Close();
                float after = (float)typeof(HueGame).GetProperty("GameTime", Private).GetValue(game);
                Require(after - frozen < .15f, "Closing Debug counted the modal's time as gameplay.");
                game.OpenPause(); panel.Open(); opened = EditorApplication.timeSinceStartup;
                frozen = (float)typeof(HueGame).GetProperty("GameTime", Private).GetValue(game);
                phase = 2; return;
            }
            if (phase == 2)
            {
                if (EditorApplication.timeSinceStartup - opened < .3) return;
                panel.Close(); game.ResumePause();
                float after = (float)typeof(HueGame).GetProperty("GameTime", Private).GetValue(game);
                Require(Mathf.Abs(after - frozen) < .15f, "Debug over Pause counted paused time twice.");
                panel.Open();
                var input = panel.GetComponentInChildren<InputField>(true);
                input.text = "999"; Button("JumpLevel").onClick.Invoke();
                Require(panel.IsOpen && game.LevelIndex == 18, "Invalid jump changed the board or closed the panel.");
                input.text = "8"; Button("JumpLevel").onClick.Invoke();
                Require(!panel.IsOpen && !game.IsRuntimeDebugOpen && game.LevelIndex == 7 && !game.IsTutorial,
                    "Jump did not enter the requested level.");
                panel.Open(); Button("WinLevel").onClick.Invoke();
                Require(!panel.IsOpen && game.Over && game.Won && game.ModalVisible && game.Mask == 0,
                    "Direct win did not show the normal result.");
                Require(new HueCampaignProgress(ProgressKey, game.LevelCount, 0).NextLevelIndex == 18,
                    "Winning an earlier test level regressed saved progress.");
                panel.Open(); Button("FreshStart").onClick.Invoke();
                Require(!panel.IsOpen && !game.IsRuntimeDebugOpen && !game.IsTutorial && game.LevelIndex == 0
                    && game.ResumeLevelIndex == 0 && game.Score == 0 && game.Taps == 0 && game.DebugBestScore == 0,
                    "Fresh start did not reset progress/score and enter level one directly.");
                Require(game.HintsRemaining == game.PowerUpSettings.initialHints && game.ErasersRemaining == game.PowerUpSettings.initialErasers
                    && game.ExtraMovesRemaining == game.PowerUpSettings.initialExtraMoves, "Fresh start did not restore initial inventory.");
                Require(PlayerPrefs.GetInt(BestKey) == 0 && PlayerPrefs.GetInt(ProgressKey) == 0
                    && game.HasCompletedTutorial && PlayerPrefs.GetInt(TutorialKey + "_started") == 0,
                    "Fresh start changes were not persisted.");
                Require(PlayerPrefs.GetString("cc_device_debug_sdk_sentinel") == "preserve"
                    && game.HasClaimedRetentionReward(HueRetentionKind.Profile), "Fresh start erased unrelated data or existing retention receipts.");
                game.EndTutorial(); game.BeginFromMenu();
                Require(!game.IsTutorial && game.LevelIndex == 0 && game.Mask == game.P.Full, "Fresh start did not stay on level one without a tutorial.");
                Field("generating").SetValue(game, true); panel.Open();
                Require(!panel.IsOpen, "Debug opened while loading a board.");
                Field("generating").SetValue(game, false);
                var ads = game.GetComponent<HueAds>();
                Field("revivePending").SetValue(game, true); panel.Open();
                bool rejected = false;
                try { game.DebugFreshStart(); } catch (InvalidOperationException) { rejected = true; }
                Require(!panel.IsOpen && rejected, "Debug reset was allowed during a rewarded ad.");
                Field("revivePending").SetValue(game, false);
                panel.Open(); Capture(panel);
                Directory.CreateDirectory("Logs/DeviceDebug");
                File.WriteAllText("Logs/DeviceDebug/runtime.txt", "PASS: debug-only three-action modal; no duplicate canvas; freezes gameplay; excludes modal time and avoids nested-pause double counting; valid/invalid level jumps; direct normal win; fresh level-one entry without tutorial; progress/items/best persisted; unrelated SDK data and retention receipts preserved; loading/ad guards; three viewport renders.\n");
                SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Capture(HueRuntimeDebugPanel debug)
    {
        Directory.CreateDirectory("Logs/DeviceDebug");
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) if (canvas.gameObject != debug.gameObject) canvas.enabled = false;
        var target = debug.GetComponent<Canvas>();
        debug.GetComponent<CanvasScaler>().enabled = false;
        foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(390, 667), new Vector2Int(844, 390) })
        {
            var camera = new GameObject("DeviceDebugPreview", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(size.x * 2, size.y * 2, 24);
            camera.targetTexture = rt; camera.orthographic = true; camera.orthographicSize = size.y / 2f;
            camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HueGame.Background;
            target.renderMode = RenderMode.ScreenSpaceCamera; target.worldCamera = camera; target.planeDistance = 10;
            target.scaleFactor = 2; Canvas.ForceUpdateCanvases();
            typeof(HueRuntimeDebugPanel).GetMethod("Layout", Private).Invoke(debug, null); Canvas.ForceUpdateCanvases();
            var card = (RectTransform)debug.transform.Find("DebugOverlay/DebugCard");
            var corners = new Vector3[4]; card.GetWorldCorners(corners);
            foreach (var p in corners) Require(((RectTransform)target.transform).rect.Contains(target.transform.InverseTransformPoint(p)), "Debug card escaped viewport.");
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/DeviceDebug/" + size.x + "x" + size.y + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture);
            camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(camera.gameObject); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
