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
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target.gameObject) > 0)
                    errors.Add("脚本丢失：" + target.name);
                if (target.name.Any(c => c >= '\u3400' && c <= '\u9fff'))
                    errors.Add("Scene object names must be English: " + target.name);
            }
            var managers = objects.Select(t => t.GetComponent<UIManager>()).Where(m => m != null).ToArray();
            if (managers.Length != 1) errors.Add("场景必须只有一个 UIManager。");
            var view = UnityEngine.Object.FindObjectOfType<HueView>(true);
            if (view == null) errors.Add("缺少 HueView。");
            else if (!view.ValidateBindings(out var reason)) errors.Add(reason);
            else if (view.canvas.GetComponent<CanvasScaler>().uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                errors.Add("游戏 Canvas 需要使用 Constant Pixel Size。");
            if (view != null && view.ValidateBindings(out _))
            {
                var icons = view.powerUpBar.GetComponentsInChildren<Image>(true);
                if (icons.Length != 3 || icons.Any(icon => !icon.sprite || !icon.preserveAspect || icon.raycastTarget))
                    errors.Add("三个道具图标必须绑定透明图片、保持比例且不拦截点击。");
                if (!view.eraseCursor || !view.eraseCursor.sprite || !view.eraseDust || !view.eraseDust.texture || !view.erasePrompt || !view.eraseLabel)
                    errors.Add("Generated eraser animation and selection prompt must be bound.");
                var retryIcon = view.retryButton.GetComponentInChildren<Image>(true);
                if (!retryIcon || !retryIcon.sprite || retryIcon.raycastTarget)
                    errors.Add("Retry must use a PNG icon without intercepting pointer input.");
                var game = UnityEngine.Object.FindObjectOfType<HueGame>(true);
                foreach (var binding in new[] { (view.hintButton, "Hint"), (view.eraseButton, "ToggleErase"), (view.extraMovesButton, "AddMoves") })
                {
                    var button = binding.Item1;
                    if (!button.transform.IsChildOf(view.powerUpBar) || button.onClick.GetPersistentEventCount() != 1
                        || button.onClick.GetPersistentTarget(0) != game || button.onClick.GetPersistentMethodName(0) != binding.Item2)
                        errors.Add("道具按钮位置或点击事件错误：" + binding.Item2);
                }
            }
            if (objects.Any(t => t.name == "LoadingCanvas" || t.name == "Loading"))
                errors.Add("Startup loading UI must be removed from the game scene.");
            var background = UnityEngine.Object.FindObjectOfType<GameBackgroundConfig>(true);
            if (!background || !background.RenderCanvas) errors.Add("背景 Canvas 未绑定。");
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0].path != ScenePath)
                errors.Add("Build Settings 只应启用 HueRelay 场景。");
            errors.AddRange(HueCampaignValidation.Validate());
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
                ? new[] { "PASS: scene bindings, direct startup without loading UI, background, build scene; 100 unique campaign layouts, geometry, item budgets, increasing difficulty, standard solutions replayed with HueEngine" } : errors);
            if (errors.Count > 0) { EditorApplication.Exit(1); return; }
            // Reproduce an already open scene surviving a script reload: the item
            // objects exist, while the newly added serialized fields are still null.
            // HueGame.Awake must bind that scene successfully without manual repair.
            var view = UnityEngine.Object.FindObjectOfType<HueView>(true);
            view.powerUpBar = null;
            view.eraseButton = view.extraMovesButton = null;
            view.eraseCount = view.extraMovesCount = null;
            SessionState.SetBool(ValidationKey, true);
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoad]
    internal static class HueMiniGameBatchSmoke
    {
        static double started = -1;
        static bool waitingForEndless;
        static double endlessStartedAt;
        static int itemPhase;
        static int overlapPhase;
        static double overlapStarted;
        static bool overlapFinished;
        static bool campaignChecked;
        static int originalMask, originalLimit;
        static double itemStarted;
        static bool winCheckStarted, winCheckFinished, sawWinSettling;
        static double winCheckStartedAt;
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
            if (EditorApplication.timeSinceStartup - started < .25) return;
            try
            {
                var manager = UIManager.Instance;
                var game = UnityEngine.Object.FindObjectOfType<HueGame>();
                if (!manager || !game || game.P == null) throw new Exception("Game did not initialize.");
                var currentView = UnityEngine.Object.FindObjectOfType<HueView>();
                if (!game.enabled || !currentView.ValidateBindings(out var bindingReason))
                    throw new Exception("Reloaded scene did not recover its required references.");
                if (!campaignChecked)
                {
                    HueCampaignValidation.CheckRuntime(game, currentView);
                    campaignChecked = true;
                }
                if (!overlapFinished)
                {
                    CheckOverlappingTaps(game);
                    return;
                }
                if (itemPhase < 6)
                {
                    CheckPowerUps(game);
                    return;
                }
                if (!winCheckFinished)
                {
                    CheckWinTransition(game, currentView);
                    return;
                }
                if (waitingForEndless)
                {
                    if ((bool)Field("generating").GetValue(game))
                    {
                        if (EditorApplication.timeSinceStartup - endlessStartedAt > 30) throw new Exception("Endless generation timed out.");
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
                if (!game.ModalVisible) throw new Exception("Failure did not show the normal result.");
                game.ContinueResult();
                if (game.Over || game.ModalVisible || game.LevelIndex != 0) throw new Exception("Failure retry did not reset the level.");
                CompleteResult(game, true);
                game.ContinueResult();
                if (game.LevelIndex != 1 || game.Over) throw new Exception("Win did not advance to the next level.");
                game.StartLevel(game.LevelCount - 1);
                CompleteResult(game, true);
                if (!game.ModalVisible) throw new Exception("Final win did not show the normal result.");
                game.ContinueResult();
                waitingForEndless = true;
                endlessStartedAt = EditorApplication.timeSinceStartup;
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }

        static FieldInfo Field(string name) => typeof(HueGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        static void CheckOverlappingTaps(HueGame game)
        {
            if (overlapPhase == 0)
            {
                game.StartLevel(0);
                typeof(HueGame).GetProperty("P").SetValue(game, HueEngine.Prep(new LevelData(5, 6, new[] {
                    new ArrowData("B", "r", new[] { new Cell(3, 1), new Cell(4, 1) }),
                    new ArrowData("B", "r", new[] { new Cell(0, 1), new Cell(1, 1) }),
                    new ArrowData("C", "u", new[] { new Cell(2, 5), new Cell(2, 4) }),
                    new ArrowData("A", "r", new[] { new Cell(0, 3), new Cell(1, 3) })
                })));
                game.ResetGame();
                Field("built").SetValue(game, Time.unscaledTime - 10);
                game.TapArrow(0);
                if (!(bool)Field("busy").GetValue(game)) throw new Exception("Overlap fixture did not start an automatic chain.");
                int pendingMask = game.Mask;
                game.TapArrow(1); // Queued for automatic launch; never spend a manual move here.
                if (game.Taps != 1 || game.Mask != pendingMask) throw new Exception("A queued arrow accepted a duplicate manual tap.");
                game.TapArrow(2); // An independent arrow must launch before the pending chain advances.
                if (game.Taps != 2 || game.Mask != 10 || game.Idle)
                    throw new Exception("An automatic chain blocked an independent manual tap.");
                game.TapArrow(2);
                game.ToggleErase();
                if (game.Taps != 2 || game.IsChoosingErase) throw new Exception("Duplicate input or an eraser interrupted an active chain.");
                overlapStarted = EditorApplication.timeSinceStartup;
                overlapPhase = 1;
                return;
            }
            if (EditorApplication.timeSinceStartup - overlapStarted > 15) throw new Exception("Concurrent arrow animations did not settle.");
            if (!game.Idle) return;
            if (overlapPhase == 1)
            {
                if (game.Mask != 8 || game.Taps != 2) throw new Exception("The original automatic chain was lost or charged an extra move.");
                game.ResetGame();
                Field("built").SetValue(game, Time.unscaledTime - 10);
                game.TapArrow(2);
                if ((bool)Field("busy").GetValue(game) || game.Idle) throw new Exception("Flight-only fixture was not active.");
                game.TapArrow(3);
                game.TapArrow(3);
                if (game.Mask != 3 || game.Taps != 2) throw new Exception("A flying arrow blocked another manual tap or allowed a duplicate.");
                overlapStarted = EditorApplication.timeSinceStartup;
                overlapPhase = 2;
                return;
            }
            if (overlapPhase == 2)
            {
                if (game.Mask != 3 || game.Taps != 2) throw new Exception("Concurrent manual flights changed the wrong arrows.");
                game.ResetGame();
                Field("built").SetValue(game, Time.unscaledTime - 10);
                game.TapArrow(1); // Blocked: it animates a collision instead of launching.
                game.TapArrow(2);
                game.TapArrow(1);
                if (game.Mask != 11 || game.Taps != 2) throw new Exception("A bump blocked another arrow or accepted a repeated tap.");
                overlapStarted = EditorApplication.timeSinceStartup;
                overlapPhase = 3;
                return;
            }
            if (game.Mask != 11 || game.Taps != 2) throw new Exception("Concurrent bump/flight did not finish consistently.");
            overlapFinished = true;
        }

        static void CheckWinTransition(HueGame game, HueView view)
        {
            if (!winCheckStarted)
            {
                game.StartLevel(0);
                var puzzle = HueEngine.Prep(new LevelData(5, 6, new[] {
                    new ArrowData("B", "r", new[] { new Cell(1, 2), new Cell(2, 2) })
                }));
                typeof(HueGame).GetProperty("P").SetValue(game, puzzle);
                game.ResetGame();
                Field("built").SetValue(game, Time.unscaledTime - 10);
                game.TapArrow(0);
                typeof(HueGame).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                winCheckStarted = true;
                winCheckStartedAt = EditorApplication.timeSinceStartup;
            }
            if (game.Idle && !game.Over) sawWinSettling = true;
            if (game.Mask != 0 || game.CanErase || game.CanHint || game.CanAddMoves
                || view.eraseButton.interactable || view.hintButton.interactable || view.extraMovesButton.interactable)
                throw new Exception("An item became active between board clear and victory settlement.");
            // Invoke directly as well: an old/stale UI click must not interrupt victory.
            view.eraseButton.onClick.Invoke();
            if (game.Mask != 0 || game.ErasersRemaining != 3 || game.Taps != 1)
                throw new Exception("An eraser interrupted or consumed inventory during victory.");
            if (game.ModalVisible)
            {
                if (!sawWinSettling || !game.Over || !game.Won)
                    throw new Exception("Victory regression did not cover the settling interval and result.");
                winCheckFinished = true;
                return;
            }
            if (EditorApplication.timeSinceStartup - winCheckStartedAt > 15)
                throw new Exception("Victory did not reach its result after the final arrow.");
        }

        static void CheckPowerUps(HueGame game)
        {
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            if (itemPhase == 0)
            {
                game.StartLevel(3);
                var puzzle = HueEngine.Prep(new LevelData(5, 6, new[] {
                    new ArrowData("B", "r", new[] { new Cell(3, 1), new Cell(4, 1) }),
                    new ArrowData("B", "r", new[] { new Cell(0, 1), new Cell(1, 1) }),
                    new ArrowData("C", "u", new[] { new Cell(2, 5), new Cell(2, 4) })
                }));
                typeof(HueGame).GetProperty("P").SetValue(game, puzzle);
                game.ResetGame();
                originalMask = game.Mask;
                originalLimit = game.Limit;
                if (!game.CanErase || game.ErasersRemaining != 3 || game.ExtraMovesRemaining != 1)
                    throw new Exception("Fresh-board item state is incorrect.");
                view.hintButton.onClick.Invoke();
                if (game.HintsRemaining != 2 || (int)Field("hintId").GetValue(game) < 0)
                    throw new Exception("Hint did not highlight an arrow and consume one use.");
                view.eraseButton.onClick.Invoke();
                if (!game.IsChoosingErase || game.ErasersRemaining != 3 || game.CanHint || game.CanAddMoves)
                    throw new Exception("Erase selection consumed an item or allowed another tool.");
                view.eraseButton.onClick.Invoke();
                if (game.IsChoosingErase || game.ErasersRemaining != 3)
                    throw new Exception("Cancel did not leave inventory unchanged.");
                view.eraseButton.onClick.Invoke();
                game.TapArrow(-1); game.TapArrow(game.P.n);
                if (game.Mask != originalMask || game.ErasersRemaining != 3 || !game.IsChoosingErase)
                    throw new Exception("An invalid target consumed the eraser.");
                // Hit the blocked arrow through the same board-pointer route used
                // by a real tap, including UI exclusion and arrow hit testing.
                Vector2 target = game.BoardOrigin + (game.P.arrs[1].Head + Vector2.one * .5f) * game.CellSize;
                typeof(HueGame).GetMethod("Pointer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(game, new object[] { target });
                game.TapArrow(0); view.eraseButton.onClick.Invoke();
                if (!game.IsErasing || game.Mask != originalMask || game.ErasersRemaining != 2 || game.Taps != 0)
                    throw new Exception("Erase did not animate exactly one selected blocked arrow.");
                if (!view.eraseCursor.gameObject.activeSelf)
                    throw new Exception("Generated eraser animation did not start.");
                itemStarted = EditorApplication.timeSinceStartup;
                itemPhase = 1;
                return;
            }
            if (EditorApplication.timeSinceStartup - itemStarted > 20)
                throw new Exception("Eraser regression timed out.");
            if (!game.Idle) return;
            if (itemPhase == 1)
            {
                if (game.Mask != (originalMask & ~(1 << 1)) || game.Taps != 0 || game.Score != 0 || game.ErasersRemaining != 2)
                    throw new Exception("Erasing removed another arrow, spent a move, or awarded launch points.");
                view.extraMovesButton.onClick.Invoke(); view.extraMovesButton.onClick.Invoke();
                if (game.Limit != originalLimit + 3 || game.ExtraMovesRemaining != 0)
                    throw new Exception("Extra moves did not add exactly three steps once.");
                game.ToggleErase(); game.TapArrow(0); game.Retry();
                if (game.IsErasing || game.IsChoosingErase || game.Limit != originalLimit || game.ErasersRemaining != 3
                    || game.HintsRemaining != 3 || game.ExtraMovesRemaining != 1)
                    throw new Exception("Retry did not cancel animation and reset inventory/moves.");
                itemStarted = EditorApplication.timeSinceStartup;
                itemPhase = 2;
                return;
            }
            if (itemPhase == 2)
            {
                if (EditorApplication.timeSinceStartup - itemStarted < 1.5) return;
                if (game.Mask != originalMask) throw new Exception("A cancelled animation removed an arrow after retry.");
                game.ToggleErase(); game.StartTutorial();
                game.ToggleErase(); game.Hint(); game.AddMoves();
                if (game.IsChoosingErase || game.IsErasing || game.CanErase || game.ErasersRemaining != 3 || game.HintsRemaining != 3)
                    throw new Exception("Tutorial consumed items or retained erase selection.");
                game.EndTutorial();
                game.ToggleErase(); game.TapArrow(0); // Launching this arrow would chain arrow 1.
                itemStarted = EditorApplication.timeSinceStartup;
                itemPhase = 3;
                return;
            }
            if (itemPhase == 3)
            {
                if (game.Mask != (originalMask & ~1) || game.Score != 0 || game.Taps != 0 || game.ErasersRemaining != 2)
                    throw new Exception("Erasing incorrectly launched a same-color chain.");
                CompleteResult(game, false);
                view.eraseButton.onClick.Invoke();
                if (game.Over || game.ModalVisible || !game.IsChoosingErase)
                    throw new Exception("Erase could not select a target after failure.");
                game.TapArrow(1);
                itemStarted = EditorApplication.timeSinceStartup;
                itemPhase = 4;
                return;
            }
            if (itemPhase == 4)
            {
                if (game.Mask != (1 << 2) || game.ErasersRemaining != 1 || game.Taps != 0)
                    throw new Exception("Failure rescue erased the wrong arrow or spent a move.");
                game.ToggleErase(); game.TapArrow(2);
                itemStarted = EditorApplication.timeSinceStartup;
                itemPhase = 5;
                return;
            }
            if (game.Mask != 0 || game.CanErase || game.CanHint || game.CanAddMoves)
                throw new Exception("An item became active after erasing the last arrow.");
            if (!game.ModalVisible) return;
            if (!game.Won || game.ErasersRemaining != 0 || game.Taps != 0)
                throw new Exception("Last-arrow erasure did not finish as a win.");
            game.ResetGame();
            CompleteResult(game, false);
            game.AddMoves();
            if (game.Over || game.ModalVisible || game.Limit != originalLimit + 3)
                throw new Exception("Extra moves did not resume a failed board.");
            CheckResponsiveLayouts(game);
            itemPhase = 6;
        }

        static void CheckResponsiveLayouts(HueGame game)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var layout = typeof(HueGame).GetMethod("LayoutViewport", flags);
            foreach (var size in new[] { new Vector2(320, 480), new Vector2(390, 844), new Vector2(402, 707), new Vector2(430, 932), new Vector2(844, 390) })
            {
                for (int level = 0; level < game.LevelCount; level++)
                {
                    game.StartLevel(level);
                    layout.Invoke(game, new object[] { size.x, size.y, new Rect(0, 34, size.x, size.y - 78) });
                    var view = UnityEngine.Object.FindObjectOfType<HueView>();
                    float scale = (float)Field("K").GetValue(game);
                    float boardBottom = game.BoardOrigin.y + game.P.L.h * game.CellSize + 8 * scale;
                    float barTop = -view.powerUpBar.anchoredPosition.y;
                    if (boardBottom + 8 * scale > barTop || game.BoardOrigin.y - 8 * scale < (float)Field("stageY").GetValue(game))
                        throw new Exception("Board overlaps item bar/header at " + size + ", level " + level);
                    if (view.hintButton.transform.IsChildOf(view.header)) throw new Exception("Hint remains in the upper-right toolbar.");
                }
            }
        }

        static void CompleteResult(HueGame game, bool won)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(HueGame).GetMethod("Finish", flags).Invoke(game, new object[] { won });
            typeof(HueGame).GetMethod("ShowSheet", flags).Invoke(game, null);
        }

        static void Finish()
        {
            File.WriteAllLines(HueMiniGameTools.ReportDirectory + "/playmode.txt", errors.Count == 0
                ? new[] { "PASS: all 100 campaign budgets, hint routes, retry resets, chapter transitions and level labels; scene reload with missing item references, immediate startup, concurrent taps during chains/flights/bumps, queued/duplicate tap protection, hint, erase selection/cancel, blocked targets, animated removal, no chain/score/move cost, item limits, retry cancellation, failure rescue, tutorial isolation, eraser last-arrow victory, 500 responsive layouts, victory item lock through settling/bonus/result, win advance, level 100 endless continuation and input" } : errors);
            SessionState.SetBool(HueMiniGameTools.ValidationKey, false);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
