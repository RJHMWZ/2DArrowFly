using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>小尺寸设置齿轮，使用 UI 网格，无新增贴图。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HueSettingsIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            for (int i = 0; i <= 64; i++)
            {
                float angle = i * Mathf.PI / 32;
                float radius = (i % 8 >= 2 && i % 8 <= 5 ? .48f : .37f) * size;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(rect.center + direction * radius, color, Vector2.zero);
                mesh.AddVert(rect.center + direction * (.20f * size), color, Vector2.zero);
                if (i == 0) continue;
                int n = i * 2;
                mesh.AddTriangle(n - 2, n - 1, n);
                mesh.AddTriangle(n, n - 1, n + 1);
            }
        }
    }
}
