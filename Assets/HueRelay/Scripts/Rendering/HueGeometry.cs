using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>
    /// 固定界面与动态特效共用的网格构造器。坐标以左上角为原点，y 向下为正。
    /// 不持有场景对象，可按不同图层的需要设置缓存容量。
    /// </summary>
    public sealed class HueGeometry
    {
        private readonly List<UIVertex> vertices;
        private readonly List<int> indices;
        public Vector2 origin;
        public float zoom = 1;

        /// <summary>预留网格容量；固定小面板无需使用动态特效的大缓存。</summary>
        public HueGeometry(int vertexCapacity = 128, int indexCapacity = 256)
        {
            vertices = new List<UIVertex>(vertexCapacity);
            indices = new List<int>(indexCapacity);
        }

        /// <summary>清空上一帧几何，保留已分配的容量。</summary>
        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            origin = Vector2.zero;
            zoom = 1;
        }

        /// <summary>提交当前缓存，不修改游戏状态。</summary>
        public void Populate(VertexHelper target)
        {
            target.Clear();
            // Playworks exposes individual vertex/triangle submission, not AddUIVertexStream.
            for (int i = 0; i < vertices.Count; i++)
                target.AddVert(vertices[i]);
            for (int i = 0; i < indices.Count; i += 3)
                target.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
        }

        /// <summary>
        /// 将局部逻辑像素应用到当前绘制层的平移和缩放。
        /// </summary>
        /// <param name="p">预处理棋盘或局部绘制位置。</param>
        /// <returns>变换后的逻辑像素坐标。</returns>
        private Vector2 P(Vector2 p)
        {
            return origin + p * zoom;
        }

        /// <summary>
        /// 追加一个顶点，并将 Canvas 向下为正的 y 转为 RectTransform 的局部坐标。
        /// </summary>
        /// <param name="p">预处理棋盘或局部绘制位置。</param>
        /// <param name="c">颜色代码或绘制颜色。</param>
        /// <returns>新顶点在缓存中的索引。</returns>
        private int V(Vector2 p, Color c)
        {
            p = P(p);
            var v = UIVertex.simpleVert;
            v.position = new Vector3(p.x, -p.y);
            v.color = c;
            v.uv0 = Vector2.zero;
            vertices.Add(v);
            return vertices.Count - 1;
        }

        /// <summary>
        /// 按指定顶点编号追加一个三角形。
        /// </summary>
        /// <param name="a">箭头集合或几何第一个顶点。</param>
        /// <param name="c">颜色代码或绘制颜色。</param>
        private void Tri(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        /// <summary>
        /// 追加一个纯色三角形，供箭头头部与放射形冲击使用。
        /// </summary>
        /// <param name="a">第一个局部逻辑像素顶点。</param>
        /// <param name="b">第二个局部逻辑像素顶点。</param>
        /// <param name="c">第三个局部逻辑像素顶点。</param>
        /// <param name="col">绘制颜色。</param>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int i = V(a, col);
            V(b, col);
            V(c, col);
            Tri(i, i + 1, i + 2);
        }

        /// <summary>
        /// 将四边形拆为两个三角形并追加到当前网格。
        /// </summary>
        /// <param name="a">第一个局部逻辑像素顶点。</param>
        /// <param name="b">第二个局部逻辑像素顶点。</param>
        /// <param name="c">第三个局部逻辑像素顶点。</param>
        /// <param name="d">第四个局部逻辑像素顶点。</param>
        /// <param name="col">绘制颜色。</param>
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = V(a, col);
            V(b, col);
            V(c, col);
            V(d, col);
            Tri(i, i + 1, i + 2);
            Tri(i, i + 2, i + 3);
        }

        /// <summary>
        /// 按夹紧到 [0, 1] 的系数乘以原颜色透明度。
        /// </summary>
        /// <param name="c">原颜色。</param>
        /// <param name="a">透明度乘数，将夹紧到 0 到 1。</param>
        /// <returns>调整透明度后的颜色副本。</returns>
        public static Color Alpha(Color c, float a)
        {
            c.a *= Mathf.Clamp01(a);
            return c;
        }

        /// <summary>
        /// 用四顶点包围圆点；着色器计算与原透明外缘相同的覆盖率，避免逐圆生成扇形网格。
        /// </summary>
        /// <param name="p">圆心的局部逻辑像素坐标。</param>
        /// <param name="r">逻辑像素半径。</param>
        /// <param name="c">填充颜色。</param>
        public void Disc(Vector2 p, float r, Color c)
        {
            if (r <= 0 || c.a <= 0)
                return;
            float aa = .65f / zoom;
            float extent = r + aa;
            int first = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = new Vector2(i == 0 || i == 3 ? -extent : extent, i < 2 ? -extent : extent);
                int index = V(p + local, c);
                var v = vertices[index];
                v.uv0 = local;
                v.uv1 = new Vector2(r, aa);
                // NORMAL.z distinguishes a circle from an ordinary polygon or shadow.
                v.normal = new Vector3(0, 0, 1);
                vertices[index] = v;
            }
            Tri(first, first + 1, first + 2);
            Tri(first, first + 2, first + 3);
        }

        /// <summary>
        /// 绘制带几何抗锯齿的线段，可选圆形端帽。
        /// </summary>
        /// <param name="a">线段起点。</param>
        /// <param name="b">线段终点。</param>
        /// <param name="w">逻辑像素线宽。</param>
        /// <param name="c">线条颜色。</param>
        /// <param name="caps">是否绘制圆形线段端帽。</param>
        public void Stroke(Vector2 a, Vector2 b, float w, Color c, bool caps = true)
        {
            var d = b - a;
            float l = d.magnitude;
            if (l < .001f)
            {
                if (caps)
                    Disc(a, w * .5f, c);
                return;
            }

            Vector2 n = new Vector2(-d.y, d.x) / l;
            float r = w * .5f;
            float aa = .65f / zoom;
            Quad(a + n * r, b + n * r, b - n * r, a - n * r, c);
            int i = V(a + n * r, c);
            V(b + n * r, c);
            V(b + n * (r + aa), Alpha(c, 0));
            V(a + n * (r + aa), Alpha(c, 0));
            Tri(i, i + 1, i + 2);
            Tri(i, i + 2, i + 3);
            i = V(a - n * r, c);
            V(b - n * r, c);
            V(b - n * (r + aa), Alpha(c, 0));
            V(a - n * (r + aa), Alpha(c, 0));
            Tri(i, i + 2, i + 1);
            Tri(i, i + 3, i + 2);
            if (caps)
            {
                Disc(a, r, c);
                Disc(b, r, c);
            }
        }

        /// <summary>
        /// 以连续的内外边缘网格绘制圆弧，避免短线拼接产生齿状接缝。
        /// </summary>
        /// <param name="p">圆心的局部逻辑像素坐标。</param>
        /// <param name="r">逻辑像素半径。</param>
        /// <param name="start">起始角度，单位为弧度。</param>
        /// <param name="end">终止角度，单位为弧度；可以小于起始角度。</param>
        /// <param name="width">线宽或卡片可用宽度，单位为逻辑像素。</param>
        /// <param name="c">线条颜色。</param>
        public void Arc(Vector2 p, float r, float start, float end, float width, Color c)
        {
            if (r <= 0 || c.a <= 0)
                return;
            // Keep chord error below 0.2 logical pixels, including the AA outer edge.
            // The old radius-linear rule generated hundreds of segments for a thin ring.
            float outer = (r + width * .5f) * zoom + .65f;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(end - start) * Mathf.Sqrt(outer / 1.6f)), 4, 240);
            int first = vertices.Count;
            float half = width * .5f;
            float aa = .65f / zoom;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(start, end, (float)i / n);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                V(p + d * Mathf.Max(0, r - half - aa), Alpha(c, 0));
                V(p + d * Mathf.Max(0, r - half), c);
                V(p + d * (r + half), c);
                V(p + d * (r + half + aa), Alpha(c, 0));
            }

            for (int i = 0; i < n; i++)
                for (int j = 0; j < 3; j++)
                {
                    int a = first + i * 4 + j;
                    int b = a + 4;
                    Tri(a, b, b + 1);
                    Tri(a, b + 1, a + 1);
                }

            if (Mathf.Abs(end - start) < 6.27f)
            {
                Disc(p + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * r, half, c);
                Disc(p + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * r, half, c);
            }
        }

        private readonly List<Vector2> contour = new List<Vector2>(40);

        /// <summary>
        /// 绘制带圆角与透明外缘的矩形；轮廓列表在各帧之间复用。
        /// </summary>
        /// <param name="r">局部逻辑像素矩形。</param>
        /// <param name="radius">逻辑像素半径。</param>
        /// <param name="c">填充颜色。</param>
        public void RoundRect(Rect r, float radius, Color c)
        {
            if (r.width <= 0 || r.height <= 0 || c.a <= 0)
                return;
            radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            contour.Clear();
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = new Vector2(k == 0 || k == 3 ? r.xMax - radius : r.x + radius, k < 2 ? r.yMax - radius : r.y + radius);
                for (int i = 0; i <= 8; i++)
                {
                    float a = (k * 90 + i * 90f / 8) * Mathf.Deg2Rad;
                    contour.Add(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }

            int ct = V(r.center, c);
            int first = vertices.Count;
            float aa = .65f / zoom;
            for (int i = 0; i < contour.Count; i++)
            {
                Vector2 p = contour[i];
                V(p, c);
                V(p + (p - r.center).normalized * aa, Alpha(c, 0));
            }

            for (int i = 0; i < contour.Count; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % contour.Count) * 2;
                Tri(ct, a, b);
                Tri(a, a + 1, b + 1);
                Tri(a, b + 1, b);
            }
        }

        /// <summary>
        /// 编码一个圆角矩形阴影四边形，由 HueCanvas shader 计算高斯覆盖，避免多层重复叠画。
        /// </summary>
        /// <param name="r">阴影主体矩形，使用局部逻辑像素坐标。</param>
        /// <param name="radius">逻辑像素半径。</param>
        /// <param name="blur">原 CSS 模糊半径，使用其一半作为高斯标准差。</param>
        /// <param name="offset">阴影向下偏移的逻辑像素距离。</param>
        /// <param name="opacity">整体透明度，范围为 0 到 1。</param>
        public void Shadow(Rect r, float radius, float blur, float offset, float opacity)
        {
            float sigma = Mathf.Max(.5f, blur * .5f);
            float pad = sigma * 3;
            Vector2 center = r.center + Vector2.up * offset;
            Vector2 half = r.size * .5f;
            int first = vertices.Count;
            Color color = new Color(28 / 255f, 32 / 255f, 64 / 255f, opacity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = new Vector2((i == 0 || i == 3 ? -1 : 1) * (half.x + pad), (i < 2 ? -1 : 1) * (half.y + pad));
                int index = V(center + local, color);
                var v = vertices[index];
                v.uv0 = local;
                // Playworks keeps only XY of UI TEXCOORD1. Carry the remaining
                // two shape parameters in NORMAL, which both renderers preserve.
                v.uv1 = new Vector2(half.x, half.y);
                v.normal = new Vector3(radius, sigma, 0);
                vertices[index] = v;
            }

            Tri(first, first + 1, first + 2);
            Tri(first, first + 2, first + 3);
        }

    }
}
