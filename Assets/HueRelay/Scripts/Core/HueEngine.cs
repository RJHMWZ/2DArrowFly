using System;
using System.Collections.Generic;
using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 纯规则与生成算法：预处理、阻挡检测、同色接力、随机棋盘和提示搜索，不负责动画或输入。
    /// </summary>
    public static class HueEngine
    {
        /// <summary>
        /// 同一线程内复用的接力模拟队列；不在频繁提示模拟中分配临时数组。
        /// </summary>
        [ThreadStatic]
        private static int[] chainQueue;

        /// <summary>
        /// 同一线程内复用的可点击候选编号缓存。
        /// </summary>
        [ThreadStatic]
        private static int[] rolloutIds;

        /// <summary>
        /// 与候选编号一一对应的模拟采样权重。
        /// </summary>
        [ThreadStatic]
        private static int[] rolloutWeights;

        /// <summary>
        /// 生成器的方向枚举顺序，必须与 JavaScript 保持一致以保留种子结果。
        /// </summary>
        public static readonly Vector2[] D =
        {
            Vector2.right,
            Vector2.left,
            Vector2.up,
            Vector2.down
        }; // Canvas 的正 y 方向朝下。
        /// <summary>
        /// 与 D 对应的方向字符串。
        /// </summary>
        public static readonly string[] DN =
        {
            "r",
            "l",
            "d",
            "u"
        };

        /// <summary>
        /// 五个预设关卡在基准步数外允许的额外步数。
        /// </summary>
        public static readonly int[] Slack =
        {
            3,
            2,
            2,
            1,
            1
        };

        /// <summary>
        /// 原 HTML 的五组种植式生成规格。
        /// </summary>
        public static readonly Spec[] Specs =
        {
            new Spec(5, 6, 1, 3, new[] { 0 }, 0, 9),
            new Spec(6, 8, 2, 5, new[] { 0, 0 }, 1, 12),
            new Spec(7, 9, 0, 6, new[] { 1, 0 }, 2, 15),
            new Spec(8, 11, 1, 8, new[] { 1, 1, 0 }, 3, 20),
            new Spec(9, 12, 2, 10, new[] { 2, 1, 0 }, 3, 24)
        };

        /// <summary>
        /// 将棋盘坐标编码为字典键；当前棋盘宽度均小于 64。
        /// </summary>
        /// <param name="x">横向网格坐标或曲线输入。</param>
        /// <param name="y">纵向网格坐标，向下为正。</param>
        /// <returns>适用于当前棋盘尺寸的唯一整数键。</returns>
        private static int Key(int x, int y)
        {
            return y * 64 + x;
        }

        /// <summary>
        /// 将 u、d、l、r 转为 Canvas 坐标方向；这里的向下是正 y，与 Unity 世界坐标不同。
        /// </summary>
        /// <param name="s">原版方向代码 u、d、l 或 r。</param>
        /// <returns>Canvas 坐标系下的单位方向向量。</returns>
        public static Vector2 Dir(string s)
        {
            return s == "r" ? Vector2.right : s == "l" ? Vector2.left : s == "d" ? Vector2.up : Vector2.down;
        }

        /// <summary>
        /// 对应 JavaScript prep：建立占用表，预计算箭头前方射线、自身阻挡和其他箭头的阻挡掩码。
        /// </summary>
        /// <param name="L">原始关卡布局。</param>
        /// <returns>包含全部阻挡信息的棋盘。</returns>
        public static Puzzle Prep(LevelData L)
        {
            var p = new Puzzle
            {
                L = L,
                arrs = new Arrow[L.a.Length]
            };
            var occ = new Dictionary<int, int>();
            for (int i = 0; i < L.a.Length; i++)
            {
                var a = L.a[i];
                p.arrs[i] = new Arrow
                {
                    id = i,
                    c = a.c,
                    d = Dir(a.d),
                    cells = a.cells
                };
                foreach (var c in a.cells)
                    occ[Key(c.x, c.y)] = i;
            }

            foreach (var a in p.arrs)
            {
                int x = (int)a.Head.x;
                int y = (int)a.Head.y;
                while (true)
                {
                    x += (int)a.d.x;
                    y += (int)a.d.y;
                    if (x < 0 || y < 0 || x >= L.w || y >= L.h)
                        break;
                    a.ray.Add(new Cell(x, y));
                    int j;
                    if (occ.TryGetValue(Key(x, y), out j))
                    {
                        if (j == a.id)
                            a.self = true;
                        else
                            a.bm |= 1 << j;
                    }
                }
            }

            return p;
        }

        /// <summary>
        /// 对应 JavaScript isFree：箭头仍存在，且其阻挡掩码与剩余掩码没有交集时允许发射。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <param name="i">箭头编号或阶段索引。</param>
        /// <returns>箭头存在且没有剩余阻挡时返回 true。</returns>
        public static bool IsFree(Puzzle p, int m, int i)
        {
            return (m & (1 << i)) != 0 && (p.arrs[i].bm & m) == 0;
        }

        /// <summary>
        /// 对应 JavaScript launch：移除指定箭头，并按原数组顺序找出刚被解锁的同色箭头。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <param name="i">箭头编号或阶段索引。</param>
        /// <param name="auto">输出列表；只追加新解锁的编号，调用方负责清空。</param>
        /// <returns>移除该箭头后的剩余掩码。</returns>
        public static int Launch(Puzzle p, int m, int i, List<int> auto)
        {
            int nm = m & ~(1 << i);
            for (int j = 0; j < p.n; j++)
            {
                var a = p.arrs[j];
                if ((nm & (1 << j)) != 0 && (a.bm & (1 << i)) != 0 && (a.bm & nm) == 0 && a.c == p.arrs[i].c)
                    auto.Add(j);
            }

            return nm;
        }

        /// <summary>
        /// 对应 JavaScript chain：仅模拟规则，按队列清除整段同色接力，不播放动画，也不增加步数。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <param name="i">箭头编号或阶段索引。</param>
        /// <returns>整条同色接力结束后的剩余掩码。</returns>
        public static int Chain(Puzzle p, int m, int i)
        {
            // 最多 30 支箭头；复用线程局部数组，避免大量提示模拟产生临时分配。
            int[] q = chainQueue ?? (chainQueue = new int[64]);
            q[0] = i;
            int head = 0;
            int tail = 1;
            while (head < tail)
            {
                int id = q[head++];
                m &= ~(1 << id);
                for (int j = 0; j < p.n; j++)
                {
                    var a = p.arrs[j];
                    if ((m & (1 << j)) != 0 && (a.bm & (1 << id)) != 0 && (a.bm & m) == 0 && a.c == p.arrs[id].c)
                        q[tail++] = j;
                }
            }

            return m;
        }

        /// <summary>
        /// 使用清除最低有效位的方式统计掩码中剩余箭头的数量。
        /// </summary>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <returns>掩码中值为 1 的位数。</returns>
        public static int PC(int m)
        {
            int c = 0;
            while (m != 0)
            {
                m &= m - 1;
                c++;
            }

            return c;
        }

        /// <summary>
        /// 反复剥离可发射箭头，判断整个棋盘能否被清空。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <returns>存在逐步清空棋盘的顺序时返回 true。</returns>
        public static bool PeelOK(Puzzle p)
        {
            int m = p.Full;
            bool go = true;
            while (m != 0 && go)
            {
                go = false;
                for (int i = 0; i < p.n; i++)
                    if (IsFree(p, m, i))
                    {
                        m &= ~(1 << i);
                        go = true;
                    }
            }

            return m == 0;
        }

        /// <summary>
        /// 从当前掩码模拟一局：默认按连锁收益的平方加权选择，也可用于均匀随机难度评估。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <param name="r">指定的随机数发生器或绘制半径。</param>
        /// <param name="random">为 true 时均匀选择，否则按连锁收益平方加权。</param>
        /// <returns>模拟得到的点击次数与首步编号。</returns>
        public static Choice Rollout(Puzzle p, int m, HueRandom r, bool random = false)
        {
            int taps = 0;
            int first = -1;
            int[] ids = rolloutIds ?? (rolloutIds = new int[30]);
            int[] weights = rolloutWeights ?? (rolloutWeights = new int[30]);
            while (m != 0)
            {
                int n = 0;
                int sum = 0;
                for (int i = 0; i < p.n; i++)
                    if (IsFree(p, m, i))
                    {
                        int gain = PC(m) - PC(Chain(p, m, i));
                        ids[n] = i;
                        weights[n] = random ? 1 : gain * gain;
                        sum += weights[n++];
                    }

                if (n == 0)
                    return new Choice(99, -1);
                double s = sum * r.Next();
                int k = ids[0];
                for (int j = 0; j < n; j++)
                {
                    s -= weights[j];
                    if (s <= 0)
                    {
                        k = ids[j];
                        break;
                    }
                }

                if (first < 0)
                    first = k;
                m = Chain(p, m, k);
                taps++;
            }

            return new Choice(taps, first);
        }

        /// <summary>
        /// 执行指定次数的随机模拟，保留手动点击次数最少的首步方案；并不宣称是穷举最优解。
        /// </summary>
        /// <param name="p">已完成预处理的棋盘。</param>
        /// <param name="m">仍存在的箭头位掩码。</param>
        /// <param name="r">指定的随机数发生器或绘制半径。</param>
        /// <param name="N">随机模拟次数。</param>
        /// <returns>所有模拟中点击次数最少的结果。</returns>
        public static Choice Best(Puzzle p, int m, HueRandom r, int N)
        {
            var b = new Choice(99, -1);
            for (int i = 0; i < N; i++)
            {
                var a = Rollout(p, m, r);
                if (a.taps < b.taps)
                    b = a;
            }

            return b;
        }

        /// <summary>
        /// 判断坐标是否位于给定棋盘的有效范围内。
        /// </summary>
        /// <param name="x">横向网格坐标或曲线输入。</param>
        /// <param name="y">纵向网格坐标，向下为正。</param>
        /// <param name="w">棋盘列数。</param>
        /// <param name="h">棋盘行数或路径起点。</param>
        /// <returns>坐标位于棋盘内时返回 true。</returns>
        private static bool Inside(int x, int y, int w, int h)
        {
            return x >= 0 && y >= 0 && x < w && y < h;
        }

        /// <summary>
        /// 对应 JavaScript gen：生成互不重叠且可剥离的随机折线，再按阻挡依赖关系分配颜色。
        /// </summary>
        /// <param name="seed">随机种子；相同种子和规格应得到相同结果。</param>
        /// <param name="w">棋盘列数。</param>
        /// <param name="h">棋盘行数或路径起点。</param>
        /// <returns>按给定种子生成的可解棋盘。</returns>
        public static LevelData Gen(int seed, int w = 9, int h = 12)
        {
            var r = new HueRandom(seed);
            var A = new List<ArrowData>();
            var occ = new HashSet<int>();
            var options = new List<int>(4);
            for (int t = 0; t < 6000 && A.Count < 30; t++)
            {
                int len = 3 + r.Int(5);
                int x = r.Int(w);
                int y = r.Int(h);
                if (occ.Contains(Key(x, y)))
                    continue;
                var cells = new List<Cell>
                {
                    new Cell(x, y)
                };
                var used = new HashSet<int>
                {
                    Key(x, y)
                };
                int pd = -1;
                while (cells.Count < len)
                {
                    options.Clear();
                    for (int j = 0; j < 4; j++)
                    {
                        int nx = x + (int)D[j].x;
                        int ny = y + (int)D[j].y;
                        if (Inside(nx, ny, w, h) && !occ.Contains(Key(nx, ny)) && !used.Contains(Key(nx, ny)))
                            options.Add(j);
                    }

                    if (options.Count == 0)
                        break;
                    int d = pd >= 0 && options.Contains(pd) && r.Next() < .5 ? pd : options[r.Int(options.Count)];
                    x += (int)D[d].x;
                    y += (int)D[d].y;
                    cells.Add(new Cell(x, y));
                    used.Add(Key(x, y));
                    pd = d;
                }

                if (cells.Count < 3)
                    continue;
                A.Add(new ArrowData("A", DN[pd], cells.ToArray()));
                var p = Prep(new LevelData(w, h, A.ToArray()));
                if (p.arrs[p.n - 1].self || !PeelOK(p))
                {
                    A.RemoveAt(A.Count - 1);
                    continue;
                }

                foreach (var c in cells)
                    occ.Add(Key(c.x, c.y));
            }

            var P = Prep(new LevelData(w, h, A.ToArray()));
            var col = new string[P.n];
            int m = P.Full;
            bool go = true;
            while (m != 0 && go)
            {
                go = false;
                for (int i = 0; i < P.n; i++)
                    if (IsFree(P, m, i))
                    {
                        m &= ~(1 << i);
                        go = true;
                        var bl = new List<string>();
                        for (int j = 0; j < P.n; j++)
                            if ((P.arrs[i].bm & (1 << j)) != 0)
                                bl.Add(col[j]);
                        col[i] = bl.Count > 0 && r.Next() < .62 ? bl[r.Int(bl.Count)] : "ABC"[r.Int(3)].ToString();
                    }
            }

            for (int i = 0; i < A.Count; i++)
                A[i].c = col[i];
            return new LevelData(w, h, A.ToArray());
        }

        /// <summary>
        /// 从最多五个种子中筛选策略差异较大的棋盘；指定种子时只评估该种子。
        /// </summary>
        /// <param name="seed">输出最终选中的种子。</param>
        /// <param name="seed0">可选固定种子；存在时只执行一次种子评估。</param>
        /// <param name="w">棋盘列数。</param>
        /// <param name="h">棋盘行数或路径起点。</param>
        /// <returns>选中棋盘的基准点击次数；种子通过 out 参数返回。</returns>
        public static Choice Pick(out int seed, int? seed0 = null, int w = 9, int h = 12)
        {
            double bs = -1e9;
            seed = 0;
            var result = new Choice(99, -1);
            for (int t = 0; t < 5; t++)
            {
                int sd = seed0 ?? UnityEngine.Random.Range(0, 1000000000);
                var p = Prep(Gen(sd, w, h));
                int par = Best(p, p.Full, new HueRandom(7), 150).taps;
                double nv = 0;
                for (int i = 0; i < 20; i++)
                    nv += Rollout(p, p.Full, new HueRandom(i + 3), true).taps;
                double sc = nv / 20 - par + p.n * .08;
                if (sc > bs)
                {
                    bs = sc;
                    seed = sd;
                    result = new Choice(par, -1);
                }

                if (seed0.HasValue)
                    break;
            }

            return result;
        }

        /// <summary>
        /// 一次 plant 调用独享的生成上下文；占用格、受保护射线和随机序列不会污染其他生成任务。
        /// </summary>
        sealed class Planter
        {
            public Spec sp;
            public HueRandom r;
            public string X;
            public string oth;
            public List<ArrowData> A = new List<ArrowData>();
            public Dictionary<int, int> occ = new Dictionary<int, int>();
            public HashSet<int> prot = new HashSet<int>();
            /// <summary>
            /// 从箭头头部的下一格开始收集朝向射线，直到离开生成区域。
            /// </summary>
            /// <param name="x">横向网格坐标或曲线输入。</param>
            /// <param name="y">纵向网格坐标，向下为正。</param>
            /// <param name="d">方向代码、方向向量或音符持续时间。</param>
            /// <returns>从头部下一格开始的射线格子列表。</returns>
            public List<Cell> Ray(int x, int y, int d)
            {
                var a = new List<Cell>();
                for (x += (int)D[d].x, y += (int)D[d].y; Inside(x, y, sp.w, sp.h); x += (int)D[d].x, y += (int)D[d].y)
                    a.Add(new Cell(x, y));
                return a;
            }

            /// <summary>
            /// 尝试放置一支符合占用、保护区及指定阻挡关系的箭头，最多尝试 500 次。
            /// </summary>
            /// <param name="color">可选主连锁颜色索引，未指定时使用规格中的颜色。</param>
            /// <param name="allowed">朝向射线上允许出现的阻挡编号；null 表示不限，空列表表示必须没有阻挡。</param>
            /// <param name="must">朝向射线必须命中的阻挡编号。</param>
            /// <param name="protect">是否保护该箭头前方的空射线，以免后续放置破坏门控关系。</param>
            /// <returns>新箭头的编号；无法放置时返回 -1。</returns>
            public int Place(string color, List<int> allowed, List<int> must, bool protect)
            {
                for (int t = 0; t < 500; t++)
                {
                    int d = r.Int(4);
                    int x = r.Int(sp.w);
                    int y = r.Int(sp.h);
                    if (occ.ContainsKey(Key(x, y)) || prot.Contains(Key(x, y)))
                        continue;
                    var rc = Ray(x, y, d);
                    var hit = new HashSet<int>();
                    bool ok = true;
                    foreach (var c in rc)
                    {
                        int o;
                        if (occ.TryGetValue(Key(c.x, c.y), out o))
                        {
                            if (allowed == null || allowed.Contains(o))
                                hit.Add(o);
                            else
                            {
                                ok = false;
                                break;
                            }
                        }
                    }

                    foreach (int id in must)
                        if (!hit.Contains(id))
                            ok = false;
                    if (!ok)
                        continue;
                    int bx = x - (int)D[d].x;
                    int by = y - (int)D[d].y;
                    if (!Inside(bx, by, sp.w, sp.h) || occ.ContainsKey(Key(bx, by)) || prot.Contains(Key(bx, by)))
                        continue;
                    int len = 3 + r.Int(protect ? 2 : 4);
                    var cells = new List<Cell>
                    {
                        new Cell(x, y),
                        new Cell(bx, by)
                    };
                    var used = new HashSet<int>
                    {
                        Key(x, y),
                        Key(bx, by)
                    };
                    var rs = new HashSet<int>();
                    foreach (var c in rc)
                        rs.Add(Key(c.x, c.y));
                    int cx = bx;
                    int cy = by;
                    Vector2 pd = -D[d];
                    while (cells.Count < len)
                    {
                        var o = new List<int>();
                        for (int j = 0; j < 4; j++)
                        {
                            int nx = cx + (int)D[j].x;
                            int ny = cy + (int)D[j].y;
                            int k = Key(nx, ny);
                            if (Inside(nx, ny, sp.w, sp.h) && !occ.ContainsKey(k) && !prot.Contains(k) && !used.Contains(k) && !rs.Contains(k))
                                o.Add(j);
                        }

                        if (o.Count == 0)
                            break;
                        bool prefer = r.Next() < .4;
                        int pi = -1;
                        foreach (int j in o)
                            if (D[j] == pd)
                                pi = j;
                        Vector2 e = prefer && pi >= 0 ? pd : D[o[r.Int(o.Count)]];
                        cx += (int)e.x;
                        cy += (int)e.y;
                        cells.Add(new Cell(cx, cy));
                        used.Add(Key(cx, cy));
                        pd = e;
                    }

                    if (cells.Count < 3)
                        continue;
                    cells.Reverse();
                    int idA = A.Count;
                    A.Add(new ArrowData(color, DN[d], cells.ToArray()));
                    foreach (var c in cells)
                        occ[Key(c.x, c.y)] = idA;
                    if (protect)
                        foreach (var c in rc)
                            if (!occ.ContainsKey(Key(c.x, c.y)))
                                prot.Add(Key(c.x, c.y));
                    return idA;
                }

                return -1;
            }
        }

        /// <summary>
        /// 对应 JavaScript plant：依次构建门控组、接力入口、同色长链和诱饵；放置失败时返回 null。
        /// </summary>
        /// <param name="sp">种植式生成规格。</param>
        /// <param name="seed">随机种子；相同种子和规格应得到相同结果。</param>
        /// <param name="color">可选主连锁颜色索引，未指定时使用规格中的颜色。</param>
        /// <returns>生成结果，或表示本次种植失败的 null。</returns>
        public static Planted Plant(Spec sp, int seed, int? color = null)
        {
            var z = new Planter
            {
                sp = sp,
                r = new HueRandom(seed),
                X = "ABC"[color ?? sp.x].ToString()
            };
            z.oth = "ABC".Replace(z.X, "");
            var all = new List<int>();
            var info = new PlantInfo();
            for (int gi = 0; gi < sp.g.Length; gi++)
            {
                int dep = sp.g[gi];
                int prev = -1;
                var mine = new List<int>();
                for (int k = 0; k <= dep; k++)
                {
                    string col = k == dep && gi == 0 ? z.X : z.oth[z.r.Int(2)].ToString();
                    if (prev >= 0 && col == z.A[prev].c)
                        col = z.oth[(z.oth.IndexOf(col, StringComparison.Ordinal) + 1) % 2].ToString();
                    int id = z.Place(col, prev < 0 ? new List<int>() : mine, prev < 0 ? new List<int>() : new List<int> { prev }, true);
                    if (id < 0)
                        return null;
                    mine.Add(id);
                    all.Add(id);
                    prev = id;
                }

                info.gates.Add(prev);
                info.grp.Add(mine);
            }

            int K = z.Place(z.X, new List<int>(all), new List<int>(info.gates), true);
            if (K < 0)
                return null;
            info.K = K;
            all.Add(K);
            int pv = K;
            for (int i = 0; i < sp.n; i++)
            {
                int id = z.Place(z.X, new List<int>(all), new List<int> { pv }, true);
                if (id < 0)
                    return null;
                all.Add(id);
                info.ch.Add(id);
                pv = id;
            }

            int lure = sp.lure;
            for (int t = 0; t < 400 && z.A.Count < sp.total; t++)
            {
                int id = z.Place(lure > 0 ? z.X : z.oth[z.r.Int(2)].ToString(), null, new List<int>(), false);
                if (id < 0)
                    break;
                var p = Prep(new LevelData(sp.w, sp.h, z.A.ToArray()));
                if (p.arrs[id].self || !PeelOK(p))
                {
                    var a = z.A[z.A.Count - 1];
                    z.A.RemoveAt(z.A.Count - 1);
                    foreach (var c in a.cells)
                        z.occ.Remove(Key(c.x, c.y));
                    continue;
                }

                if (lure > 0)
                    lure--;
            }

            return new Planted
            {
                L = new LevelData(sp.w, sp.h, z.A.ToArray()),
                info = info
            };
        }

        /// <summary>
        /// 对应 JavaScript metrics：检查可解性，并估算初始连锁、基准步数和随机策略差距。
        /// </summary>
        /// <param name="L">原始关卡布局。</param>
        /// <returns>难度评估；不可解或含自身阻挡时返回 null。</returns>
        public static Metrics Measure(LevelData L)
        {
            var p = Prep(L);
            if (!PeelOK(p))
                return null;
            int init = 0;
            foreach (var a in p.arrs)
            {
                if (a.self)
                    return null;
                if (IsFree(p, p.Full, a.id))
                    init = Math.Max(init, PC(p.Full) - PC(Chain(p, p.Full, a.id)));
            }

            int par = Best(p, p.Full, new HueRandom(7), 300).taps;
            double nv = 0;
            for (int i = 0; i < 30; i++)
                nv += Rollout(p, p.Full, new HueRandom(i + 3), true).taps;
            return new Metrics
            {
                n = p.n,
                init = init,
                par = par,
                gap = nv / 30 - par,
                P = p
            };
        }

        /// <summary>
        /// 对应 JavaScript ideal：先清除辅助门控，统计主入口能够触发的同色链长度。
        /// </summary>
        /// <param name="p">与生成信息对应的预处理棋盘。</param>
        /// <param name="info">种植时记录的门控和接力拓扑。</param>
        /// <returns>taps 表示前置点击数，first 表示主入口的连锁长度；无法解锁时长度为 -1。</returns>
        public static Choice Ideal(Puzzle p, PlantInfo info)
        {
            int m = p.Full;
            int cnt = 0;
            bool go = true;
            var set = new List<int>();
            foreach (var g in info.grp)
                foreach (int i in g)
                    if (i != info.gates[0])
                        set.Add(i);
            while (go)
            {
                go = false;
                foreach (int i in set)
                    if (IsFree(p, m, i))
                    {
                        m = Chain(p, m, i);
                        cnt++;
                        go = true;
                        break;
                    }
            }

            int gate = info.gates[0];
            return new Choice(cnt, IsFree(p, m, gate) ? PC(m) - PC(Chain(p, m, gate)) : -1);
        }
    }
}
