using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>
    /// 仅负责关卡网格和动态特效。文字池可直接保存在场景中，不在启动时重建固定 UI。
    /// </summary>
    public sealed class HueSurface : MaskableGraphic
    {
        private readonly HueGeometry geometry = new HueGeometry(4096, 8192);
        [SerializeField, HideInInspector] private List<Text> texts = new List<Text>();
        [SerializeField, HideInInspector] private List<Outline> outlines = new List<Outline>();
        private int textUsed;
        /// <summary>
        /// 普通无衬线字体。
        /// </summary>
        public Font inter;

        /// <summary>
        /// 粗体无衬线字体。
        /// </summary>
        public Font interBold;

        /// <summary>
        /// 分数专用衬线字体，已烘焙原 CSS 字重、光学尺寸和字距。
        /// </summary>
        public Font serif;

        /// <summary>
        /// 底部大分数专用衬线字体。
        /// </summary>
        public Font serifBig;

        /// <summary>
        /// Canvas 加分浮字专用衬线字体。
        /// </summary>
        public Font serifFloat;

        /// <summary>
        /// 中文常规字体子集。
        /// </summary>
        public Font chinese;

        /// <summary>
        /// 中文粗体字体子集。
        /// </summary>
        public Font chineseBold;

        /// <summary>
        /// 兼容文字接口的手势字体后备项；实际彩色手势使用纹理绘制。
        /// </summary>
        public Font emoji;


        /// <summary>动态绘制层的局部平移。</summary>
        public Vector2 origin
        {
            get
            {
                return geometry.origin;
            }
            set
            {
                geometry.origin = value;
            }
        }
        /// <summary>动态绘制层的局部缩放。</summary>
        public float zoom
        {
            get
            {
                return geometry.zoom;
            }
            set
            {
                geometry.zoom = value;
            }
        }
        /// <summary>场景中已经准备的文字槽位数，供检查池容量使用。</summary>
        public int TextCapacity
        {
            get
            {
                return texts.Count;
            }
        }

        /// <summary>复用本帧网格与文字池。</summary>
        public void Begin()
        {
            geometry.Clear();
            textUsed = 0;
        }
        /// <summary>提交本帧并隐藏未使用的文字槽位。</summary>
        public void End()
        {
            for (int i = textUsed; i < texts.Count; i++)
                texts[i].gameObject.SetActive(false);
            SetVerticesDirty();
        }
        /// <summary>向 Unity UI 提交动态几何。</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            geometry.Populate(vh);
        }
        private Vector2 P(Vector2 p)
        {
            return origin + p * zoom;
        }
        /// <summary>追加箭头三角形。</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            geometry.Triangle(a, b, c, col);
        }
        /// <summary>追加四边形。</summary>
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            geometry.Quad(a, b, c, d, col);
        }
        /// <summary>按给定系数调整透明度。</summary>
        public static Color Alpha(Color c, float a)
        {
            return HueGeometry.Alpha(c, a);
        }
        /// <summary>绘制带抗锯齿外缘的圆点。</summary>
        public void Disc(Vector2 p, float r, Color c)
        {
            geometry.Disc(p, r, c);
        }
        /// <summary>绘制带圆形端帽的线段。</summary>
        public void Stroke(Vector2 a, Vector2 b, float w, Color c, bool caps = true)
        {
            geometry.Stroke(a, b, w, c, caps);
        }
        /// <summary>绘制连续圆弧，角度单位为弧度。</summary>
        public void Arc(Vector2 p, float r, float start, float end, float width, Color c)
        {
            geometry.Arc(p, r, start, end, width, c);
        }
        /// <summary>绘制圆角矩形。</summary>
        public void RoundRect(Rect r, float radius, Color c)
        {
            geometry.RoundRect(r, radius, c);
        }
        /// <summary>绘制着色器计算的高斯阴影。</summary>
        public void Shadow(Rect r, float radius, float blur, float offset, float opacity)
        {
            geometry.Shadow(r, radius, blur, offset, opacity);
        }

        /// <summary>
        /// 创建一个不可阻挡输入的文字对象，并记录其白描边组件以供复用。
        /// </summary>
        /// <returns>已加入文字池的 Unity Text 组件。</returns>
        private Text NewText()
        {
            var g = new GameObject("Canvas text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            g.transform.SetParent(transform, false);
            var t = g.GetComponent<Text>();
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.alignByGeometry = false;
            var o = g.GetComponent<Outline>();
            o.effectColor = Color.white;
            o.useGraphicAlpha = true;
            texts.Add(t);
            outlines.Add(o);
            return t;
        }

        /// <summary>
        /// 预热指定数量的文字对象，降低首个连锁或结算出现时的创建开销。
        /// </summary>
        /// <param name="n">需要预热的文字对象数量。</param>
        public void Reserve(int n)
        {
            while (texts.Count < n)
                NewText();
            foreach (var t in texts)
                t.gameObject.SetActive(false);
        }

        /// <summary>
        /// 从文字池提交标签，按中文、无衬线和衬线样式选择字体，并应用缩放、旋转和白描边。
        /// </summary>
        /// <param name="s">需要绘制或换行的字符串，或特效摆动符号。</param>
        /// <param name="p">标签锚点的局部逻辑像素坐标。</param>
        /// <param name="size">逻辑像素字号或粒径。</param>
        /// <param name="c">文字颜色。</param>
        /// <param name="isSerif">是否选择衬线字体。</param>
        /// <param name="align">文字对齐方式。</param>
        /// <param name="scale">当前文字的动画缩放倍率。</param>
        /// <param name="rotation">文字旋转角度，单位为弧度。</param>
        /// <param name="outline">白色描边的逻辑像素宽度。</param>
        /// <param name="bold">是否选择粗体字体。</param>
        /// <param name="isEmoji">是否使用手势字体后备项。</param>
        /// <param name="serifStyle">衬线样式索引：0 为顶部分数、1 为结算分数、2 为加分浮字。</param>
        public void Label(string s, Vector2 p, float size, Color c, bool isSerif = false, TextAnchor align = TextAnchor.MiddleCenter, float scale = 1, float rotation = 0, float outline = 0, bool bold = false, bool isEmoji = false, int serifStyle = 0)
        {
            if (c.a <= 0 || string.IsNullOrEmpty(s))
                return;
            Text t = textUsed < texts.Count ? texts[textUsed] : NewText();
            var o = outlines[textUsed++];
            t.gameObject.SetActive(true);
            bool cn = false;
            foreach (char ch in s)
                if (ch > 255 && ch != '×' && ch != '！')
                {
                    cn = true;
                    break;
                }

            t.font = isEmoji ? emoji : cn ? (bold && chineseBold ? chineseBold : chinese) : isSerif ? (serifStyle == 1 ? serifBig : serifStyle == 2 ? serifFloat : serif) : (bold && interBold ? interBold : inter);
            t.text = s;
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
            t.color = c;
            t.fontStyle = FontStyle.Normal;
            t.alignment = align;
            t.lineSpacing = 1.35f;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(align == TextAnchor.MiddleLeft ? 0 : align == TextAnchor.MiddleRight ? 1 : .5f, .5f);
            p = P(p);
            rt.anchoredPosition = new Vector2(p.x, -p.y);
            rt.sizeDelta = new Vector2(1000, size * 3);
            rt.localScale = Vector3.one * zoom * scale;
            rt.localRotation = Quaternion.Euler(0, 0, -rotation * Mathf.Rad2Deg);
            o.enabled = outline > 0;
            o.effectDistance = new Vector2(outline * .5f, -outline * .5f);
        }

        /// <summary>
        /// 禁用网格绘制层的 UI 射线检测，点击由游戏控制器按原 HTML 规则统一处理。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }
    }
}
