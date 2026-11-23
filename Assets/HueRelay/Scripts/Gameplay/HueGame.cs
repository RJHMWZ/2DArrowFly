using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using static HueRelay.HueEngine;

namespace HueRelay
{
    /// <summary>
    /// 游戏主控制器：维护分数、步数、接力队列、输入、结算和延迟任务，其他 partial 文件负责具体表现。
    /// </summary>
    public sealed partial class HueGame : MonoBehaviour
    {
        /// <summary>
        /// 深色箭头与主文字颜色：#1c2040。
        /// </summary>
        public static readonly Color Ink = Hex("1c2040");

        /// <summary>
        /// 蓝色箭头与主要按钮颜色：#4a6cf7。
        /// </summary>
        public static readonly Color Blue = Hex("4a6cf7");

        /// <summary>
        /// 橙色箭头、奖励和低步数提示颜色：#ff9a1f。
        /// </summary>
        public static readonly Color Orange = Hex("ff9a1f");

        /// <summary>
        /// 页面浅紫色背景：#e8eaf6。
        /// </summary>
        public static readonly Color Background = Hex("e8eaf6");

        /// <summary>
        /// 最高分和结算辅助文字颜色：#8085a8。
        /// </summary>
        public static readonly Color Mute = Hex("8085a8");

        /// <summary>
        /// 进度条未完成部分颜色：#d5d9ee。
        /// </summary>
        public static readonly Color Track = Hex("d5d9ee");

        /// <summary>
        /// 未受波纹影响时的网格圆点颜色：#cdd2ea。
        /// </summary>
        public static readonly Color Dot = Hex("cdd2ea");

        /// <summary>
        /// 碰撞反馈颜色：#ef4b4b。
        /// </summary>
        public static readonly Color Red = Hex("ef4b4b");

        /// <summary>
        /// 将不含井号的十六进制颜色字符串转换为 Unity 颜色。
        /// </summary>
        /// <param name="c">不含井号的六位十六进制 RGB 字符串。</param>
        /// <returns>解析得到的 Unity 颜色。</returns>
        public static Color Hex(string c)
        {
            Color v;
            ColorUtility.TryParseHtmlString("#" + c, out v);
            return v;
        }

        /// <summary>
        /// 将原 HTML 的 A、B、C 颜色代码映射为深色、蓝色和橙色。
        /// </summary>
        /// <param name="c">原版颜色代码 A、B 或 C。</param>
        /// <returns>颜色代码对应的固定配色。</returns>
        public static Color Col(string c)
        {
            return c == "A" ? Ink : c == "B" ? Blue : Orange;
        }

        /// <summary>
        /// 当前已预处理的棋盘。
        /// </summary>
        public Puzzle P
        {
            get; private set;
        }

        /// <summary>
        /// 仍留在棋盘上的箭头位掩码。
        /// </summary>
        public int Mask
        {
            get; private set;
        }

        /// <summary>
        /// 当前实际分数；滚动显示值单独平滑追踪此值。
        /// </summary>
        public int Score
        {
            get; private set;
        }

        /// <summary>
        /// 本局已消耗的手动点击步数。
        /// </summary>
        public int Taps
        {
            get; private set;
        }

        /// <summary>
        /// 本局允许的总步数。
        /// </summary>
        public int Limit
        {
            get; private set;
        }

        /// <summary>
        /// 是否已进入胜负结算流程。
        /// </summary>
        public bool Over
        {
            get; private set;
        }

        /// <summary>
        /// 本局是否清盘成功。
        /// </summary>
        public bool Won
        {
            get; private set;
        }

        /// <summary>
        /// 当前预设关卡的零基编号。
        /// </summary>
        public int LevelIndex
        {
            get; private set;
        }

        /// <summary>
        /// 是否处于无尽模式。
        /// </summary>
        public bool Endless
        {
            get; private set;
        }

        private LevelData[] levels;
        private string campaignBestPrefix = "cc_b";
        public int LevelCount => levels != null ? levels.Length : 0;
        public bool IsLastCampaignLevel => !Endless && LevelIndex == LevelCount - 1;
        private int par;
        private int hints = 3;
        private int bestScore;
        private int seed;
        private int chainN;
        private int remaining;
        private int bonusLaunched;
        private int bonusLanded;

        /// <summary>
        /// 追踪实际分数的滚动显示值。
        /// </summary>
        private float ds;
        private float built;

