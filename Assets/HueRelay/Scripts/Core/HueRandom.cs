namespace HueRelay
{
    /// <summary>
    /// 与原 HTML 的 Mulberry32 完全一致的有种子随机数发生器；使用无符号 32 位溢出保持跨平台序列一致。
    /// </summary>
    public sealed class HueRandom
    {
        /// <summary>
        /// Mulberry32 的无符号 32 位内部状态。
        /// </summary>
        private uint state;

        /// <summary>
        /// 使用指定种子初始化与 JavaScript 对应的无符号随机状态。
        /// </summary>
        /// <param name="seed">随机种子；相同种子和规格应得到相同结果。</param>
        public HueRandom(int seed)
        {
            state = unchecked((uint)seed);
        }

        /// <summary>
        /// 推进一次 Mulberry32 状态并返回半开区间 [0, 1) 的随机数。
        /// </summary>
        /// <returns>大于等于 0 且小于 1 的随机浮点数。</returns>
        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = (state ^ state >> 15) * (1u | state);
                t = (t + (t ^ t >> 7) * (61u | t)) ^ t;
                return (t ^ t >> 14) / 4294967296.0;
            }
        }

        /// <summary>
        /// 将一次随机采样映射为 [0, n) 的整数；采样次数必须与原 JavaScript 保持一致。
        /// </summary>
        /// <param name="n">不包含在结果中的正整数上界。</param>
        /// <returns>大于等于 0 且小于 n 的随机整数。</returns>
        public int Int(int n)
        {
            return (int)(Next() * n);
        }
    }
}
