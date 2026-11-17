using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HuePlayableTools
    {
        public const string ScenePath = "Assets/HueRelay/Scenes/HueRelay.unity";

        [MenuItem("Tools/Hue Relay/检查试玩配置")]
        public static void ValidateMenu()
        {
            var errors = Validate();
            if (errors.Count == 0) Debug.Log("HueRelay 试玩配置检查通过。背景、品牌、CTA、场景和导出依赖已绑定。");
            else foreach (var error in errors) Debug.LogError(error);
        }

        [MenuItem("Tools/Hue Relay/定位品牌与试玩配置")]
        public static void SelectManager()
        {
            var manager = UnityEngine.Object.FindObjectOfType<UIManager>(true);
            if (manager != null) Selection.activeGameObject = manager.gameObject;
        }

        [MenuItem("Tools/Hue Relay/打开转包工具")]
        public static void OpenExportTools() => EditorApplication.ExecuteMenuItem("ExportTools/Open Window");

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
            if (managers.Length != 1) errors.Add("场景必须只有一个 UIManager（包括停用的对象）。");
            var view = UnityEngine.Object.FindObjectOfType<HueView>(true);
            if (view == null) errors.Add("缺少 HueView。");
            else if (!view.ValidateBindings(out var reason)) errors.Add(reason);
            else if (view.canvas.GetComponent<CanvasScaler>().uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                errors.Add("HueGame 计算逻辑像素，原游戏 Canvas 需要使用 Constant Pixel Size。");
            if (managers.Length == 1)
            {
                var manager = managers[0];
                if (!manager.winPanel || !manager.footer || !manager.loadingPanel) errors.Add("底栏、尾卡或加载 UI 未绑定。");
                if (!manager.presentation || !manager.presentation.endBrand || !manager.presentation.endRays || !manager.presentation.loadingIcon)
                    errors.Add("HTML 界面布局与尾卡动画引用未绑定。");
                if (!manager.BrandIconTexture || !manager.BrandLogoTexture || !manager.CtaButtonTexture) errors.Add("品牌中台替换槽未绑定。");
                if (manager.brandIconTargets.Any(i => i == null) || manager.brandLogoTargets.Any(b => b == null || b.image == null)) errors.Add("品牌绑定列表有空引用。");
                foreach (var binding in manager.ctaButtonTargets)
                {
                    var button = binding?.image ? binding.image.GetComponent<Button>() : null;
                    if (button == null || !HasAdSkip(button, manager)) errors.Add("CTA 未绑定 UIManager.AdSkip。");
                }
                if (manager.winPanel && !HasAdSkip(manager.winPanel.GetComponent<Button>(), manager)) errors.Add("尾卡整屏点击未绑定 AdSkip。");
            }
            var background = UnityEngine.Object.FindObjectOfType<GameBackgroundConfig>(true);
            if (background == null || !background.RenderCanvas || !background.BackgroundTexture) errors.Add("背景替换或独立背景 Canvas 未绑定。");
            if (background != null && background.RenderCanvas && background.RenderCanvas.sortingOrder >= 0) errors.Add("背景 Canvas 应位于棋盘后方。");
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0].path != ScenePath) errors.Add("Build Settings 只应启用 HueRelay 场景。");
            if (!File.Exists("luna.json")) errors.Add("缺少 luna.json。");
            return errors;
        }

        private static bool HasAdSkip(Button button, UIManager manager)
        {
            if (button == null) return false;
            int matches = 0;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == manager && button.onClick.GetPersistentMethodName(i) == "AdSkip") matches++;
            return matches == 1;
        }

        // Used by an isolated Unity batch project so the open editor need not be closed.
        public static void BatchValidate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var errors = Validate();
            var dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Directory.CreateDirectory("Logs/TemplateMigration");
            File.WriteAllLines("Logs/TemplateMigration/dependencies.txt", dependencies);
            File.WriteAllLines("Logs/TemplateMigration/validation.txt", errors.Count == 0 ? new[] { "PASS: scene bindings, UI scripts, CTA events, branding, background, build scene" } : errors);
            if (errors.Count > 0) { EditorApplication.Exit(1); return; }
            SessionState.SetBool("HueRelay.PlayableValidation", true);
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoad]
    internal static class HuePlayableBatchSmoke
    {
        static double started = -1;
        static bool waitingForTimeout;
        static bool waitingForAnimation;
        static float timeoutRequestedAt;
        static readonly List<string> errors = new List<string>();
        static HuePlayableBatchSmoke()
        {
            if (SessionState.GetBool("HueRelay.PlayableValidation", false))
            {
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            // Unity 2022 can emit this native diagnostic during domain reload in batch mode.
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
                if (manager == null || game == null || game.P == null) throw new Exception("Runtime scene did not initialize.");
                if (manager.IsLoading) throw new Exception("Loading UI did not finish.");
                if (waitingForAnimation)
                {
                    if (Time.unscaledTime - timeoutRequestedAt < .65f) return;
                    if (Mathf.Abs(manager.presentation.endBrand.localScale.x - 1) > .001f)
                        throw new Exception("End-card pop did not restore its authored scale.");
                    if (Quaternion.Angle(Quaternion.identity, manager.presentation.endRays.localRotation) < 1)
                        throw new Exception("End-card rays did not rotate.");
                    var cta = manager.ctaButtonTargets[0].image.GetComponent<Button>();
                    cta.onClick.Invoke(); cta.onClick.Invoke();
                    Finish(); return;
                }
                if (waitingForTimeout)
                {
                    if (Time.unscaledTime - timeoutRequestedAt < .1f) return;
                    if (!manager.HasRequestedEnd || !manager.winPanel.activeInHierarchy || !manager.BlocksGameplay)
                        throw new Exception("Trial timeout did not show a blocking end card.");
                    manager.NotifyLevelResult(true, true);
                    manager.CompletePlayable();
                    int mask = game.Mask;
                    game.TapArrow(0);
                    if (game.Mask != mask) throw new Exception("End card allowed a board tap.");
                    if (manager.footer.gameObject.activeInHierarchy) throw new Exception("End card did not hide the footer.");
                    if (manager.presentation.endBrand.localScale.x <= 0) throw new Exception("End-card pop did not start.");
                    waitingForAnimation = true;
                    return;
                }
                game.StartLevel(0);
                if (manager.BlocksPointer(new Vector2(Screen.width * .5f, Screen.height * .5f)))
                    throw new Exception("The footer blocked the center of the board.");
                manager.NotifyLevelResult(true, false);
                if (manager.HasRequestedEnd) throw new Exception("An ordinary level unexpectedly ended the playable.");
                manager.BrandName = "Override Test";
                manager.ApplyBranding();
                if (manager.brandNameTargets.Any(t => t.text != "Override Test")) throw new Exception("Brand text replacement failed.");
                var labelSizes = manager.brandNameTargets.Select(t => t.fontSize).ToArray();
                manager.ApplyBranding();
                if (!labelSizes.SequenceEqual(manager.brandNameTargets.Select(t => t.fontSize))) throw new Exception("Brand replacement changed authored typography.");
                var originalIcon = manager.BrandIconTexture;
                var originalLogo = manager.BrandLogoTexture;
                var originalButton = manager.CtaButtonTexture;
                var background = UnityEngine.Object.FindObjectOfType<GameBackgroundConfig>();
                var originalBackground = background.BackgroundTexture;
                var replacement = new Texture2D(32, 16);
                replacement.SetPixels(Enumerable.Repeat(Color.cyan, 32 * 16).ToArray()); replacement.Apply();
                manager.BrandIconTexture = manager.BrandLogoTexture = manager.CtaButtonTexture = replacement;
                manager.ShowCtaButtonText = false;
                manager.ApplyBranding();
                background.BackgroundTexture = replacement; background.ApplySettings();
                if (manager.brandIconTargets.Any(i => i.sprite == null || i.sprite.texture != replacement)
                    || manager.brandLogoTargets.Any(b => b.image.sprite == null || b.image.sprite.texture != replacement)
                    || manager.ctaButtonTargets.Any(b => b.image.sprite == null || b.image.sprite.texture != replacement || b.label.enabled)
                    || background.GetComponent<Image>().sprite.texture != replacement)
                    throw new Exception("One of the four branding/background replacement slots failed.");
                manager.BrandIconTexture = originalIcon; manager.BrandLogoTexture = originalLogo; manager.CtaButtonTexture = originalButton;
                manager.ShowCtaButtonText = true; manager.ApplyBranding();
                background.BackgroundTexture = originalBackground; background.ApplySettings();
                UnityEngine.Object.Destroy(replacement);
                var audio = new SerializedObject(UnityEngine.Object.FindObjectOfType<HueAudio>());
                var tones = audio.FindProperty("playableTones");
                if (tones.arraySize != 76) throw new Exception("Playable tone bank was not bound.");
                for (int i = 0; i < tones.arraySize; i++)
                    if (!tones.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue)
                        throw new Exception("Playable tone clip is missing.");
                manager.trialDuration = .01f;
                manager.NotifyGameplayStarted();
                timeoutRequestedAt = Time.unscaledTime;
                waitingForTimeout = true;
                return;
            }
            catch (Exception e) { errors.Add(e.ToString()); }
            Finish();
        }

        static void Finish()
        {
            File.WriteAllLines("Logs/TemplateMigration/playmode.txt", errors.Count == 0 ? new[] { "PASS: startup, loading, ordinary result, four texture replacements, typography, 76 audio references, trial timeout, footer input, end-card pop/rotation, repeated completion, input isolation, repeated CTA" } : errors);
            SessionState.SetBool("HueRelay.PlayableValidation", false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