        /// <summary>
        /// 棋盘首次进入可结算静止状态的时间；负数表示尚未等待。
        /// </summary>
        private float finishAt = -1;

        /// <summary>
        /// 下一支接力箭头的计划发射时间；负数表示没有计划。
        /// </summary>
        private float nextChain = -1;
        private float scorePop = -100;
        private float scorePopAmount = 1;
        private float comboAt = -100;
        private float mileAt = -100;
        private float mileN;
        private float shakeAt = -100;
        private float shakeAmplitude;
        private float lastShake = -100;
        private float punchAt = -100;
        private float endAt = -100;
        private float modalAt = -100;
        private float progress;
        private float progressFrom;
        private float progressTarget;
        private float progressAt;
        private Color comboColor;
        private Color mileColor;
        private string chipOverride;

        /// <summary>
        /// 是否仍有自动接力等待执行。
        /// </summary>
        private bool busy;
        private bool sheetVisible;
        private bool record;

        /// <summary>
        /// 是否正在筛选无尽棋盘；阻止期间的重复按钮操作。
        /// </summary>
        private bool generating;
        [Header("Scene Bindings")]
        [SerializeField] private HueView sceneView;
        [SerializeField] private HueAudio audioBus;

        /// <summary>
        /// 提示搜索持续使用的随机序列；重试不重置该序列，以对应原版行为。
        /// </summary>
        private HueRandom hintRandom = new HueRandom(99);

        /// <summary>
        /// 等待 400 毫秒接力动画完成后自动发射的箭头队列。
        /// </summary>
        private readonly Queue<int> q = new Queue<int>(30);

        /// <summary>
        /// 已解锁、等待接力的箭头集合。
        /// </summary>
        private readonly HashSet<int> charged = new HashSet<int>();

        /// <summary>
        /// 箭头开始接收接力光点的时间，用于激活圆环和扫光。
        /// </summary>
        private readonly Dictionary<int, float> chargeAt = new Dictionary<int, float>();
        private readonly List<int> auto = new List<int>(30);

        /// <summary>
        /// 属于当前棋局的一次延迟回调；重置时统一清空，防止旧棋局触发声音或结算。
        /// </summary>
        struct Timed
        {
            public float at;
            public Action action;
        }

        /// <summary>
        /// 当前棋局拥有的延迟任务，重置时全部取消。
        /// </summary>
        private readonly List<Timed> timed = new List<Timed>(64);
        private HueSurface board;
        private HueSurface fx;
        private RawImage finger;


        private Canvas root;
        private CanvasScaler rootScaler;
        private static readonly double[] ChordRatios = { 1, 1.26, 1.5, 2 };
        private static readonly int[] VictoryNotes = { 0, 4, 7, 12, 16 };

        /// <summary>
        /// 本帧非缩放时间，单位为秒。
        /// </summary>
        private float now;

        /// <summary>
        /// 本帧模拟步长，按原版限制为不超过 0.05 秒。
        /// </summary>
        private float dt;
        private float viewWidth = 390;
        private float viewHeight = 800;

        /// <summary>
        /// 原 CSS 的 --k 缩放系数。
        /// </summary>
        private float K = 1;

        /// <summary>
        /// 格子边长，单位为逻辑像素。
        /// </summary>
        private float cs = 34;

        /// <summary>
        /// 棋盘原点的水平逻辑像素坐标。
        /// </summary>
        private float ox;

        /// <summary>
        /// 棋盘原点的垂直逻辑像素坐标。
        /// </summary>
        private float oy;
        private float stageY;
        private float stageH;
        private float appX;
        private float appW;
        private float safeTop;
        private float safeBottom;
        private float pixelScale;
        private Rect chipRect;
        private Rect scoreRect;
        private int hintId = -1;
        private float hintAt;
        private float deviceDensity;

        /// <summary>
        /// 一个网格边长对应的逻辑像素数。
        /// </summary>
        public float CellSize
        {
            get
            {
                return cs;
            }
        }

        /// <summary>
        /// 棋盘左上角在界面逻辑像素坐标中的位置。
        /// </summary>
        public Vector2 BoardOrigin
        {
            get
            {
                return new Vector2(ox, oy);
            }
        }

        /// <summary>
        /// 底部结算面板是否已出现。
        /// </summary>
        public bool ModalVisible
        {
            get
            {
                return sheetVisible;
            }
        }

