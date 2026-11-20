using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HueBackIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            float size = Mathf.Min(r.width, r.height);
            Vector2 tip = r.center + new Vector2(-.32f, 0) * size;
            Segment(mesh, tip, r.center + new Vector2(.02f, .3f) * size, size * .12f);
            Segment(mesh, tip, r.center + new Vector2(.02f, -.3f) * size, size * .12f);
            Segment(mesh, tip, r.center + new Vector2(.34f, 0) * size, size * .12f);
        }
        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            var d = (b-a).normalized;
            var n = new Vector2(-d.y,d.x) * width/2;
            int i = mesh.currentVertCount;
            mesh.AddVert(a+n,color,Vector2.zero); mesh.AddVert(b+n,color,Vector2.zero);
            mesh.AddVert(b-n,color,Vector2.zero); mesh.AddVert(a-n,color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
