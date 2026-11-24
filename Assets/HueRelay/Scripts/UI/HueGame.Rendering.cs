using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 游戏控制器的表现部分：按 HTML 的图层顺序绘制界面、棋盘、动态特效和底部面板。
    /// </summary>
    public sealed partial class HueGame
    {
        private int drawnScore = -1;
        private int drawnMovesLeft = -1;
        private int drawnBest = -1;
        private int drawnChain = -1;
        private int drawnLevel = -2;
        private int drawnTut = -2;
        private bool drawnEndless;
        private float cachedModal = -1;
        private string scoreText;
        private string movesText;
        private string bestText;
        private string chainText;
        private string titleText;
        private string resultTitle;
        private string resultDetail;
        private string resultMain;
        private string resultSecondary;
        private string resultScore;
        private Puzzle renderedPuzzle;
        private int renderedMask = -1;
        private int renderedHint = -2;
        private float renderedCellSize;
        private Vector2 renderedOrigin;
        private float renderedZoom;
        private bool boardWasDynamic;
        private bool effectsWereVisible;

        /// <summary>
        /// 按原页面层级提交一帧绘制；棋盘震动、胜利放大和固定界面分别使用各自变换。
        /// </summary>
        private void Render()
        {
            UpdateCampaignGuide();
            RenderChrome();
            float pt = (now - punchAt) / .52f;
            float ps = pt >= 0 && pt < 1 ? 1 + .05f * Mathf.Sin(pt * Mathf.PI) * (1 - .35f * pt) : 1;
            sceneView.boardFrame.localScale = Vector3.one * ps;
            Vector2 shake = Vector2.zero;
            float st = now - shakeAt;
            if (st >= 0 && st <= .4f)
            {
                float k = Mathf.Exp(-st * 9);
                shake = new Vector2(Mathf.Sin(st * 70), Mathf.Cos(st * 55) * .6f) * shakeAmplitude * k * K;
            }
            Vector2 origin = new Vector2(ox, oy) + shake + new Vector2(P.L.w * cs, P.L.h * cs) * .5f * (1 - ps);
            bool dynamicBoard = IsErasing || ripples.Count > 0 || flies.Count > 0 || bumps.Count > 0 || charged.Count > 0
                || hintId >= 0 || now - built < P.n * .026f + .42f;
            // Rebuild once after the last animation, then retain the settled mesh.
            if (dynamicBoard || boardWasDynamic || renderedPuzzle != P || renderedMask != Mask
                || renderedHint != hintId || renderedCellSize != cs || renderedOrigin != origin || renderedZoom != ps)
            {
                board.Begin();
                board.origin = origin;
                board.zoom = ps;
                RenderGrid();
                RenderArrows();
                board.End();
                renderedPuzzle = P;
                renderedMask = Mask;
                renderedHint = hintId;
                renderedCellSize = cs;
                renderedOrigin = origin;
                renderedZoom = ps;
            }
            boardWasDynamic = dynamicBoard;
            bool effectsVisible = conf.Count > 0 || tokens.Count > 0 || beads.Count > 0 || rings.Count > 0
                || particles.Count > 0 || floats.Count > 0;
            if (effectsVisible || effectsWereVisible)
            {
                fx.Begin();
                fx.origin = origin;
                fx.zoom = ps;
                RenderEffects();
                fx.End();
            }
            effectsWereVisible = effectsVisible;
            Vector2 erasePosition = IsErasing
                ? origin + G(At(P.arrs[erasingArrow], EraseProgress * (P.arrs[erasingArrow].cells.Length - 1))) * ps
                : Vector2.zero;
            sceneView.SetErasePresentation(IsChoosingErase, IsErasing, erasePosition, EraseProgress, K);
            RenderCombo();
            RenderTutorial();
            RenderSheet();
            float flashAlpha = Won && Over && now - endAt < .52f
                ? .45f * (1 - Bezier(Mathf.Clamp01((now - endAt) / .52f), 0, 0, .58f, 1)) : 0;
            sceneView.victoryFlash.color = new Color(1, 1, 1, flashAlpha);
        }

        /// <summary>
        /// 绘制顶部按钮、滚动分数、最高分、关卡标题、步数和缓动进度条。
        /// </summary>
        private void RenderChrome()
        {
            float scoreT = (now - scorePop) / .38f;
            float scoreScale = 1;
            Color scoreColor = Ink;
            if (scoreT >= 0 && scoreT < 1)
            {
                float t = Bezier(scoreT, 0, 0, .58f, 1);
                float f = t < .3f ? t / .3f : 1 - (t - .3f) / .7f;
                scoreScale = Mathf.LerpUnclamped(1, scorePopAmount, f);
                scoreColor = Color.Lerp(Ink, Blue, f);
            }
            int displayScore = Mathf.RoundToInt(ds);
            if (displayScore != drawnScore)
            {
                drawnScore = displayScore;
                scoreText = displayScore.ToString();
            }
            float scoreW = scoreText.Length * 24.376f * K;
            if (bestScore != drawnBest)
            {
                drawnBest = bestScore;
                bestText = "Best " + bestScore;
            }
            if (drawnLevel != LevelIndex || drawnTut != tut || drawnEndless != Endless)
            {
                drawnLevel = LevelIndex;
                drawnTut = tut;
                drawnEndless = Endless;
                titleText = tut >= 0 ? "How to Play" : Endless ? "Endless" : "Level " + (LevelIndex + 1);
            }
            RevealAddedMoves();
            int movesLeft = Mathf.Max(0, Limit - Taps - movesPendingReveal);
            float movePulseTime = (now - moveCountPulseAt) / MoveCountStepSeconds;
            float movePulse = movePulseTime >= 0 && movePulseTime < 1 ? Mathf.Sin(movePulseTime * Mathf.PI) : 0;
            if (movesLeft != drawnMovesLeft)
            {
                drawnMovesLeft = movesLeft;
                movesText = "Left " + movesLeft;
            }
            sceneView.SetHud(scoreText, scoreColor, scoreScale, scoreW / K, bestText, titleText,
                chipOverride ?? movesText, movePulse > 0 ? Blue : movesLeft <= 2 ? Orange : Ink,
                progress, tut >= 0, chipRect.width / K, P.n - HueEngine.PC(Mask), P.n, Won && Over, 1 + .1f * movePulse);
            sceneView.SetPowerUps(hints, eraseUses, extraMoveUses, CanRequestHint, CanRequestErase, CanRequestExtraMoves, IsChoosingErase);
        }

        /// <summary>
        /// 绘制棋盘网点，并叠加传播波对其颜色、尺寸和位置的影响。
        /// </summary>
        private void RenderGrid()
        {
            for (int x = 0; x < P.L.w; x++)
                for (int y = 0; y < P.L.h; y++)
                {
                    Vector2 p = G(new Vector2(x, y));
                    Vector2 offset = Vector2.zero;
                    float max = 0;
                    float sum = 0;
                    Color c = Dot;
                    foreach (var r in ripples)
                    {
                        Vector2 d;
                        float f = RippleForce(r, p, out d);
                        sum += f;
                        if (f > max)
                        {
                            max = f;
                            c = r.c;
                            offset = d * cs * .11f * f;
                        }
                    }

                    board.Disc(p + offset, cs * (.04f + .065f * Mathf.Min(1, max + .3f * (sum - max))), max > .12f ? c : Dot);
                }
        }

        /// <summary>
        /// 绘制路径的指定弧长窗口及圆角三角箭头，复用于入场、滑动飞行、碰撞和激活扫光。
        /// </summary>
        /// <param name="s">接收当前路径几何的绘制层。</param>
        /// <param name="a">原始折线箭头。</param>
        /// <param name="start">路径起始距离或圆弧起始角度。</param>
        /// <param name="end">路径终止距离或圆弧终止角度。</param>
        /// <param name="c">颜色代码或绘制颜色。</param>
        /// <param name="width">线宽或卡片可用宽度，单位为逻辑像素。</param>
        /// <param name="headSize">箭头头部相对于原始尺寸的倍率。</param>
        /// <param name="jitter">仅沿 x 方向附加的碰撞抖动，单位为逻辑像素。</param>
        /// <param name="arrowhead">是否绘制箭头头部；白色激活扫光关闭此项。</param>
        private void Path(HueSurface s, Arrow a, float start, float end, Color c, float width, float headSize = 1, float jitter = 0, bool arrowhead = true)
        {
            if (end <= start)
                return;
            Vector2 prev = G(At(a, start)) + Vector2.right * jitter;
            s.Disc(prev, cs * width * .5f, c);
            int first = Mathf.FloorToInt(start) + 1;
            for (int i = first; i < end; i++)
            {
                // Interior cells on the same straight run do not need a new stroke or cap.
                if (Tangent(a, i - 1) == Tangent(a, i))
                    continue;
                Vector2 v = G(At(a, i)) + Vector2.right * jitter;
                s.Stroke(prev, v, cs * width, c, false);
                s.Disc(v, cs * width * .5f, c);
                prev = v;
            }

            Vector2 head = G(At(a, end)) + Vector2.right * jitter;
            s.Stroke(prev, head, cs * width, c, false);
            s.Disc(head, cs * width * .5f, c);
            if (!arrowhead)
                return;
            Vector2 d = Tangent(a, end) * headSize;
            Vector2 n = new Vector2(-d.y, d.x);
            Vector2 p0 = head + d * cs * .3f;
            Vector2 p1 = head - d * cs * .04f + n * cs * .15f;
            Vector2 p2 = head - d * cs * .04f - n * cs * .15f;
            s.Triangle(p0, p1, p2, c);
            s.Stroke(p0, p1, cs * .17f * headSize, c, false);
            s.Stroke(p1, p2, cs * .17f * headSize, c, false);
            s.Stroke(p2, p0, cs * .17f * headSize, c, false);
            s.Disc(p0, cs * .085f * headSize, c);
            s.Disc(p1, cs * .085f * headSize, c);
            s.Disc(p2, cs * .085f * headSize, c);
        }

        /// <summary>
        /// 根据入场、提示、接力激活、碰撞和飞行状态绘制全部箭头。
        /// </summary>
        private void RenderArrows()
        {
            foreach (var a in P.arrs)
            {
                if ((Mask & (1 << a.id)) == 0)
                    continue;
                if (a.id == erasingArrow)
                {
                    float p = EraseProgress;
                    Path(board, a, p * (a.cells.Length - 1), a.cells.Length - 1,
                        HueSurface.Alpha(Col(a.c), 1 - p * .25f), .14f, Mathf.Clamp01((1 - p) * 5));
                    continue;
                }
                int bump = -1;
                for (int i = 0; i < bumps.Count; i++)
                    if (bumps[i].id == a.id)
                    {
                        bump = i;
                        break;
                    }

                if (bump >= 0)
                {
                    var b = bumps[bump];
                    Path(board, a, b.s, b.s + a.cells.Length - 1, b.hit ? Red : Col(a.c), .14f, 1, b.jitter);
                    continue;
                }

                float wd = .14f;
                float ct;
                bool hasTime = chargeAt.TryGetValue(a.id, out ct);
                if (charged.Contains(a.id))
                {
                    wd = .14f + .05f * (.5f + .5f * Mathf.Sin(now / .055f));
                    if (hasTime)
                    {
                        float p = Mathf.Min(1, (now - ct) / .4f);
                        board.Arc(G(a.Head), cs * (.95f - .7f * p), 0, Mathf.PI * 2, 2 * K, Col(a.c));
                    }
                }

                if (hintId == a.id && now >= hintAt)
                {
                    float el = now - hintAt;
                    float p = el % .9f / .9f;
                    float he = Ease(el / .45f);
                    float fade = tut < 0 && !UsesChainGuide && el > 3.2f ? Mathf.Max(0, (3.6f - el) / .4f) : 1;
                    board.Arc(G(a.Head), cs * (.3f + p * .9f), 0, Mathf.PI * 2, 2 * K, HueSurface.Alpha(Blue, (1 - p) * Mathf.Min(1, el / .3f) * fade));
                    wd = .14f + .06f * he * fade;
                    if (tut < 0 && !UsesChainGuide && el > 3.6f)
                        hintId = -1;
                }

                float bp = Mathf.Clamp01((now - built - a.id * .026f) / .42f);
                float e = Ease(bp);
                float n = a.cells.Length - 1;
                if (e < 1)
                {
                    if (e > 0)
                        Path(board, a, 0, e * n, Col(a.c), wd, Mathf.Min(1, .2f + e * 5));
                }
                else
                {
                    Path(board, a, 0, n, Col(a.c), wd, wd / .14f);
                    if (charged.Contains(a.id) && hasTime)
                    {
                        float tc = (now - ct) / .4f;
                        if (tc > .3f)
                        {
                            float u = Mathf.Min(1, (tc - .3f) / .7f) * a.cells.Length;
                            float end = Mathf.Min(a.cells.Length - 1, u);
                            float start = Mathf.Max(0, u - 1.3f);
                            if (end > start)
                                Path(board, a, start, end, Color.white, .08f, arrowhead: false);
                        }
                    }
                }
            }

            foreach (var f in flies)
                Path(board, f.a, f.s, f.s + f.a.cells.Length - 1, Col(f.a.c), .14f + Mathf.Min(.04f, f.s * .012f), 1 + Mathf.Min(.3f, f.s * .09f));
        }

        /// <summary>
        /// 计算与原版接力光点一致的三次加减速曲线。
        /// </summary>
        /// <param name="p">0 到 1 的归一化时间。</param>
        /// <returns>三次加减速缓动值。</returns>
        private static float CubicInOut(float p)
        {
            return p < .5f ? 4 * p * p * p : 1 - Mathf.Pow(-2 * p + 2, 3) / 2;
        }

        /// <summary>
        /// 计算二次贝塞尔轨迹上的位置，用于步数奖励飞向分数的弧线。
        /// </summary>
        /// <param name="a">轨迹起点。</param>
        /// <param name="b">轨迹控制点。</param>
        /// <param name="c">轨迹终点。</param>
        /// <param name="u">二次贝塞尔插值参数。</param>
        /// <returns>二次贝塞尔轨迹上的位置。</returns>
        private static Vector2 Quadratic(Vector2 a, Vector2 b, Vector2 c, float u)
        {
            return (1 - u) * (1 - u) * a + 2 * (1 - u) * u * b + u * u * c;
        }

        /// <summary>
        /// 绘制彩点、奖励拖尾、接力光点、扩散环、粒子和带白描边的加分文字。
        /// </summary>
        private void RenderEffects()
        {
            foreach (var c in conf)
                if (c.t >= 0)
                    fx.Disc(c.p, cs * .16f * c.size, HueSurface.Alpha(c.c, Mathf.Min(1, (c.life - c.t) / .6f)));
            foreach (var t in tokens)
            {
                float p = Mathf.Min(1, (now - t.t) / .55f);
                float e = p < .5f ? 2 * p * p : 1 - Mathf.Pow(-2 * p + 2, 2) / 2;
                var mid = new Vector2((t.from.x + t.to.x) / 2, Mathf.Min(t.from.y, t.to.y) - cs * 2.2f);
                for (int j = 4; j >= 0; j--)
                {
                    float u = Mathf.Max(0, e - j * .045f);
                    fx.Disc(Quadratic(t.from, mid, t.to, u), cs * (j > 0 ? .15f - j * .02f : .19f), HueSurface.Alpha(Orange, j > 0 ? .45f - j * .07f : 1));
                }
            }

            foreach (var b in beads)
            {
                float p = (now - b.t) / .4f;
                Vector2 delta = b.to - b.from;
                float len = delta.magnitude;
                if (len == 0)
                    len = 1;
                Vector2 n = new Vector2(-delta.y, delta.x) / len;
                for (int j = 5; j >= 0; j--)
                {
                    float e = CubicInOut(Mathf.Max(0, p - j * .035f));
                    float arc = Mathf.Sin(Mathf.PI * e) * .7f * b.s;
                    Vector2 v = G(b.from + delta * e + n * arc);
                    fx.Disc(v, cs * (j > 0 ? .14f - j * .018f : .17f), HueSurface.Alpha(b.c, j > 0 ? .5f - j * .07f : 1));
                    if (j == 0)
                        fx.Disc(v, cs * .07f, Color.white);
                }
            }

            foreach (var r in rings)
            {
                Color c = HueSurface.Alpha(r.c, r.life);
                float w = r.width * K;
                if (r.type == '\0')
                    fx.Arc(r.p, r.radius, 0, Mathf.PI * 2, w, c);
                else if (r.type == 'a')
                {
                    float g = Mathf.PI * 2 / r.n;
                    for (int i = 0; i < r.n; i++)
                    {
                        float a = r.phase + i * g;
                        fx.Arc(r.p, r.radius, a, a + g * .62f, w, c);
                    }
                }
                else if (r.type == 'f')
                {
                    float a = Mathf.Atan2(r.d.y, r.d.x);
                    fx.Arc(r.p, r.radius, a - r.sweep, a + r.sweep, w, c);
                }
                else if (r.type == 'k')
                {
                    float l = cs * (.25f + .9f * r.life);
                    for (int i = 0; i < r.n; i++)
                    {
                        float a = r.phase + i * Mathf.PI * 2 / r.n;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        fx.Stroke(r.p + d * r.radius, r.p + d * (r.radius + l), w, c);
                        if (i % 2 == 0)
                            fx.Disc(r.p + d * (r.radius + l + cs * .18f), cs * .045f, c);
                    }
                }
                else
                {
                    for (int i = 0; i < r.n; i++)
                    {
                        float a = r.phase + i * Mathf.PI * 2 / r.n;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 n = new Vector2(-d.y, d.x);
                        Vector2 v = r.p + d * r.radius;
                        float s = cs * .2f;
                        Vector2 p0 = v + d * s;
                        Vector2 p1 = v - d * s * .4f + n * s * .7f;
                        Vector2 p2 = v - d * s * .4f - n * s * .7f;
                        fx.Triangle(p0, p1, p2, c);
                        fx.Stroke(p0, p1, cs * .07f, c, false);
                        fx.Stroke(p1, p2, cs * .07f, c, false);
                        fx.Stroke(p2, p0, cs * .07f, c, false);
                        fx.Disc(p0, cs * .035f, c);
                        fx.Disc(p1, cs * .035f, c);
                        fx.Disc(p2, cs * .035f, c);
                    }
                }
            }

            foreach (var p in particles)
                fx.Disc(p.p, p.size, HueSurface.Alpha(p.c, p.life));
            foreach (var f in floats)
            {
                float a = f.age;
                float scale = a < .12f ? .5f + a / .12f * 1.1f : a < .3f ? 1.6f - (a - .12f) / .18f * .4f : 1.2f;
                float rot = Mathf.Sin(a * 20) * .14f * Mathf.Max(0, 1 - a / .7f);
                float y = f.p.y - (a > .9f ? (a - .9f) * cs * 2.2f : 0);
                float alpha = a > 1 ? Mathf.Max(0, 1 - (a - 1) / .4f) : 1;
                fx.Label(f.text, new Vector2(f.p.x, y), Mathf.Round(cs * (.66f + Mathf.Min(f.k, 6) * .09f)), HueSurface.Alpha(f.c, alpha), true, scale: scale, rotation: rot, outline: 5 * K, serifStyle: 2);
            }
        }

        /// <summary>
        /// 绘制连锁倍数和 5、8、12 连锁里程碑，并保留各自的原版关键帧。
        /// </summary>
        private void RenderCombo()
        {
            if (chainN != drawnChain)
            {
                drawnChain = chainN;
                chainText = "Chain ×" + chainN;
            }

            float c = now - comboAt;
            if (c >= 0 && c < 1)
            {
                float e = Bezier(c, 0, 0, .58f, 1);
                float scale = e < .25f ? Mathf.Lerp(.7f, 1.3f, e / .25f) : Mathf.Lerp(1.3f, 1, (e - .25f) / .75f);
                float alpha = e < .25f ? 1 : 1 - (e - .25f) / .75f;
                sceneView.combo.text = chainText;
                sceneView.combo.color = comboColor;
                HueView.SetFloating(sceneView.comboGroup, new Vector2(appX + appW / 2, stageY + stageH * .03f + 19 * K), K * scale, alpha);
            }

            else if (sceneView.comboGroup.gameObject.activeSelf)
                sceneView.comboGroup.gameObject.SetActive(false);
            float m = (now - mileAt) / 1.5f;
            if (m >= 0 && m < 1)
            {
                float e = Bezier(m, .3f, .9f, .4f, 1);
                float alpha;
                float scale;
                float y;
                if (e < .22f)
                {
                    float p = e / .22f;
                    alpha = p;
                    scale = Mathf.Lerp(.6f, 1.14f, p);
                    y = Mathf.Lerp(14, 0, p);
                }
                else if (e < .4f)
                {
                    alpha = 1;
                    scale = Mathf.Lerp(1.14f, 1, (e - .22f) / .18f);
                    y = 0;
                }
                else if (e < .75f)
                {
                    alpha = 1;
                    scale = 1;
                    y = 0;
                }
                else
                {
                    alpha = 1 - (e - .75f) / .25f;
                    scale = 1;
                    y = Mathf.Lerp(0, -18, (e - .75f) / .25f);
                }

                Color col = HueSurface.Alpha(mileColor, alpha);
                Vector2 p0 = new Vector2(appX + appW / 2, stageY + stageH * .34f + 42 * K + y);
                sceneView.milestoneNumber.text = mileN == 5 ? "×5" : mileN == 8 ? "×8" : "×12";
                sceneView.milestoneNumber.color = sceneView.milestoneCaption.color = mileColor;
                sceneView.milestoneCaption.text = mileN == 5 ? "Nice!" : mileN == 8 ? "Amazing!" : "Legendary!";
                HueView.SetFloating(sceneView.milestoneGroup, p0, K * scale, alpha);
            }
            else if (sceneView.milestoneGroup.gameObject.activeSelf)
                sceneView.milestoneGroup.gameObject.SetActive(false);
        }

        /// <summary>
        /// 绘制教学底部卡片和手势；缓存换行结果，避免每帧重复创建字符串。
        /// </summary>
        private void RenderTutorial()
        {
            bool showFinger = now >= hintAt && ((tut >= 0 && tut < 3 && !tutWait)
                || (UsesChainGuide && Idle && !Over && guideArrow >= 0 && hintId == guideArrow));
            if (finger.gameObject.activeSelf != showFinger)
                finger.gameObject.SetActive(showFinger);
            if (tut < 0)
            {
                if (sceneView.tutorialGroup.gameObject.activeSelf)
                    sceneView.tutorialGroup.gameObject.SetActive(false);
            }
            else
            {
                float p = Bezier(Mathf.Clamp01((now - tutStepAt) / .35f), .3f, 1.4f, .5f, 1);
                Vector2? target = tut < 3 ? (Vector2?)(new Vector2(ox, oy) + G(P.arrs[TutorialIds[tut]].Head)) : null;
                sceneView.SetTutorial(TutorialText[tut], tut, K, p, target, cs * .7f);
            }

            if (showFinger)
            {
                var a = P.arrs[tut >= 0 ? TutorialIds[tut] : guideArrow];
                float t = (now - hintAt) % .9f / .9f;
                float y;
                float scale = 1;
                if (t < .45f)
                    y = Mathf.Lerp(10, 0, .5f - .5f * Mathf.Cos(t / .45f * Mathf.PI));
                else if (t < .6f)
                {
                    float u = (t - .45f) / .15f;
                    y = Mathf.Lerp(0, -4, u);
                    scale = Mathf.Lerp(1, .88f, u);
                }
                else
                {
                    float u = (t - .6f) / .4f;
                    y = Mathf.Lerp(-4, 10, u);
                    scale = Mathf.Lerp(.88f, 1, u);
                }

                Vector2 pos = new Vector2(ox, oy) + G(a.Head) + new Vector2(10 * K, cs * .3f + (y - 14) * K);
                finger.gameObject.SetActive(true);
                finger.rectTransform.anchoredPosition = new Vector2(pos.x, -pos.y - 60 * K);
                finger.rectTransform.sizeDelta = new Vector2(52 * K, 60 * K);
                finger.rectTransform.localScale = Vector3.one * scale;
                finger.color = HueSurface.Alpha(Color.white, Mathf.Clamp01((now - hintAt) / .4f));
            }
        }

        /// <summary>
        /// 显示胜负结算卡片，并以平稳的淡入过渡同步按钮命中区域。
        /// </summary>
        private void RenderSheet()
        {
            if (!sheetVisible)
            {
                if (sceneView.resultGroup.gameObject.activeSelf)
                    sceneView.resultGroup.gameObject.SetActive(false);
                if (sceneView.resultBackdrop.gameObject.activeSelf)
                    sceneView.resultBackdrop.gameObject.SetActive(false);
                return;
            }
            if (cachedModal != modalAt)
            {
                cachedModal = modalAt;
                resultTitle = Won ? (Endless ? "Board cleared!" : "Level " + (LevelIndex + 1) + " complete!") : "Out of moves";
                resultScore = Score.ToString();
                resultDetail = Won ? (record ? "NEW BEST" : "TOTAL SCORE") : "";
                resultMain = !Won ? "Retry" : Endless ? "New Board" : IsLastCampaignLevel ? "Endless Mode" : "Next Level";
                resultSecondary = "Home";
            }

            float p = 1 - Mathf.Pow(1 - Mathf.Clamp01((now - modalAt) / .42f), 3);
            sceneView.SetResult(Won, record, K, p, resultTitle, resultScore,
                resultDetail, resultMain, resultSecondary, revivePending, reviveUnavailable);
        }
    }
}