        /// <summary>
        /// 接力、飞行和碰撞均已结束；用于结算等待和流程验证。
        /// </summary>
        public bool Idle
        {
            get
            {
                return !busy && flies.Count == 0 && bumps.Count == 0 && !IsErasing;
            }
        }

        /// <summary>
        /// 绑定场景组件并进入主菜单；关卡按钮按教学标记启动首次教学。
        /// </summary>
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
#if !UNITY_LUNA
            // Keep the game in portrait orientation while playing.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
#endif
            if (!BindScene())
            {
                enabled = false;
                return;
            }
            var collection = JsonConvert.DeserializeObject<LevelCollection>(sceneView.levelData.text);
            levels = collection?.levels;
            if (LevelCount == 0)
            {
                Debug.LogError("HueGame requires at least one campaign level.", this);
                enabled = false;
                return;
            }
            if (!string.IsNullOrEmpty(collection.campaignId)) campaignBestPrefix = "cc_" + collection.campaignId + "_b";
            now = GameTime;
            if (PlayerPrefs.HasKey("cc_prog"))
            {
                PlayerPrefs.DeleteKey("cc_prog");
                PlayerPrefs.Save();
            }
            StartLevel(UIManager.Instance != null ? UIManager.Instance.startingLevel - 1 : 0);
            mainMenu = FindObjectOfType<HueMainMenu>();
            if (mainMenu)
                mainMenu.Show();
            else
                BeginFromMenu();
        }

        private HueMainMenu mainMenu;

        /// <summary>关卡按钮进入游戏；首次教学在点击之后启动。</summary>
        public void BeginFromMenu()
        {
            HueSafeArea.Refresh();
            ResetGame();
            if ((UIManager.Instance == null || UIManager.Instance.showTutorial) && PlayerPrefs.GetInt("cc_seen", 0) == 0)
                StartTutorial();
        }

        /// <summary>胜负面板的 Home 入口，清理本局动画后返回主菜单。</summary>
        public void ReturnHome()
        {
            if ((!sheetVisible && !IsPaused) || generating) return;
            if (!mainMenu) mainMenu = FindObjectOfType<HueMainMenu>();
            if (!mainMenu || !mainMenu.ResolveBindings()) return;
            ResumePause();
            if (tut >= 0) EndTutorial();
            ResetGame();
            mainMenu.Show();
        }

        /// <summary>
        /// 进入指定预设关卡，按原算法估算基准步数，并重置所有运行状态。
        /// </summary>
        /// <param name="index">预设关卡或教学步骤的零基编号。</param>
        public void StartLevel(int index)
        {
            LevelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            Endless = false;
            P = Prep(levels[LevelIndex]);
            par = P.L.par > 0 ? P.L.par : Best(P, P.Full, new HueRandom(7), 300).taps;
            Limit = P.L.moves > 0 ? P.L.moves : par + 2;
            baseMoveLimit = Limit;
            bestScore = PlayerPrefs.GetInt(campaignBestPrefix + LevelIndex, 0);
            ResetGame();
        }

        /// <summary>
        /// 启动无尽棋盘筛选；生成期间拒绝重复启动，避免同时运行多个生成协程。
        /// </summary>
        public void StartEndless()
        {
            if (generating)
                return;
            generating = true;
            Endless = true;
            bestScore = PlayerPrefs.GetInt("cc_best", 0);
            LevelData L = null;
            Metrics metrics = null;
            // 每次尝试后让出一帧，避免筛选复杂棋盘时长时间阻塞输入和绘制。
            StartCoroutine(FindEndless((l, m) =>
            {
                L = l;
                metrics = m;
                P = Prep(L);
                par = metrics.par;
                Limit = par + 2;
                baseMoveLimit = Limit;
                ResetGame();
            }));
        }

        /// <summary>
        /// 逐帧尝试种植式生成并筛选难度，失败后退回普通随机棋盘。
        /// </summary>
        /// <param name="ready">筛选成功后接收布局和难度评估的回调。</param>
        /// <returns>可由 Unity 调度的逐帧生成协程。</returns>
        private System.Collections.IEnumerator FindEndless(Action<LevelData, Metrics> ready)
        {
            for (int t = 0; t < 30; t++)
            {
                var sp = Specs[1 + UnityEngine.Random.Range(0, 4)];
                int color = UnityEngine.Random.Range(0, 3);
                int sd = UnityEngine.Random.Range(0, 1000000000);
                var r = Plant(sp, sd, color);
                if (r != null)
                {
                    var m = Measure(r.L);
                    if (m != null && m.init <= 2 && m.gap >= 1 && r.L.a.Length >= sp.total - 3)
                    {
                        seed = sd;
                        ready(r.L, m);
                        yield break;
                    }
                }

                yield return null;
            }

            StartRandom();
        }

