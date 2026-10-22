using UnityEngine;
using UnityEngine.UI;

namespace ROVVR
{
    // Bake UI vertices onto the same surface as the video. The cached mesh
    // moves with its parent, so head tracking never rebuilds or repositions it.
    public sealed class RovHudSurfaceGraphic : BaseMeshEffect
    {
        public RovVideoViewCalibration view;
        public RectTransform layoutRoot;

        public static Vector3 SurfacePoint(float pixelX, float pixelY, float fov, float curve, bool cylindrical)
        {
            float u = pixelX / 1920f + 0.5f, v = pixelY / 1080f + 0.5f;
            Vector3 p = cylindrical
                ? RovVideoProjection.AdjustablePoint(u, v, fov, 16f / 9f, RovVideoProjection.ReferenceDistance, curve)
                : RovVideoProjection.Point(u, v, fov, 16f / 9f, RovVideoProjection.ReferenceDistance);
            float angle = (u - 0.5f) * curve * Mathf.Deg2Rad;
            Vector3 normal = cylindrical ? new Vector3(-Mathf.Sin(angle), 0f, -Mathf.Cos(angle))
                : -(p + Vector3.forward * RovVideoProjection.ReferenceDistance).normalized;
            return p + normal * 0.003f;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || view == null || layoutRoot == null) return;
            if (graphic is Image && vh.currentVertCount == 4) SubdividePanel(vh);
            Matrix4x4 toLayout = layoutRoot.worldToLocalMatrix * graphic.rectTransform.localToWorldMatrix;
            Matrix4x4 toGraphic = graphic.rectTransform.worldToLocalMatrix * view.transform.localToWorldMatrix;
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 pixel = toLayout.MultiplyPoint3x4(vertex.position);
                vertex.position = toGraphic.MultiplyPoint3x4(SurfacePoint(pixel.x, pixel.y,
                    view.horizontalFov, view.curveDegrees, view.cylindricalSurface));
                vh.SetUIVertex(vertex, i);
            }
        }

        private void SubdividePanel(VertexHelper vh)
        {
            // Large backing rectangles must follow the bow too; one large
            // quad would cut across the curved screen at a different depth.
            var q = new UIVertex[4];
            for (int i = 0; i < 4; i++) vh.PopulateUIVertex(ref q[i], i);
            int columns = Mathf.Clamp(Mathf.CeilToInt(graphic.rectTransform.rect.width / 48f), 1, 64);
            int rows = Mathf.Clamp(Mathf.CeilToInt(graphic.rectTransform.rect.height / 48f), 1, 16);
            vh.Clear();
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = (float)x / columns, v = (float)y / rows;
                var vertex = q[0];
                vertex.position = Vector3.Lerp(Vector3.Lerp(q[0].position, q[3].position, u), Vector3.Lerp(q[1].position, q[2].position, u), v);
                vertex.uv0 = Vector4.Lerp(Vector4.Lerp(q[0].uv0, q[3].uv0, u), Vector4.Lerp(q[1].uv0, q[2].uv0, u), v);
                vh.AddVert(vertex);
                if (x == columns || y == rows) continue;
                int at = y * (columns + 1) + x;
                vh.AddTriangle(at, at + columns + 1, at + 1);
                vh.AddTriangle(at + 1, at + columns + 1, at + columns + 2);
            }
        }
    }
}
