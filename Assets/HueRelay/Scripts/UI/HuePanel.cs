using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>
    /// 场景中可直接调整的固定 UI 图形。只有尺寸或样式改变时才重建网格，
    /// 与棋盘共用原版圆角、线宽和阴影算法，不使用默认按钮外观。
    /// </summary>
    [AddComponentMenu("Hue Relay/Fixed UI Panel")]
    public sealed class HuePanel : MaskableGraphic
    {
        public enum Shape
        {
            RoundedRectangle, Disc, RetryIcon, HintIcon
        }

        [Header("Shape")]
        public Shape shape;
        [Min(0)] public float radius = 20;
        [Header("Border")]
        [Min(0)] public float borderWidth;
        public Color borderColor = Color.white;
        [Header("Soft Shadow")]
        public bool castShadow;
        [Min(0)] public float shadowBlur = 14;
        public float shadowOffset = 4;
        public float shadowSpread;
        [Range(0, 1)] public float shadowOpacity = .08f;

        private readonly HueGeometry geometry = new HueGeometry(128, 256);

        /// <summary>按当前 RectTransform 和 Inspector 样式重建一次固定图形。</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            geometry.Clear();
            Rect bounds = rectTransform.rect;
            geometry.origin = new Vector2(bounds.xMin, -bounds.yMax);
            Rect box = new Rect(0, 0, bounds.width, bounds.height);
            Vector2 center = box.center;

            if (shape == Shape.RetryIcon)
            {
                geometry.Arc(center, 8, Mathf.PI, Mathf.Atan2(-6.2f, -5), 2.4f, color);
                geometry.Stroke(center + new Vector2(-8, -8), center + new Vector2(-8, -3), 2.4f, color);
                geometry.Stroke(center + new Vector2(-8, -3), center + new Vector2(-3, -3), 2.4f, color);
            }
            else if (shape == Shape.HintIcon)
            {
                geometry.Disc(center + new Vector2(0, -3), 7, color);
                geometry.RoundRect(new Rect(center.x - 4, center.y + 2, 8, 4), 1, color);
                geometry.RoundRect(new Rect(center.x - 3, center.y + 8, 6, 2), .5f, color);
            }
            else
            {
                float corner = shape == Shape.Disc ? Mathf.Min(box.width, box.height) * .5f : radius;
                if (castShadow)
                {
                    Rect shadow = Expand(box, shadowSpread);
                    geometry.Shadow(shadow, Mathf.Max(0, corner + shadowSpread), shadowBlur, shadowOffset, shadowOpacity);
                }
                if (borderWidth > 0)
                    geometry.RoundRect(Expand(box, borderWidth), corner + borderWidth, borderColor);
                geometry.RoundRect(box, corner, color);
            }
            geometry.Populate(vh);
        }

        /// <summary>在布局比例变化后同步样式，无变化时不触发网格重建。</summary>
        public void SetMetrics(float corner, float blur, float offset, float spread)
        {
            if (radius == corner && shadowBlur == blur && shadowOffset == offset && shadowSpread == spread)
                return;
            radius = corner;
            shadowBlur = blur;
            shadowOffset = offset;
            shadowSpread = spread;
            SetVerticesDirty();
        }

        private static Rect Expand(Rect box, float amount)
        {
            return new Rect(box.x - amount, box.y - amount, box.width + amount * 2, box.height + amount * 2);
        }
    }
}