        /// <summary>
        /// 按固定种子或筛选出的新种子生成随机棋盘，并设置原版额外两步容错。
        /// </summary>
        /// <param name="fixedSeed">可选固定种子，未指定时重新筛选随机种子。</param>
        public void StartRandom(int? fixedSeed = null)
        {
            var c = Pick(out seed, fixedSeed, P == null ? 9 : P.L.w, P == null ? 12 : P.L.h);
            P = Prep(Gen(seed, P == null ? 9 : P.L.w, P == null ? 12 : P.L.h));
            par = c.taps;
            Limit = par + 2;
            baseMoveLimit = Limit;
            ResetGame();
        }

        /// <summary>
        /// 重置当前棋盘；统一取消协程、接力队列、延迟回调和特效，防止旧状态触发重复结算。
        /// </summary>
        public void ResetGame()
        {
            ResumePause();
            StopAllCoroutines();
            generating = false;
            timed.Clear();
            now = GameTime;
            built = now;
            Mask = P.Full;
            Taps = Score = 0;
            ds = 0;
            hints = Mathf.Max(0, P.L.hints);
            eraseUses = Mathf.Max(0, P.L.erasers);
            extraMoveUses = Mathf.Max(0, P.L.extraMoves);
            Limit = baseMoveLimit;
            CancelErase();
            q.Clear();
            auto.Clear();
            charged.Clear();
            chargeAt.Clear();
            ClearEffects();
            sceneView.HideTransient();
            busy = Over = Won = sheetVisible = false;
            cachedModal = -1;
            nextChain = -1;
            finishAt = -1;
            punchAt = endAt = modalAt = comboAt = mileAt = scorePop = -100;
            hintId = -1;
            remaining = bonusLaunched = bonusLanded = 0;
            chipOverride = null;
            tut = -1;
            snapshot = null;
            progress = progressFrom = progressTarget = 0;
            guidePuzzle = null;
            guideMask = guideArrow = -1;
            Layout();
        }

        /// <summary>
        /// 登记属于当前棋局的延迟任务；使用与动画一致的非缩放时间。
        /// </summary>
        /// <param name="delay">延迟秒数。</param>
        /// <param name="action">到期后执行的当前棋局回调。</param>
        private void After(float delay, Action action)
        {
            timed.Add(new Timed { at = now + delay, action = action });
        }

        /// <summary>
        /// 记录进度条的新目标和过渡起点，实际缓动由 Update 统一计算。
        /// </summary>
        private void UIProgress()
        {
            progressFrom = progress;
            progressTarget = (P.n - PC(Mask)) / (float)P.n;
            progressAt = now;
        }

        /// <summary>
        /// 触发分数文字的 380 毫秒弹性反馈。
        /// </summary>
        /// <param name="amount">分数反馈的最大缩放倍率。</param>
        private void Pop(float amount)
        {
            scorePop = now;
            scorePopAmount = amount;
        }

        /// <summary>
        /// 优先提示关卡标准解；偏离标准路线或进入无尽模式时使用模拟搜索。
        /// </summary>
        public void Hint()
        {
            if (!CanHint)
                return;
            int suggested = CampaignHint();
            if (suggested < 0) suggested = Best(P, Mask, hintRandom, 300).first;
            if (suggested < 0)
                return;
            hints--;
            hintId = suggested;
            hintAt = now;
            audioBus.Tone(660, .14, .08);
            Render();
        }

        private int CampaignHint()
        {
            if (Endless || P.L.solution == null) return -1;
            int remainingMask = P.Full;
            foreach (int selected in P.L.solution)
            {
                if (selected < 0 || selected >= P.n || !IsFree(P, remainingMask, selected)) return -1;
                if (remainingMask == Mask) return selected;
                remainingMask = Chain(P, remainingMask, selected);
            }
            return -1;
        }

