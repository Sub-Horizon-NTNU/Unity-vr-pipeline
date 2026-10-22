using UnityEngine;
using UnityEngine.UI;

namespace ROVVR
{
    // UI geometry shares the windows' stereo rendering path. No transparent
    // sprite texture or depth buffer can hide the cursor inside the video.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RovPointerVisualGraphic : MaskableGraphic
    {
        public bool cursor;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (cursor)
            {
                Disk(vh, r.center, 31f, new Color(0.01f, 0.025f, 0.045f, 0.95f));
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16f, b = (i + 1) * Mathf.PI / 16f;
                    Quad(vh, r.center + Circle(a, 28), r.center + Circle(b, 28), r.center + Circle(b, 22), r.center + Circle(a, 22), color);
                }
                Box(vh, new Rect(r.center.x - 3, r.center.y - 16, 6, 32), Color.white);
                Box(vh, new Rect(r.center.x - 16, r.center.y - 3, 32, 6), Color.white);
                Disk(vh, r.center, 5, color);
            }
            else
            {
                Box(vh, new Rect(r.xMin, r.yMin, r.width, 7), color);
                Box(vh, new Rect(r.xMin, r.yMax - 7, r.width, 7), color);
                Box(vh, new Rect(r.xMin, r.yMin, 7, r.height), color);
                Box(vh, new Rect(r.xMax - 7, r.yMin, 7, r.height), color);
            }
        }
        private static Vector2 Circle(float a, float radius) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        private static void Box(VertexHelper vh, Rect r, Color color) => Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), color);
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        private static void Disk(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            for (int i = 0; i < 32; i++)
            {
                int n = vh.currentVertCount;
                vh.AddVert(center, color, Vector2.zero); vh.AddVert(center + Circle(i * Mathf.PI / 16f, radius), color, Vector2.zero);
                vh.AddVert(center + Circle((i + 1) * Mathf.PI / 16f, radius), color, Vector2.zero); vh.AddTriangle(n, n + 1, n + 2);
            }
        }
    }
}
