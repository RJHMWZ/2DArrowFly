using System.Collections.Generic;
using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 游戏控制器的特效模拟部分：以值类型列表维护飞行、碰撞、波纹、粒子和奖励飞行轨迹。
    /// </summary>
    public sealed partial class HueGame
    {
        /// <summary>
        /// 沿原折线滑动并沿箭头方向延伸的飞行状态；s 的单位为网格边长。
        /// </summary>
        struct Flight
        {
            public Arrow a;
            public float s;
            public int k;
            public int points;
            public bool exited;
        }

        /// <summary>
        /// 无效点击的三段动画状态：前冲、碰撞停顿和回弹。
        /// </summary>
        struct Bump
        {
            public int id;
            public float t;
            public float dist;
            public float s;
            public float jitter;
            public bool hit;
        }

        /// <summary>
        /// 离场或碰撞爆发的圆点粒子；位置、速度均使用棋盘局部逻辑像素。
        /// </summary>
        struct Particle
        {
            public Vector2 p;
            public Vector2 v;
            public float life;
            public float size;
            public float gravity;
            public Color c;
        }

        /// <summary>
        /// 影响网格圆点的传播波；与可见圆环分开模拟，支持径向、方向性和花瓣形波前。
        /// </summary>
        struct Ripple
        {
            public Vector2 p;
            public Vector2 d;
            public float t;
            public float a;
            public float speed;
            public float width;
            public float life;
            public float rotation;
            public char type;
            public Color c;
        }

        /// <summary>
        /// 可见扩散圆环或冲击波；life 从 1 递减至 0，同时控制透明度。
        /// </summary>
        struct ExpandingRing
        {
            public Vector2 p;
            public Vector2 d;
            public float radius;
            public float life;
            public float speed;
            public float rate;
            public float width;
            public float phase;
            public float spin;
            public float sweep;
            public int n;
            public char type;
            public Color c;
        }

        /// <summary>
        /// 离场加分文字的短生命周期状态，保存弹性缩放、旋转和上浮所需的时间。
        /// </summary>
        struct FloatScore
        {
            public Vector2 p;
            public float age;
            public string text;
            public Color c;
            public int k;
        }

        /// <summary>
        /// 400 毫秒自动接力期间，从已移除箭头飞向目标箭头的带拖尾光点。
        /// </summary>
        struct Bead
        {
            public Vector2 from;
            public Vector2 to;
            public float t;
            public float s;
            public Color c;
        }

        /// <summary>
        /// 将一个剩余步数转化为 50 分的奖励粒子；抵达分数位置后才真正加分。
        /// </summary>
        struct Token
        {
            public Vector2 from;
            public Vector2 to;
            public float t;
            public int i;
            public int total;
        }

        /// <summary>
        /// 胜利彩点的运动状态。原 HTML 最终绘制的是圆点，因此这里保留其实际表现。
        /// </summary>
        struct Conf
        {
            public Vector2 p;
            public Vector2 v;
            public float t;
            public float life;
            public float drag;
            public float rotation;
            public float spin;
            public float flip;
            public float flipSpeed;
            public float sway;
            public float size;
            public Color c;
        }

        private readonly List<Flight> flies = new List<Flight>(32);
        private readonly List<Bump> bumps = new List<Bump>(30);
        private readonly List<Particle> particles = new List<Particle>(512);
        private readonly List<Ripple> ripples = new List<Ripple>(128);
        private readonly List<ExpandingRing> rings = new List<ExpandingRing>(128);
        private readonly List<FloatScore> floats = new List<FloatScore>(32);
        private readonly List<Bead> beads = new List<Bead>(32);
        private readonly List<Token> tokens = new List<Token>(32);
        private readonly List<Conf> conf = new List<Conf>(256);
        float Rand
        {
            get
            {
                return UnityEngine.Random.value;
            }
        }

        /// <summary>
        /// 清空各类可复用特效列表并停止屏幕震动；不销毁或重建粒子对象。
        /// </summary>
        private void ClearEffects()
        {
            flies.Clear();
            bumps.Clear();
            particles.Clear();
            ripples.Clear();
            rings.Clear();
            floats.Clear();
            beads.Clear();
            tokens.Clear();
            conf.Clear();
            shakeAt = -100;
        }

        /// <summary>
        /// 创建扩散环状态；同一数据结构支持完整圆环、分段弧、方向冲击和小箭头。
        /// </summary>
        /// <param name="p">棋盘局部逻辑像素中心。</param>
        /// <param name="c">圆环颜色。</param>
        /// <param name="radius">逻辑像素半径。</param>
        /// <param name="speed">半径扩散速度，单位为格子边长每秒。</param>
        /// <param name="rate">生命值每秒减少量。</param>
        /// <param name="width">线宽或卡片可用宽度，单位为逻辑像素。</param>
        /// <param name="type">圆环类型：完整环、分段弧、方向冲击或小箭头。</param>
        /// <param name="n">分段或放射元素数量。</param>
        /// <param name="phase">初始旋转相位，单位为弧度。</param>
        /// <param name="spin">旋转角速度，单位为弧度每秒。</param>
        /// <param name="d">方向冲击使用的 Canvas 单位向量。</param>
        /// <param name="sweep">方向冲击弧的半张角，单位为弧度。</param>
        private void Ring(Vector2 p, Color c, float radius = -1, float speed = 5, float rate = 2.4f, float width = 2.5f, char type = '\0', int n = 0, float phase = 0, float spin = 0, Vector2 d = default(Vector2), float sweep = 0)
        {
            rings.Add(new ExpandingRing { p = p, c = c, radius = radius < 0 ? cs * .2f : radius, life = 1, speed = speed, rate = rate, width = width, type = type, n = n, phase = phase, spin = spin, d = d, sweep = sweep });
        }

        /// <summary>
        /// 按原版随机角度、速度和粒径创建一组离场或碰撞圆点。
        /// </summary>
        /// <param name="p">爆发中心，单位为棋盘局部逻辑像素。</param>
        /// <param name="c">颜色代码或绘制颜色。</param>
        /// <param name="n">粒子数量。</param>
        /// <param name="speed">基础速度，单位为逻辑像素每秒。</param>
        private void Burst(Vector2 p, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rand * 6.28f;
                float v = (.3f + Rand) * speed;
                particles.Add(new Particle { p = p, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, life = 1, c = c, size = (2 + Rand * 3) * K });
            }
        }

        /// <summary>
        /// 登记一条影响网格圆点的传播波，可指定延迟、方向、波宽和生命周期。
        /// </summary>
        /// <param name="p">波源的棋盘局部逻辑像素坐标。</param>
        /// <param name="c">波峰颜色。</param>
        /// <param name="d">波的传播基准方向。</param>
        /// <param name="type">r 为径向，l 为窄方向，s 为平面，f 为花瓣波。</param>
        /// <param name="amplitude">网点颜色、位移和尺寸变化的强度。</param>
        /// <param name="speed">波前速度，单位为格子边长每秒。</param>
        /// <param name="width">高斯波前宽度，单位为格子边长。</param>
        /// <param name="life">生命周期，单位为秒。</param>
        /// <param name="delay">延迟秒数。</param>
        /// <param name="rotation">花瓣波的初始旋转相位，单位为弧度。</param>
        private void AddRipple(Vector2 p, Color c, Vector2 d, char type = 'r', float amplitude = 1, float speed = 9, float width = 1.5f, float life = 1.4f, float delay = 0, float rotation = 0)
        {
            ripples.Add(new Ripple { p = p, c = c, d = d, type = type, a = amplitude, speed = speed, width = width, life = life, t = now + delay, rotation = rotation });
        }

        /// <summary>
        /// 对应 JavaScript spawn：按五档强度组合径向、窄方向、平面和花瓣波纹。
        /// </summary>
        /// <param name="p">波源的棋盘局部逻辑像素坐标。</param>
        /// <param name="c">主波纹颜色。</param>
        /// <param name="tier">从 1 到 5 的波纹组合强度。</param>
        /// <param name="d">离场箭头的 Canvas 方向。</param>
        private void Spawn(Vector2 p, Color c, int tier, Vector2 d)
        {
            Color c2 = Rand < .5f ? Blue : Orange;
            if (tier == 1)
                AddRipple(p, c, d, amplitude: .55f, speed: 7, width: 1.3f, life: 1);
            else if (tier == 2)
            {
                AddRipple(p, c, d, amplitude: .8f);
                AddRipple(p, c, d, amplitude: .5f, speed: 7, life: 1.2f, delay: .16f);
            }
            else if (tier == 3)
            {
                AddRipple(p, c, d, 'l', speed: 15, width: 1.1f, life: 1.1f);
                AddRipple(p, c, d, amplitude: .7f, delay: .1f);
            }
            else if (tier == 4)
            {
                AddRipple(p, c, d, 's', speed: 13, width: 2, life: 1.5f);
                AddRipple(p, c2, d, 's', amplitude: .6f, speed: 13, width: 2, life: 1.5f, delay: .2f);
                AddRipple(p, c, d, 'l', speed: 17, width: 1.1f, life: 1.1f);
            }
            else
            {
                AddRipple(p, c, d, 'f', speed: 8, width: 2.3f, life: 1.9f, rotation: Rand * 6);
                AddRipple(p, c2, d, 's', speed: 14, width: 2.4f, life: 1.6f);
                AddRipple(p, c2, d, 'f', amplitude: .8f, speed: 10, width: 1.6f, life: 1.7f, delay: .22f, rotation: Rand * 6);
            }
        }

        /// <summary>
        /// 对应 JavaScript exitRing：根据连锁次数组合离场圆环、旋转弧和方向性冲击波。
        /// </summary>
        /// <param name="p">离场位置的棋盘局部逻辑像素坐标。</param>
        /// <param name="c">箭头颜色。</param>
        /// <param name="k">当前连锁长度，从 1 开始。</param>
        /// <param name="d">箭头的 Canvas 离场方向。</param>
        private void ExitRing(Vector2 p, Color c, int k, Vector2 d)
        {
            if (k <= 2)
                Ring(p, c, speed: 5, rate: 2.6f, width: 1.8f);
            else if (k <= 5)
            {
                Ring(p, Ink, speed: 5.5f, rate: 2.8f, width: 1.6f);
                Ring(p, c, cs * .35f, 4.2f, 1.7f, 2.6f, 'a', 6, Rand * 6.28f, 2.2f);
            }
            else
            {
                Ring(p, c, cs * .35f, 4.6f, 1.6f, 2.6f, 'a', 8, Rand * 6.28f, -2.6f);
                Ring(p, c, cs * .45f, 5.2f, 1.7f, 2.5f, 'm', 8, Rand * 6.28f);
                Ring(p, c, cs * .3f, 7, 2.4f, 3.4f, 'f', d: -d, sweep: 1.05f);
                After(.11f, () => Ring(p, c, cs * .2f, 7, 2.2f, 2.4f, 'f', d: -d, sweep: 1.05f));
            }
        }

        /// <summary>
        /// 触发指数衰减屏幕震动，并保留原版 160 毫秒的连续触发间隔。
        /// </summary>
        /// <param name="amplitude">原版逻辑像素震动幅度，随后乘以 CSS 缩放系数。</param>
        private void Shake(float amplitude)
        {
            if (now - lastShake < .16f)
                return;
            lastShake = now;
            shakeAt = now;
            shakeAmplitude = amplitude;
        }

        /// <summary>
        /// 寻找最近的有效阻挡格，建立前冲距离，并播放点击时的声音与圆环。
        /// </summary>
        /// <param name="id">箭头编号或按钮编号。</param>
        /// <param name="mask">用于阻挡检测的有效剩余掩码。</param>
        private void BumpStart(int id, int mask)
        {
            var a = P.arrs[id];
            float dist = a.ray.Count;
            for (int i = 0; i < a.ray.Count; i++)
            {
                bool found = false;
                foreach (var b in P.arrs)
                {
                    if ((mask & (1 << b.id)) == 0 || b.id == id)
                        continue;
                    foreach (var c in b.cells)
                        if (c.x == a.ray[i].x && c.y == a.ray[i].y)
                        {
                            found = true;
                            break;
                        }

                    if (found)
                        break;
                }

                if (found)
                {
                    dist = i;
                    break;
                }
            }

            bumps.Add(new Bump { id = id, t = now, dist = dist + .5f });
            Ring(G(a.Head), Col(a.c), cs * .15f, 2.6f, 3.6f, 2);
            audioBus.Blip(0);
            HueAudio.Vibrate(8);
        }

        /// <summary>
        /// 在原始折线及头部延长射线上按弧长采样；参数单位为网格边长。
        /// </summary>
        /// <param name="a">原始折线及离场朝向。</param>
        /// <param name="v">沿折线从尾部起算的距离，单位为网格边长。</param>
        /// <returns>折线或离场延长线上的网格坐标。</returns>
        private static Vector2 At(Arrow a, float v)
        {
            int n = a.cells.Length - 1;
            if (v >= n)
                return a.Head + a.d * (v - n);
            int i = Mathf.Clamp(Mathf.FloorToInt(v), 0, n - 1);
            return Vector2.LerpUnclamped(a.cells[i].V, a.cells[i + 1].V, v - i);
        }

        /// <summary>
        /// 返回路径指定位置的分段切线，保证转弯和离场时箭头朝向与原版一致。
        /// </summary>
        /// <param name="a">原始折线及离场朝向。</param>
        /// <param name="v">沿折线从尾部起算的距离，单位为网格边长。</param>
        /// <returns>所在路径段的单位方向。</returns>
        private static Vector2 Tangent(Arrow a, float v)
        {
            int i = Mathf.FloorToInt(v);
            return i >= a.cells.Length - 1 ? a.d : a.cells[i + 1].V - a.cells[Mathf.Max(0, i)].V;
        }

        /// <summary>
        /// 按胜利等级安排两侧喷射和十批顶部落下的彩点。
        /// </summary>
        /// <param name="tier">与连锁或胜利等级对应的效果档位。</param>
        private void Confetti(int tier)
        {
            int n = new[]
            {
                30,
                45,
                60
            }[tier - 1];
            int rain = new[]
            {
                15,
                25,
                40
            }[tier - 1];
            After(.9f, () => ConfettiFire(1, n));
            After(1.2f, () => ConfettiFire(-1, n));
            for (int k = 0; k < 10; k++)
                After(1.5f + k * .17f, () =>
                {
                    int m = Mathf.CeilToInt(rain / 10f);
                    for (int i = 0; i < m; i++)
                        MakeConf(new Vector2((i + Rand) / m * viewWidth, -10), new Vector2((Rand - .5f) * 80, viewHeight * .2f), 2.2f, Rand * .2f);
                });
        }

        /// <summary>
        /// 从屏幕一侧向上喷射彩点，同时播放原版的两段音效。
        /// </summary>
        /// <param name="direction">方向向量；方向喷射中用正负号区分两侧。</param>
        /// <param name="n">数量或上界，具体含义见方法说明。</param>
        private void ConfettiFire(int direction, int n)
        {
            audioBus.Tone(180, .14, .12);
            After(.03f, () => audioBus.Tone(330, .1, .05));
            HueAudio.Vibrate(10);
            for (int i = 0; i < n; i++)
                MakeConf(new Vector2(direction > 0 ? -12 : viewWidth + 12, viewHeight * (.9f + Rand * .1f)), new Vector2(direction * viewWidth * (.15f + Rand * 2.2f), -viewHeight * (1.3f + Rand * 2.2f)), 2.2f + Rand * .7f, Rand * .45f);
        }

        /// <summary>
        /// 创建一个带延迟、阻力、重力和横向摆动的胜利彩点。
        /// </summary>
        /// <param name="p">屏幕逻辑像素位置，将转换为棋盘局部坐标。</param>
        /// <param name="v">初速度，单位为逻辑像素每秒。</param>
        /// <param name="life">生命周期，单位为秒。</param>
        /// <param name="delay">延迟秒数。</param>
        private void MakeConf(Vector2 p, Vector2 v, float life, float delay)
        {
            float shape = Rand;
            conf.Add(new Conf { p = p - new Vector2(ox, oy), v = v, t = -delay, drag = .75f + Rand * .8f, life = life, c = new[] { Blue, Orange, Ink }[UnityEngine.Random.Range(0, 3)], rotation = Rand * 6.28f, spin = (Rand - .5f) * 10, flip = Rand * 6.28f, flipSpeed = 6 + Rand * 8, sway = (Rand - .5f) * 140, size = .45f + Mathf.Pow(Rand, 1.6f) * 1.7f });
        }

        /// <summary>
        /// 推进所有特效值类型状态并回收过期项；奖励粒子抵达时才增加分数。
        /// </summary>
        private void UpdateEffects()
        {
            for (int i = 0; i < bumps.Count;)
            {
                var b = bumps[i];
                var a = P.arrs[b.id];
                float el = now - b.t;
                float T1 = Mathf.Min(.4f, .1f + .035f * b.dist);
                float T2 = .1f;
                float T3 = Mathf.Min(.5f, .25f + .02f * b.dist);
                b.jitter = 0;
                if (el < T1)
                    b.s = b.dist * Mathf.Pow(el / T1, 2);
                else
                {
                    if (!b.hit)
                    {
                        b.hit = true;
                        Vector2 h = G(a.Head + a.d * (b.dist + .35f));
                        Burst(h, Red, 7, cs * 2);
                        Ring(h, Red, cs * .15f, 3, 3, 2);
                        audioBus.Tone(140, .2, .15);
                        HueAudio.Vibrate(30, 40);
                        Shake(2.5f);
                    }

                    if (el < T1 + T2)
                    {
                        b.s = b.dist;
                        b.jitter = Mathf.Sin(el * 120) * cs * .03f;
                    }
                    else
                    {
                        float p = Mathf.Min(1, (el - T1 - T2) / T3);
                        b.s = b.dist * Mathf.Pow(1 - p, 3);
                        if (p >= 1)
                        {
                            bumps.RemoveAt(i);
                            continue;
                        }
                    }
                }

                bumps[i++] = b;
            }

            for (int i = 0; i < flies.Count;)
            {
                var f = flies[i];
                f.s += dt * Mathf.Min(36, 4 + f.s * 3.4f);
                Vector2 head = At(f.a, f.s + f.a.cells.Length - 1);
                if (!f.exited && (head.x < -.5f || head.y < -.5f || head.x > P.L.w - .5f || head.y > P.L.h - .5f))
                {
                    f.exited = true;
                    Vector2 p = G(head);
                    p.x = Mathf.Clamp(p.x, 0, P.L.w * cs);
                    p.y = Mathf.Clamp(p.y, 0, P.L.h * cs);
                    Color c = Col(f.a.c);
                    Burst(p, c, Mathf.Min(10 + f.k * 2, 22), cs * (3 + Mathf.Min(f.k, 8) * .5f));
                    ExitRing(p, c, f.k, f.a.d);
                    Spawn(p, c, f.k >= 9 ? 5 : f.k >= 6 ? 4 : f.k >= 4 ? 3 : f.k >= 2 ? 2 : 1, f.a.d);
                    floats.Add(new FloatScore { p = new Vector2(Mathf.Clamp(p.x, cs, P.L.w * cs - cs), Mathf.Clamp(p.y, cs * 1.2f, P.L.h * cs - cs * .5f)), text = "+" + f.points, c = c, k = f.k });
                    if (f.k > 1)
                        Shake(Mathf.Min(1.2f * (f.k - 1), 7));
                    HueAudio.Vibrate(6 + f.k * 3);
                }

                Vector2 tail = new Vector2(ox, oy) + G(At(f.a, f.s));
                if (f.exited && (tail.x < -cs || tail.x > viewWidth + cs || tail.y < -cs || tail.y > viewHeight + cs))
                {
                    flies.RemoveAt(i);
                    continue;
                }

                flies[i++] = f;
            }

            for (int i = 0; i < conf.Count;)
            {
                var p = conf[i];
                p.t += dt;
                if (p.t < 0)
                {
                    conf[i++] = p;
                    continue;
                }

                if (p.t >= p.life)
                {
                    conf.RemoveAt(i);
                    continue;
                }

                p.v.y += viewHeight * .9f * dt;
                float k = Mathf.Exp(-2.2f * p.drag * dt);
                p.v *= k;
                p.p += new Vector2(p.v.x + Mathf.Sin(p.t * 3 + p.flip) * p.sway, p.v.y) * dt;
                p.rotation += p.spin * dt;
                p.flip += p.flipSpeed * dt;
                conf[i++] = p;
            }

            for (int i = 0; i < tokens.Count;)
            {
                var t = tokens[i];
                if (now - t.t >= .55f)
                {
                    Score += 50;
                    Pop(1.22f + t.i * .04f);
                    audioBus.Blip(t.i);
                    Ring(t.to, Orange, cs * .2f, 3.2f, 3, 2);
                    bonusLanded++;
                    if (t.i == t.total - 1)
                        After(.15f, ShowSheet);
                    tokens.RemoveAt(i);
                    continue;
                }

                i++;
            }

            for (int i = 0; i < beads.Count;)
            {
                var b = beads[i];
                if (now - b.t >= .4f)
                {
                    Ring(G(b.to), b.c, cs * .15f, 2.6f, 3.6f, 2);
                    beads.RemoveAt(i);
                    continue;
                }

                i++;
            }

            for (int i = 0; i < ripples.Count;)
            {
                if (now - ripples[i].t > ripples[i].life + .05f)
                    ripples.RemoveAt(i);
                else
                    i++;
            }

            for (int i = 0; i < rings.Count;)
            {
                var r = rings[i];
                r.life -= dt * r.rate;
                r.radius += dt * cs * r.speed;
                r.phase += dt * r.spin;
                if (r.life <= 0)
                {
                    rings.RemoveAt(i);
                    continue;
                }

                rings[i++] = r;
            }

            for (int i = 0; i < particles.Count;)
            {
                var p = particles[i];
                p.life -= dt * 1.6f;
                p.p += p.v * dt;
                p.v.y += p.gravity * dt;
                p.v.x *= .97f;
                if (p.life <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particles[i++] = p;
            }

            for (int i = 0; i < floats.Count;)
            {
                var f = floats[i];
                f.age += dt;
                if (f.age >= 1.4f)
                {
                    floats.RemoveAt(i);
                    continue;
                }

                floats[i++] = f;
            }
        }

        /// <summary>
        /// 对应 JavaScript rf：计算指定网点受到的高斯波前影响强度与偏移方向。
        /// </summary>
        /// <param name="r">待计算的传播波。</param>
        /// <param name="p">网点的棋盘局部逻辑像素坐标。</param>
        /// <param name="direction">输出波前推动网点的单位方向。</param>
        /// <returns>已乘以幅度和生命衰减的波纹强度。</returns>
        private float RippleForce(Ripple r, Vector2 p, out Vector2 direction)
        {
            float el = now - r.t;
            direction = Vector2.zero;
            if (el < 0 || el >= r.life)
                return 0;
            Vector2 delta = p - r.p;
            float f = 0;
            if (r.type == 'r' || r.type == 'f')
            {
                float d = delta.magnitude;
                float e = (d - el * cs * r.speed) / (cs * r.width);
                f = Mathf.Exp(-e * e);
                if (d > 0)
                    direction = delta / d;
                if (r.type == 'f')
                    f *= .55f + .45f * Mathf.Cos(6 * Mathf.Atan2(delta.y, delta.x) + r.rotation + el * 5);
            }
            else
            {
                float along = -Vector2.Dot(delta, r.d);
                float e = (along - el * cs * r.speed) / (cs * r.width);
                f = Mathf.Exp(-e * e);
                if (r.type == 'l')
                {
                    float across = -delta.x * r.d.y + delta.y * r.d.x;
                    f *= Mathf.Exp(-Mathf.Pow(across / (cs * .9f), 2));
                }

                direction = -r.d;
            }

            return f * r.a * (1 - el / r.life);
        }
    }
}