        /// <summary>
        /// 执行一次实际发射：更新掩码和连锁计分，创建离场效果，并把新解锁箭头加入自动接力队列。
        /// </summary>
        /// <param name="i">箭头编号或阶段索引。</param>
        private void Step(int i)
        {
            if ((Mask & (1 << i)) == 0)
                return;
            auto.Clear();
            Mask = Launch(P, Mask, i, auto);
            var a = P.arrs[i];
            charged.Remove(i);
            chainN++;
            int points = 10 * chainN;
            Score += points;
            flies.Add(new Flight { a = a, s = 0, k = chainN, points = points });
            Ring(G(a.Head), Col(a.c), cs * .15f, 2.6f, 3.6f, 2);
            audioBus.Blip(chainN - 1);
            UIProgress();
            Pop(1.18f + Mathf.Min(chainN, 6) * .05f);
            if (chainN == 5 || chainN == 8 || chainN == 12)
            {
                mileAt = now;
                mileN = chainN;
                mileColor = Col(a.c);
                Spawn(new Vector2(P.L.w * cs / 2, P.L.h * cs / 2), mileColor, 5, Vector2.down);
                for (int k = 0; k < 4; k++)
                {
                    double ratio = ChordRatios[k];
                    After(k * .07f, () => audioBus.Tone(392 * ratio, .7, .05));
                }
            }

            if (chainN > 1)
            {
                comboAt = now;
                comboColor = Col(a.c);
            }

            foreach (int j in auto)
            {
                charged.Add(j);
                chargeAt.Remove(j);
                q.Enqueue(j);
            }

            if (q.Count > 0)
            {
                busy = true;
                if (nextChain < 0)
                    Schedule(a.Head, Col(a.c));
            }
            else if (nextChain < 0)
            {
                busy = false;
                if (chainN >= 3)
                {
                    double b = HueAudio.Ending(chainN);
                    for (int k = 0; k < 3; k++)
                    {
                        double mul = ChordRatios[k];
                        After(.32f + k * .06f, () => audioBus.Tone(b * mul, .9, .045));
                    }
                }
            }
        }

        /// <summary>
        /// 为队列首个箭头启动接力光点，并在 400 毫秒后发射该箭头。
        /// </summary>
        /// <param name="h">前一支箭头头部的网格坐标。</param>
        /// <param name="col">接力光点颜色。</param>
        private void Schedule(Vector2 h, Color col)
        {
            int nx = q.Peek();
            chargeAt[nx] = now;
            beads.Add(new Bead { from = h, to = P.arrs[nx].Head, t = now, c = col, s = chainN % 2 != 0 ? 1 : -1 });
            nextChain = now + .4f;
        }

        /// <summary>
        /// 处理命中的箭头编号；自动接力不额外扣步，受阻的手动点击仍扣一步并播放回弹。
        /// </summary>
        /// <param name="bi">命中的箭头编号。</param>
        public void TapArrow(int bi)
        {
            if (IsPaused) return;
            if (IsChoosingErase)
            {
                SelectEraseArrow(bi);
                return;
            }
            if (bi < 0 || bi >= P.n || (Mask & (1 << bi)) == 0 || Over || (busy && tut >= 0) || now - built < P.n * .026f + .3f || (tut < 0 && Taps >= Limit))
                return;
            if (tut >= 0)
            {
                if (tutWait || TutorialIds[tut] != bi)
                    return;
                tutWait = true;
                hintId = -1;
                if (IsFree(P, Mask, bi))
                {
                    chainN = 0;
                    Step(bi);
                }
                else
                    BumpStart(bi, Mask);
                return;
            }

            if (q.Contains(bi) || bumps.Exists(b => b.id == bi))
                return;
            // Other manual taps may overlap flights, bumps and automatic chains.
            // Only the targeted eraser animation owns exclusive board input.
            if (IsErasing)
                return;
            if (UsesChainGuide && (!Idle || !CanStartGuidedChain(bi)))
                return;
            hintId = -1;
            Taps++;
            int em = Mask;
            foreach (int i in q)
                em &= ~(1 << i);
            if (IsFree(P, em, bi))
            {
                if (!busy)
                    chainN = 0;
                Step(bi);
            }
            else
                BumpStart(bi, em);
        }


        /// <summary>场景中重试按钮的持久化事件入口。</summary>
        public void Retry()
        {
            if (enabled && root && P != null && LevelCount > 0 && !generating && tut < 0)
                ResetGame();
        }

        /// <summary>场景中帮助按钮的持久化事件入口。</summary>
        public void OpenHelp()
        {
            if (enabled && root && P != null && LevelCount > 0 && !generating && tut < 0)
                StartTutorial();
        }

