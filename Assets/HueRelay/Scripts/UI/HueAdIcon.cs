using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    // Smooth video-ad symbol using UI geometry and vertex-alpha edge coverage.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HueAdIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float size = Mathf.Min(r.width, r.height);
            float feather = Mathf.Min(.65f, size * .035f);
            float thickness = size * .075f;
            r = new Rect(r.xMin + feather, r.yMin + feather, r.width - feather * 2, r.height - feather * 2);
            RoundedFrame(mesh, r, thickness, feather);
            RoundedPlay(mesh, r, feather);
        }

        private void RoundedFrame(VertexHelper mesh, Rect r, float thickness, float feather)
        {
            const int segments = 8;
            const int points = 4 * (segments + 1);
            float radius = Mathf.Min(r.width, r.height) * .28f;
            float innerRadius = radius - thickness;
            Color transparent = color; transparent.a = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                    corner < 2 ? r.yMax - radius : r.yMin + radius);
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (corner * 90 + step * 90f / segments) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    mesh.AddVert(center + direction * (radius + feather), transparent, Vector2.zero);
                    mesh.AddVert(center + direction * radius, color, Vector2.zero);
                    mesh.AddVert(center + direction * innerRadius, color, Vector2.zero);
                    mesh.AddVert(center + direction * (innerRadius - feather), transparent, Vector2.zero);
                }
            }
            for (int point = 0; point < points; point++)
            {
                int a = point * 4, b = (point + 1) % points * 4;
                for (int band = 0; band < 3; band++)
                {
                    mesh.AddTriangle(a + band, b + band, b + band + 1);
                    mesh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }

        private void RoundedPlay(VertexHelper mesh, Rect r, float feather)
        {
            const int segments = 5;
            const int points = 3 * (segments + 1);
            int start = mesh.currentVertCount;
            Vector2 center = r.center + new Vector2(r.width * .025f, 0);
            mesh.AddVert(center, color, Vector2.zero);
            Color transparent = color; transparent.a = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                Vector2 tip = PlayPoint(r, corner);
                Vector2 from = Vector2.Lerp(tip, PlayPoint(r, (corner + 2) % 3), .15f);
                Vector2 to = Vector2.Lerp(tip, PlayPoint(r, (corner + 1) % 3), .15f);
                for (int step = 0; step <= segments; step++)
                {
                    float t = step / (float)segments;
                    Vector2 p = (1 - t) * (1 - t) * from + 2 * t * (1 - t) * tip + t * t * to;
                    mesh.AddVert(p, color, Vector2.zero);
                    mesh.AddVert(p + (p - center).normalized * feather, transparent, Vector2.zero);
                }
            }
            for (int point = 0; point < points; point++)
            {
                int a = start + 1 + point * 2, b = start + 1 + (point + 1) % points * 2;
                mesh.AddTriangle(start, a, b);
                mesh.AddTriangle(a, a + 1, b + 1); mesh.AddTriangle(a, b + 1, b);
            }
        }

        private static Vector2 PlayPoint(Rect r, int corner)
        {
            return r.center + (corner == 0 ? new Vector2(-.12f * r.width, -.24f * r.height)
                : corner == 1 ? new Vector2(-.12f * r.width, .24f * r.height)
                : new Vector2(.24f * r.width, 0));
        }
    }
}
