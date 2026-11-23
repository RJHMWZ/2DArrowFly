using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>Flat rounded UI surface with a one-pixel alpha edge and no texture border.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HueRoundedGraphic : MaskableGraphic
    {
        [Min(0)] public float cornerRadius = 18;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float radius = Mathf.Clamp(cornerRadius, .01f, Mathf.Min(rect.width, rect.height) * .5f);
            float density = canvas ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            float edge = Mathf.Min(radius, 1 / density);
            int segments = Mathf.Clamp(Mathf.CeilToInt(radius * density / 4), 8, 24);
            int count = (segments + 1) * 4;
            mesh.AddVert(rect.center, color, Vector2.zero);
            var clear = color; clear.a = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner + i / (float)segments) * Mathf.PI * .5f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    mesh.AddVert(center + direction * (radius - edge), color, Vector2.zero);
                    mesh.AddVert(center + direction * radius, clear, Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int a = 1 + i * 2, b = 1 + ((i + 1) % count) * 2;
                mesh.AddTriangle(0, a, b);
                mesh.AddTriangle(a, a + 1, b + 1); mesh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
