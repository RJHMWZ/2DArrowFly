using System.Collections.Generic;

namespace HueRelay
{
    /// <summary>
    /// 随机模拟的决策结果：预计手动点击次数，以及应当首先点击的箭头。
    /// </summary>
    public struct Choice
    {
        /// <summary>
        /// 模拟清盘需要的手动点击次数。
        /// </summary>
        public int taps;

        /// <summary>
        /// 建议首先点击的箭头编号；无候选时为 -1。
        /// </summary>
        public int first;

        /// <summary>
        /// 创建一次模拟搜索的点击次数与首步结果。
        /// </summary>
        /// <param name="t">清盘所需的手动点击次数。</param>
        /// <param name="f">首步箭头编号；无候选时为 -1。</param>
        public Choice(int t, int f)
        {
            taps = t;
            first = f;
        }
    }

    /// <summary>
    /// 种植式生成器的拓扑信息，包括入口、门控分组和同色接力链。
    /// </summary>
    public sealed class PlantInfo
    {
        /// <summary>
        /// 主同色接力链的入口箭头编号。
        /// </summary>
        public int K;

        /// <summary>
        /// 每个门控分组的末端箭头编号。
        /// </summary>
        public List<int> gates = new List<int>();

        /// <summary>
        /// 主入口后方的同色接力链编号。
        /// </summary>
        public List<int> ch = new List<int>();

        /// <summary>
        /// 门控分组内按放置顺序排列的箭头编号。
        /// </summary>
        public List<List<int>> grp = new List<List<int>>();
    }

    /// <summary>
    /// 一次成功的种植式生成结果，包含完整关卡和生成时的拓扑信息。
    /// </summary>
    public sealed class Planted
    {
        /// <summary>
        /// 成功生成的关卡；整体返回 null 表示生成失败。
        /// </summary>
        public LevelData L;

        /// <summary>
        /// 生成时建立的门控与连锁拓扑。
        /// </summary>
        public PlantInfo info;
    }

    /// <summary>
    /// 关卡难度评估结果，用于筛选初始连锁较短、具有策略差异的棋盘。
    /// </summary>
    public sealed class Metrics
    {
        /// <summary>
        /// 箭头总数。
        /// </summary>
        public int n;

        /// <summary>
        /// 初始状态下单次点击可触发的最大连锁长度。
        /// </summary>
        public int init;

        /// <summary>
        /// 原版随机模拟得到的基准手动点击次数。
        /// </summary>
        public int par;

        /// <summary>
        /// 均匀随机策略平均步数与基准步数的差值。
        /// </summary>
        public double gap;

        /// <summary>
        /// 完成评估的预处理棋盘。
        /// </summary>
        public Puzzle P;
    }

    /// <summary>
    /// 种植式生成配置；字段顺序和含义对应 HTML 的 SPECS。
    /// </summary>
    public sealed class Spec
    {
        /// <summary>
        /// 生成区域列数。
        /// </summary>
        public int w;

        /// <summary>
        /// 生成区域行数。
        /// </summary>
        public int h;

        /// <summary>
        /// 主连锁颜色在 ABC 中的索引。
        /// </summary>
        public int x;

        /// <summary>
        /// 主入口之后要求种植的同色箭头数量。
        /// </summary>
        public int n;

        /// <summary>
        /// 优先尝试放置的同色诱饵数量。
        /// </summary>
        public int lure;

        /// <summary>
        /// 目标箭头总数。
        /// </summary>
        public int total;

        /// <summary>
        /// 每个门控组的依赖深度。
        /// </summary>
        public int[] g;

        /// <summary>
        /// 创建一组门控深度、目标同色链长度和诱饵配置。
        /// </summary>
        /// <param name="w">棋盘列数。</param>
        /// <param name="h">棋盘行数或路径起点。</param>
        /// <param name="x">主接力颜色在 ABC 中的索引，取值为 0、1 或 2。</param>
        /// <param name="n">主入口之后的同色接力箭头数量。</param>
        /// <param name="g">各门控组的依赖深度数组。</param>
        /// <param name="lure">希望放置的同色诱饵数量。</param>
        /// <param name="total">目标箭头总数。</param>
        public Spec(int w, int h, int x, int n, int[] g, int lure, int total)
        {
            this.w = w;
            this.h = h;
            this.x = x;
            this.n = n;
            this.g = g;
            this.lure = lure;
            this.total = total;
        }
    }
}
