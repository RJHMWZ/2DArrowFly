using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HueMiniGameTools
    {
        public const string ScenePath = "Assets/HueRelay/Scenes/HueRelay.unity";
        internal const string ValidationKey = "HueRelay.MiniGameValidation";
        internal const string ReportDirectory = "Logs/MiniGameValidation";

        [MenuItem("Tools/Hue Relay/检查小游戏配置")]
        public static void ValidateMenu()
        {
            var errors = Validate();
            if (errors.Count == 0) Debug.Log("HueRelay 小游戏配置检查通过。");
            else foreach (var error in errors) Debug.LogError(error);
        }

        [MenuItem("Tools/Hue Relay/定位游戏与加载配置")]
        public static void SelectManager()
        {
            var manager = UnityEngine.Object.FindObjectOfType<UIManager>(true);
            if (manager != null) Selection.activeGameObject = manager.gameObject;
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) errors.Add("请打开 HueRelay 场景后检查。");
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var target in objects)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target.gameObject) > 0)
                    errors.Add("脚本丢失：" + target.name);
            var managers = objects.Select(t => t.GetComponent<UIManager>()).Where(m => m != null).ToArray();
            if (managers.Length != 1) errors.Add("场景必须只有一个 UIManager。");
            var view = UnityEngine.Object.FindObjectOfType<HueView>(true);
            if (view == null) errors.Add("缺少 HueView。");
            else if (!view.ValidateBindings(out var reason)) errors.Add(reason);
            else if (view.canvas.GetComponent<CanvasScaler>().uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                errors.Add("游戏 Canvas 需要使用 Constant Pixel Size。");
            if (managers.Length == 1)
            {
                var manager = managers[0];
                var presentation = manager.GetComponent<LoadingPresentation>();
                if (!manager.loadingPanel || !manager.loadingProgressFill || !manager.loadingStatusText)
                    errors.Add("加载界面引用未绑定。");
                if (!presentation || !presentation.loadingIcon || !presentation.loadingLogo || !presentation.loadingTrack)
                    errors.Add("加载布局引用未绑定。");
                if (manager.brandIconTargets.Any(i => i == null) || manager.brandLogoTargets.Any(b => b == null || b.image == null))
                    errors.Add("加载品牌列表有空引用。");
            }
            var background = UnityEngine.Object.FindObjectOfType<GameBackgroundConfig>(true);
            if (!background || !background.RenderCanvas) errors.Add("背景 Canvas 未绑定。");
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0].path != ScenePath)
                errors.Add("Build Settings 只应启用 HueRelay 场景。");
            return errors;
        }

        // Run in an isolated Unity project so the user's open scene stays untouched.
        public static void BatchValidate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var errors = Validate();
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllLines(ReportDirectory + "/dependencies.txt", AssetDatabase.GetDependencies(ScenePath, true));
            File.WriteAllLines(ReportDirectory + "/validation.txt", errors.Count == 0
                ? new[] { "PASS: scene bindings, loading UI, background, build scene" } : errors);
            if (errors.Count > 0) { EditorApplication.Exit(1); return; }
            SessionState.SetBool(ValidationKey, true);
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoad]
    internal static class HueMiniGameBatchSmoke
    {
        static double started = -1;
        static bool waitingForEndless;
        static readonly List<string> errors = new List<string>();

        static HueMiniGameBatchSmoke()
        {
            if (!SessionState.GetBool(HueMiniGameTools.ValidationKey, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (message.StartsWith("Release of invalid GC handle.")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (started < 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started < 2) return;
            try
            {
                var manager = UIManager.Instance;
                var game = UnityEngine.Object.FindObjectOfType<HueGame>();
                if (!manager || !game || game.P == null) throw new Exception("Game did not initialize.");
                if (manager.IsLoading || manager.BlocksGameplay) throw new Exception("Loading did not release gameplay.");
                if (waitingForEndless)
                {
                    if ((bool)Field("generating").GetValue(game))
                    {
                        if (EditorApplication.timeSinceStartup - started > 30) throw new Exception("Endless generation timed out.");
                        return;
                    }
                    if (!game.Endless || game.Over || game.ModalVisible) throw new Exception("Final-level continue did not enter endless play.");
                    // Verify the new board still accepts an actual free-arrow tap.
                    Field("built").SetValue(game, Time.unscaledTime - 10);
                    var best = HueEngine.Best(game.P, game.Mask, new HueRandom(7), 300);
                    int mask = game.Mask;
                    game.TapArrow(best.first);
                    if (game.Mask == mask || game.Taps != 1) throw new Exception("Endless board rejected input.");
                    Finish();
                    return;
                }
                game.StartLevel(0);
                CompleteResult(game, false);
                if (!game.ModalVisible || manager.BlocksGameplay) throw new Exception("Failure did not show the normal result.");
                game.ContinueResult();
                if (game.Over || game.ModalVisible || game.LevelIndex != 0) throw new Exception("Failure retry did not reset the level.");
                CompleteResult(game, true);
                game.ContinueResult();
                if (game.LevelIndex != 1 || game.Over) throw new Exception("Win did not advance to the next level.");
                game.StartLevel(4);
                CompleteResult(game, true);
                if (!game.ModalVisible || manager.BlocksGameplay) throw new Exception("Final win did not show the normal result.");
                game.ContinueResult();
                waitingForEndless = true;
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }

        static FieldInfo Field(string name) => typeof(HueGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        static void CompleteResult(HueGame game, bool won)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(HueGame).GetMethod("Finish", flags).Invoke(game, new object[] { won });
            typeof(HueGame).GetMethod("ShowSheet", flags).Invoke(game, null);
        }

        static void Finish()
        {
            File.WriteAllLines(HueMiniGameTools.ReportDirectory + "/playmode.txt", errors.Count == 0
                ? new[] { "PASS: startup, loading, failure retry, win advance, final-level result, endless continuation and input" } : errors);
            SessionState.SetBool(HueMiniGameTools.ValidationKey, false);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
