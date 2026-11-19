using System;
using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 棋盘整数坐标。x 向右增加，y 向下增加。
    /// </summary>
    [Serializable]
    public class Cell
    {
        /// <summary>
        /// 从左向右增加的零基列号。
        /// </summary>
        public int x;

        /// <summary>
        /// 从上向下增加的零基行号。
        /// </summary>
        public int y;

        /// <summary>
        /// 创建一个棋盘整数坐标。
        /// </summary>
        /// <param name="x">从左向右增加的零基列号。</param>
        /// <param name="y">纵向网格坐标，向下为正。</param>
        public Cell(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// 同一格子的浮点坐标表示；不包含半格绘制偏移。
        /// </summary>
        public Vector2 V
        {
            get
            {
                return new Vector2(x, y);
            }
        }
    }

    /// <summary>
    /// 关卡中的原始折线箭头数据；保留 HTML 的 c、d、cells 字段名以便直接对照。
    /// </summary>
    [Serializable]
    public class ArrowData
    {
        /// <summary>
        /// 颜色代码：A 为深色、B 为蓝色、C 为橙色。
        /// </summary>
        public string c;

        /// <summary>
        /// 朝向代码：u、d、l、r。
        /// </summary>
        public string d;

        /// <summary>
        /// 从尾部到头部的连续折线格子；最后一个格子是箭头头部。
        /// </summary>
        public Cell[] cells;

        /// <summary>
        /// 创建保留原始颜色代码、朝向代码和格子顺序的箭头数据。
        /// </summary>
        /// <param name="c">原版颜色代码 A、B 或 C。</param>
        /// <param name="d">原版方向代码 u、d、l 或 r。</param>
        /// <param name="cells">从尾部到头部排列的折线格子。</param>
        public ArrowData(string c, string d, Cell[] cells)
        {
            this.c = c;
            this.d = d;
            this.cells = cells;
        }
    }

    /// <summary>
    /// 单个关卡的尺寸和箭头集合；仅表示布局，不包含运行中的移除状态。
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id;
        public string title;
        public int chapter;
        public float difficulty;
        [Min(0)] public int par;
        [Min(0)] public int moves;
        [Min(0)] public int hints = 3;
        [Min(0)] public int erasers = 3;
        [Min(0)] public int extraMoves = 1;
        public int[] solution;
        public bool guidedChains;

        /// <summary>
        /// 棋盘列数。
        /// </summary>
        public int w;

        /// <summary>
        /// 棋盘行数。
        /// </summary>
        public int h;

        /// <summary>
        /// 原始箭头数组；顺序同时决定箭头编号与入场顺序。
        /// </summary>
        public ArrowData[] a;

        /// <summary>
        /// 创建给定尺寸和箭头布局的关卡。
        /// </summary>
        /// <param name="w">棋盘列数。</param>
        /// <param name="h">棋盘行数或路径起点。</param>
        /// <param name="a">箭头集合或几何第一个顶点。</param>
        public LevelData(int w, int h, ArrowData[] a)
        {
            this.w = w;
            this.h = h;
            this.a = a;
        }
    }

    /// <summary>
    /// 关卡集及其独立的成绩存储版本。
    /// </summary>
    [Serializable]
    public class LevelCollection
    {
        public string campaignId;
        /// <summary>
        /// 按难度顺序排列的关卡。
        /// </summary>
        public LevelData[] levels;
    }
}
