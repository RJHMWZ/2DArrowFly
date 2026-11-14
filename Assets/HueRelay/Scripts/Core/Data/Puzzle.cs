using System.Collections.Generic;
using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 预处理后的运行时箭头，包含朝向射线和阻挡位掩码。
    /// </summary>
    public sealed class Arrow
    {
        /// <summary>
        /// 在原关卡数组中的零基编号，也是位掩码的位号。
        /// </summary>
        public int id;

        /// <summary>
        /// 位 i 为 1 表示箭头 i 占据本箭头前方射线，构成阻挡。
        /// </summary>
        public int bm;

        /// <summary>
        /// 前方射线是否穿过自身折线；生成阶段会拒绝此类箭头。
        /// </summary>
        public bool self;

        /// <summary>
        /// 原始颜色代码。
        /// </summary>
        public string c;

        /// <summary>
        /// Canvas 坐标系下的单位朝向。
        /// </summary>
        public Vector2 d;

        /// <summary>
        /// 尾部到头部的原始折线，不随飞行动画修改。
        /// </summary>
        public Cell[] cells;

        /// <summary>
        /// 从头部下一格延伸至棋盘边界的全部格子。
        /// </summary>
        public readonly List<Cell> ray = new List<Cell>();

        /// <summary>
        /// 原始折线最后一格的坐标。
        /// </summary>
        public Vector2 Head
        {
            get
            {
                return cells[cells.Length - 1].V;
            }
        }
    }

    /// <summary>
    /// 已预处理的棋盘；运行中的剩余箭头通过独立位掩码表示，避免复制关卡。
    /// </summary>
    public sealed class Puzzle
    {
        /// <summary>
        /// 原始关卡布局。
        /// </summary>
        public LevelData L;

        /// <summary>
        /// 按原始顺序排列的预处理箭头。
        /// </summary>
        public Arrow[] arrs;

        /// <summary>
        /// 箭头总数，生成器上限为 30。
        /// </summary>
        public int n
        {
            get
            {
                return arrs.Length;
            }
        }

        /// <summary>
        /// 当前棋盘所有箭头都存在时的完整掩码。
        /// </summary>
        public int Full
        {
            get
            {
                return (1 << n) - 1;
            }
        }
    }
}
