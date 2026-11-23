using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>Small line icons that share the game's arrow geometry and palette.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HueRetentionIcon : MaskableGraphic
    {
        public enum Shape { Profile, Phone, Menu, Minis, Close, Arrow, Check, Person, Chevron, MusicNote, Cube }
        public Shape shape;
        private VertexHelper mesh;
        private Rect bounds;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); mesh = helper; bounds = GetPixelAdjustedRect();
            switch (shape)
            {
                case Shape.Profile:
                    Line(-.32f, -.35f, -.32f, .35f); Line(-.32f, .35f, .06f, .35f);
                    Line(-.32f, -.35f, .06f, -.35f); Line(.03f, .35f, .03f, .18f);
                    Line(.03f, -.35f, .03f, -.18f); Arrow(-.11f, 0, .38f, 0); break;
                case Shape.Phone:
                    Line(-.24f, -.37f, -.24f, .37f); Line(-.24f, .37f, .24f, .37f);
                    Line(.24f, .37f, .24f, -.37f); Line(.24f, -.37f, -.24f, -.37f);
                    Line(-.04f, -.25f, .04f, -.25f); break;
                case Shape.Menu:
                    for (int i = -1; i <= 1; i++) Line(-.32f, i * .23f, .32f, i * .23f); break;
                case Shape.Minis:
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                        {
                            float a = x * .21f, b = y * .21f;
                            Line(a - .1f, b - .1f, a + .1f, b - .1f);
                            Line(a + .1f, b - .1f, a + .1f, b + .1f);
                            Line(a + .1f, b + .1f, a - .1f, b + .1f);
                            Line(a - .1f, b + .1f, a - .1f, b - .1f);
                        }
                    break;
                case Shape.Close: Line(-.22f, -.22f, .22f, .22f); Line(-.22f, .22f, .22f, -.22f); break;
                case Shape.Check: Line(-.3f, 0, -.08f, -.22f); Line(-.08f, -.22f, .32f, .25f); break;
                case Shape.Arrow: Arrow(-.34f, 0, .34f, 0); break;
                case Shape.Chevron: Line(-.12f, -.24f, .12f, 0); Line(.12f, 0, -.12f, .24f); break;
                case Shape.Person:
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                        Line(Mathf.Cos(a) * .13f, .2f + Mathf.Sin(a) * .13f,
                            Mathf.Cos(b) * .13f, .2f + Mathf.Sin(b) * .13f);
                    }
                    Line(-.28f, -.3f, -.2f, -.05f); Line(-.2f, -.05f, .2f, -.05f); Line(.2f, -.05f, .28f, -.3f);
                    break;
                case Shape.MusicNote:
                    Line(.07f, .36f, .07f, -.2f); Line(.07f, .36f, .32f, .19f); Line(.32f, .19f, .32f, .04f);
                    Disc(-.075f, -.2f, .18f); break;
                case Shape.Cube:
                    Line(0, .36f, .31f, .18f); Line(.31f, .18f, .31f, -.18f); Line(.31f, -.18f, 0, -.36f);
                    Line(0, -.36f, -.31f, -.18f); Line(-.31f, -.18f, -.31f, .18f); Line(-.31f, .18f, 0, .36f);
                    Line(-.31f, .18f, 0, 0); Line(.31f, .18f, 0, 0); Line(0, 0, 0, -.36f); break;
            }
        }
        private void Disc(float x, float y, float radius)
        {
            float size = Mathf.Min(bounds.width, bounds.height);
            var center = bounds.center + new Vector2(x, y) * size;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * size, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
        private void Arrow(float x, float y, float endX, float endY)
        {
            Line(x, y, endX, endY); Line(endX - .19f, endY + .19f, endX, endY);
            Line(endX - .19f, endY - .19f, endX, endY);
        }
        private void Line(float x, float y, float endX, float endY)
        {
            float size = Mathf.Min(bounds.width, bounds.height);
            var a = bounds.center + new Vector2(x, y) * size;
            var b = bounds.center + new Vector2(endX, endY) * size;
            var d = (b - a).normalized;
            var normal = new Vector2(-d.y, d.x) * size * .035f;
            int n = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
