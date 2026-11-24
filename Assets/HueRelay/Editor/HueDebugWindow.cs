using System;
using UnityEditor;
using UnityEngine;

namespace HueRelay.Editor
{
    public sealed class HueDebugWindow : EditorWindow
    {
        [SerializeField] private int hints = 1, erasers = 1, extraMoves = 1, level = 1;
        private Vector2 scroll;
        private HueGame game;
        private string message;
        private HueAds simulatedAds;
        private HueAdPolicy originalPolicy, simulationPolicy;
        private int outcome;
        private static readonly string[] Outcomes = { "完整观看 / 插屏正常关闭", "提前关闭", "播放失败", "无广告填充", "播放无回调（测试超时）" };

        [MenuItem("Tools/Hue Relay/调试面板", false, 0)]
        public static void Open() => GetWindow<HueDebugWindow>("Hue Relay 调试");

        private void OnEnable() => EditorApplication.playModeStateChanged += PlayModeChanged;
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            StopSimulation();
        }
        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) StopSimulation();
        }
        private void OnInspectorUpdate() => Repaint();

        private void Run(Action action)
        {
            try { action(); message = "操作完成。"; }
            catch (Exception e) { message = e.Message; Debug.LogException(e); }
            Repaint();
        }
        private static bool Confirm(string action) => EditorUtility.DisplayDialog("修改本地测试存档", action + "\n此操作会写入本地存档。", "执行", "取消");
        private void Heading(string text) { EditorGUILayout.Space(10); EditorGUILayout.LabelField(text, EditorStyles.boldLabel); }
        private void ApplyInventory(int h, int e, int x)
        {
            if (EditorApplication.isPlaying)
            {
                if (!game) throw new InvalidOperationException("请先运行 HueRelay 场景。");
                game.DebugSetPowerUps(h, e, x);
            }
            else
            {
                var config = Resources.Load<HuePowerUpConfig>("HueRelay/Data/PowerUps");
                if (!config) throw new InvalidOperationException("缺少 PowerUps 配置。");
                new HuePowerUpInventory(config).DebugSetCounts(h, e, x);
            }
            hints = Mathf.Max(0, h); erasers = Mathf.Max(0, e); extraMoves = Mathf.Max(0, x);
        }

        private void OnGUI()
        {
            game = EditorApplication.isPlaying ? FindObjectOfType<HueGame>() : null;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("初始道具配置只在首次创建库存时生效。旧的 3 / 3 / 3 来自本地存档，点击下方“恢复配置默认数量”即可更新。调试工具仅存在于 Unity 编辑器。", MessageType.Info);
            bool switching = EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;
            bool blocked = switching || (EditorApplication.isPlaying && (!game || !game.DebugCanEdit));
            if (game)
                EditorGUILayout.LabelField($"当前：{(game.Endless ? "无尽模式" : "第 " + (game.LevelIndex + 1) + " 关")}  道具 {game.HintsRemaining} / {game.ErasersRemaining} / {game.ExtraMovesRemaining}");
            if (blocked) EditorGUILayout.HelpBox("等待场景初始化、广告或棋盘生成结束后可操作。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(blocked))
            {
                Heading("道具库存（停止和 Play 模式均可修改）");
                hints = EditorGUILayout.IntField("提示", hints);
                erasers = EditorGUILayout.IntField("擦除", erasers);
                extraMoves = EditorGUILayout.IntField("加步道具", extraMoves);
                if (GUILayout.Button("应用自定义数量并保存")) Run(() => ApplyInventory(hints, erasers, extraMoves));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("恢复配置默认数量")) Run(() =>
                    {
                        var c = game ? game.PowerUpSettings : Resources.Load<HuePowerUpConfig>("HueRelay/Data/PowerUps");
                        if (!c) throw new InvalidOperationException("缺少 PowerUps 配置。");
                        ApplyInventory(c.initialHints, c.initialErasers, c.initialExtraMoves);
                    });
                    if (GUILayout.Button("全部清零")) Run(() => ApplyInventory(0, 0, 0));
                    if (GUILayout.Button("各加到 10")) Run(() => ApplyInventory(10, 10, 10));
                }
                EditorGUILayout.LabelField("仅修改余额，保留 Profile / Home screen 奖励领取记录。", EditorStyles.wordWrappedMiniLabel);
            }
            using (new EditorGUI.DisabledScope(blocked || !game))
            {
                Heading("关卡与结算（需要 Play 模式）");
                EditorGUILayout.HelpBox("跳关不解锁后续存档。快速胜利会走真实通关记录与广告计数；快速结算从当前棋盘重置后开始，不模拟得分过程。", MessageType.None);
                level = EditorGUILayout.IntField("关卡号（从 1 开始）", level);
                if (GUILayout.Button("进入指定关卡")) Run(() => game.DebugJumpToLevel(level));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重玩当前棋盘")) Run(() => game.Retry());
                    if (GUILayout.Button("快速胜利")) Run(() => game.DebugFinish(true));
                    if (GUILayout.Button("快速失败")) Run(() => game.DebugFinish(false));
                }
                if (GUILayout.Button("进入无尽模式")) Run(() => { game.DebugJumpToLevel(game.LevelIndex + 1); game.StartEndless(); });
                Heading("教学与本地记录");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重播首次教学")) Run(() => game.DebugTutorial(true));
                    if (GUILayout.Button("标记教学已完成")) Run(() => game.DebugTutorial(false));
                }
                if (game) EditorGUILayout.LabelField($"教学已完成：{game.HasCompletedTutorial}   下次进入：{game.ResumeButtonLabel}   最高分：{game.DebugBestScore}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重置闯关进度") && Confirm("重置当前闯关存档并进入第 1 关？道具和教学记录保留。")) Run(() => game.DebugSetCampaignProgress(false));
                    if (GUILayout.Button("解锁全部关卡") && Confirm("将当前闯关存档标记为全部完成？")) Run(() => game.DebugSetCampaignProgress(true));
                    if (GUILayout.Button("清零最高分") && Confirm("清零全局最高分？")) Run(() => game.DebugResetBestScore());
                }
                Heading("广告模拟（仅本次运行）");
                EditorGUILayout.HelpBox("模拟沿用当前冷却和插屏门槛，不请求平台广告。启用或更换结果会重置本次广告会话计数；关闭窗口或退出 Play 后还原。测试激励：道具清零 → 游戏中点击道具 → Watch Ad。", MessageType.None);
                outcome = EditorGUILayout.Popup("模拟结果", outcome, Outcomes);
                if (GUILayout.Button("启用 / 更新模拟并重置广告会话")) Run(StartSimulation);
                if (GUILayout.Button("恢复正常广告后端")) Run(StopSimulation);
                if (game)
                {
                    var ads = game.GetComponent<HueAds>();
                    EditorGUILayout.LabelField($"后端：{(simulatedAds ? "调试模拟" : "正常")}   广告播放中：{(ads && ads.IsBusy)}");
                    if (ads && ads.Pacing != null)
                        EditorGUILayout.LabelField($"距上次插屏通关数：{ads.Pacing.WinsSinceInterstitial}   会话插屏数：{ads.Pacing.InterstitialsThisSession}");
                }
            }
            // Always allow abandoning the intentional no-callback simulation.
            if (simulatedAds && simulatedAds.IsBusy && GUILayout.Button("终止模拟广告并恢复正常后端")) Run(StopSimulation);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void StartSimulation()
        {
            var ads = game.GetComponent<HueAds>();
            if (!ads || !ads.policy) throw new InvalidOperationException("场景缺少已初始化的 HueAds / AdPolicy。");
            StopSimulation();
            originalPolicy = ads.policy;
            simulationPolicy = Instantiate(originalPolicy);
            simulationPolicy.hideFlags = HideFlags.HideAndDontSave;
            simulationPolicy.adsEnabled = simulationPolicy.rewardedEnabled = simulationPolicy.interstitialEnabled = true;
            simulationPolicy.rewardedAdUnitId = "editor-rewarded";
            simulationPolicy.interstitialAdUnitId = "";
            simulationPolicy.logDecisions = true;
            simulatedAds = ads;
            ads.Initialize(game, simulationPolicy, new DebugBackend(outcome));
        }
        private void StopSimulation()
        {
            if (simulatedAds && originalPolicy)
                simulatedAds.Initialize(simulatedAds.game, originalPolicy, new HueTikTokAdBackend(originalPolicy));
            simulatedAds = null; originalPolicy = null;
            if (simulationPolicy) DestroyImmediate(simulationPolicy);
            simulationPolicy = null;
        }

        internal sealed class DebugBackend : IHueAdBackend
        {
            private readonly int mode;
            private readonly bool[] ready = new bool[2];
            public event Action<bool> VisibilityChanged { add { } remove { } }
            public DebugBackend(int mode) { this.mode = mode; }
            public bool Supports(HueAdFormat format) => format == HueAdFormat.Rewarded;
            public bool IsReady(HueAdFormat format) => ready[(int)format];
            public void Preload(HueAdFormat format) { ready[(int)format] = mode != 3; }
            public void Show(HueAdFormat format, Action<HueAdOutcome> completed)
            {
                if (!IsReady(format)) { completed(HueAdOutcome.Unavailable); return; }
                Release(format);
                if (mode == 4) return;
                completed(mode == 0 ? HueAdOutcome.Completed : mode == 1 ? HueAdOutcome.Cancelled : HueAdOutcome.Failed);
            }
            public void Release(HueAdFormat format) { ready[(int)format] = false; }
            public void Dispose() { Array.Clear(ready, 0, ready.Length); }
        }
    }
}