        /// <summary>结算主按钮入口；面板关闭后忽略同一帧重复点击。</summary>
        public void ContinueResult()
        {
            if (generating || !sheetVisible)
                return;
            if (!Won)
                ResetGame();
            else if (Endless)
                StartRandom();
            else if (IsLastCampaignLevel)
                StartEndless();
            else
                StartLevel(LevelIndex + 1);
        }

        /// <summary>推进教学文字步骤，最后一步恢复原棋局。</summary>
        public void AdvanceTutorial()
        {
            if (generating || tut < 3)
                return;
            if (tut < 5)
                TutorialStep(tut + 1);
            else
                EndTutorial();
        }

        /// <summary>
        /// 只处理棋盘折线命中；固定按钮由场景中的 EventSystem 与 Button 处理。
        /// </summary>
        /// <param name="p">相对于整个界面的逻辑像素点击位置。</param>
        private void Pointer(Vector2 p)
        {
            if (IsPaused || Time.frameCount == resumedFrame || generating || sceneView.BlocksBoardPointer(p, pixelScale))
                return;
            if (p.x < ox || p.x > ox + P.L.w * cs || p.y < oy || p.y > oy + P.L.h * cs)
                return;
            Vector2 g = (p - new Vector2(ox, oy)) / cs - Vector2.one * .5f;
            int bi = -1;
            float distance = .6f;
            foreach (var a in P.arrs)
            {
                if ((Mask & (1 << a.id)) == 0)
                    continue;
                for (int k = 0; k < a.cells.Length; k++)
                {
                    Vector2 c = a.cells[k].V;
                    Vector2 n = a.cells[Mathf.Min(k + 1, a.cells.Length - 1)].V;
                    Vector2 d = n - c;
                    float t = Mathf.Clamp01(Vector2.Dot(g - c, d) / (d.sqrMagnitude == 0 ? 1 : d.sqrMagnitude));
                    float v = (c + d * t - g).magnitude;
                    if (v < distance)
                    {
                        distance = v;
                        bi = a.id;
                    }
                }
            }

            if (bi >= 0)
                TapArrow(bi);
        }

        /// <summary>
        /// 只启动一次胜负流程；胜利时先播放庆祝和步数转分，再延迟显示结算面板。
        /// </summary>
        /// <param name="win">是否为清盘胜利。</param>
        private void Finish(bool win)
        {
            if (Over)
                return;
            Over = true;
            Won = win;
            CancelErase();
            endAt = now;
            remaining = Mathf.Max(0, Limit - Taps);
            if (!win)
            {
                After(.4f, ShowSheet);
                return;
            }

            punchAt = now;
            chipOverride = "Left " + remaining;
            HueAudio.Vibrate(20, 30, 60);
            int tier = remaining >= 2 ? 3 : remaining == 1 ? 2 : 1;
            for (int i = 0; i < tier + 2; i++)
            {
                double f = 392 * Math.Pow(2, VictoryNotes[i] / 12.0);
                After(i * .13f, () =>
                {
                    audioBus.Tone(f, .7, .09);
                    if (tier >= 2)
                        audioBus.Tone(f / 2, .8, .04);
                });
            }

            Spawn(new Vector2(P.L.w * cs / 2, P.L.h * cs / 2), Blue, 5, Vector2.down);
            Ring(new Vector2(P.L.w * cs / 2, P.L.h * cs / 2), Blue, cs * .3f, 15, .9f, 3);
            Confetti(tier);
            // 庆祝与奖励转换衔接，避免结算前长时间空等。
            if (remaining == 0)
                After(.8f, ShowSheet);
            else
                for (int i = 0; i < remaining; i++)
                {
                    int k = i;
                    After(.65f + i * .12f, () =>
                    {
                        tokens.Add(new Token { t = now, from = chipRect.center - new Vector2(ox, oy), to = scoreRect.center - new Vector2(ox, oy), i = k, total = remaining });
                        bonusLaunched++;
                        chipOverride = "Left " + (remaining - k - 1);
                    });
                }
        }

        /// <summary>
        /// 显示唯一的结算面板；胜利时在奖励落点全部计分后保存最高分，不保存关卡进度。
        /// </summary>
        private void ShowSheet()
        {
            if (sheetVisible || !Over)
                return;
            if (Won)
            {
                record = Score > bestScore;
                if (record)
                {
                    bestScore = Score;
                    PlayerPrefs.SetInt(Endless ? "cc_best" : campaignBestPrefix + LevelIndex, Score);
                }

                PlayerPrefs.Save();
                chipOverride = null;
            }

            sheetVisible = true;
            modalAt = now;
        }

        /// <summary>
        /// 驱动布局、延迟任务、接力队列、输入和动画；等待棋盘完全静止后才判定结算。
        /// </summary>
        private void Update()
        {
            if (mainMenu && mainMenu.IsVisible) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused) ResumePause(); else OpenPause();
                return;
            }
            if (IsPaused) { Layout(); Render(); return; }
            now = GameTime;
            dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            Layout();
            // 先移除再执行；即使回调触发重置，旧任务也不会在随后再次执行。
            for (int i = 0; i < timed.Count;)
            {
                if (timed[i].at <= now)
                {
                    var action = timed[i].action;
                    timed.RemoveAt(i);
                    action();
                }
                else
                    i++;
            }

            if (nextChain >= 0 && now >= nextChain)
            {
                nextChain = -1;
                if (q.Count > 0)
                    Step(q.Dequeue());
            }

            if (Mathf.RoundToInt(ds) != Score)
            {
                ds += (Score - ds) * Mathf.Min(1, dt * 9);
                if (Mathf.Abs(Score - ds) < 1)
                    ds = Score;
            }

            progress = Mathf.Lerp(progressFrom, progressTarget, Mathf.SmoothStep(0, 1, (now - progressAt) / .35f));
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        Vector2 pt = new Vector2(touch.position.x, Screen.height - touch.position.y) / pixelScale;
                        Pointer(pt);
                    }
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 pt = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / pixelScale;
                    Pointer(pt);
                }

            }

            UpdateErase();
            UpdateEffects();
            if (!Over && Idle && !IsChoosingErase && tut < 0 && (Mask == 0 || Taps >= Limit))
            {
                if (finishAt < 0)
                    finishAt = now;
                if (now - finishAt > .35f)
                    Finish(Mask == 0);
            }
            else
                finishAt = -1;
            if (tut >= 0 && tutWait && !busy && flies.Count == 0 && bumps.Count == 0)
            {
                if (tutIdle < 0)
                    tutIdle = now;
                if (now - tutIdle > .45f)
                    TutorialStep(tut + 1);
            }

            Render();
        }

        /// <summary>
        /// 通过二分求解三次贝塞尔曲线的 x 参数，复刻 CSS cubic-bezier 的缓动输出。
        /// </summary>
        /// <param name="x">归一化动画时间，范围为 0 到 1。</param>
        /// <param name="x1">第一个控制点的 x 坐标。</param>
        /// <param name="y1">第一个控制点的 y 坐标。</param>
        /// <param name="x2">第二个控制点的 x 坐标。</param>
        /// <param name="y2">第二个控制点的 y 坐标。</param>
        /// <returns>指定三次贝塞尔缓动曲线的纵坐标，可按原曲线发生超调。</returns>
        public static float Bezier(float x, float x1, float y1, float x2, float y2)
        {
            float lo = 0;
            float hi = 1;
            float t = x;
            for (int i = 0; i < 14; i++)
            {
                t = (lo + hi) * .5f;
                float v = 3 * (1 - t) * (1 - t) * t * x1 + 3 * (1 - t) * t * t * x2 + t * t * t;
                if (v < x)
                    lo = t;
                else
                    hi = t;
            }

            return 3 * (1 - t) * (1 - t) * t * y1 + 3 * (1 - t) * t * t * y2 + t * t * t;
        }

        /// <summary>
        /// 计算从 0 到 1 的三次减速曲线，用于原版逐段入场等动画。
        /// </summary>
        /// <returns>三次减速缓动值。</returns>
        private static float Ease(float t)
        {
            return 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        }

        /// <summary>
        /// 将网格坐标转换为棋盘局部逻辑像素，并加上半格偏移以对齐网点中心。
        /// </summary>
        /// <param name="p">预处理棋盘或局部绘制位置。</param>
        /// <returns>棋盘局部逻辑像素坐标。</returns>
        private Vector2 G(Vector2 p)
        {
            return (p + Vector2.one * .5f) * cs;
        }

        /// <summary>
        /// 取消剩余任务；固定材质属于场景资源，不在控制器销毁时删除。
        /// </summary>
        private void OnDestroy()
        {
            timed.Clear();
            StopAllCoroutines();
        }
    }
}
