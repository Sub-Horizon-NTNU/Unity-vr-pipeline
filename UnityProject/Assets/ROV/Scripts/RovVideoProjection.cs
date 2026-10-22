using System;
using UnityEngine;

namespace ROVVR
{
    public static class RovVideoProjection
    {
        public const float ReferenceDistance = 2f;

        public static float ApparentHorizontalFov(float horizontalFov, float curve, float distance, bool cylindrical)
        {
            Vector3 edge = cylindrical ? AdjustablePoint(1f, 0.5f, horizontalFov, 16f / 9f, ReferenceDistance, curve)
                : Point(1f, 0.5f, horizontalFov, 16f / 9f, ReferenceDistance);
            return 2f * Mathf.Atan2(edge.x, distance + edge.z) * Mathf.Rad2Deg;
        }

        public static float ApparentVerticalFov(float horizontalFov, float curve, float distance, bool cylindrical)
        {
            Vector3 edge = cylindrical ? AdjustablePoint(0.5f, 1f, horizontalFov, 16f / 9f, ReferenceDistance, curve)
                : Point(0.5f, 1f, horizontalFov, 16f / 9f, ReferenceDistance);
            return 2f * Mathf.Atan2(edge.y, distance + edge.z) * Mathf.Rad2Deg;
        }
        public static float VerticalFov(float horizontalFov, float aspect)
        {
            return 2f * Mathf.Atan(Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2f) / aspect) * Mathf.Rad2Deg;
        }

        public static float CenterPixelsPerDegree(int width, float horizontalFov)
        {
            return width * Mathf.Deg2Rad / (2f * Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2f));
        }

        // A pinhole image is rectilinear, not an equirectangular panorama.
        // Project each source-image ray onto a sphere about the viewer. This
        // preserves perspective and source aspect ratio at the viewing origin.
        public static Vector3 Point(float u, float v, float horizontalFov, float aspect, float radius)
        {
            float tanHalf = Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2f);
            var ray = new Vector3((2f * u - 1f) * tanHalf, (2f * v - 1f) * tanHalf / aspect, 1f);
            return ray.normalized * radius - Vector3.forward * radius;
        }

        public static float CylinderVerticalFov(float horizontalFov, float aspect)
        {
            return 2f * Mathf.Atan(horizontalFov * Mathf.Deg2Rad / (2f * aspect)) * Mathf.Rad2Deg;
        }

        public static Quaternion ViewOrientation(Quaternion head, Quaternion heading, bool followPitch,
            bool followRoll, float verticalOffset, float rollOffset)
        {
            float pitch = followPitch ? RovHeadTiltMath.Pitch(head) : 0f;
            Vector3 forward = head * Vector3.forward;
            Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, forward);
            float roll = followRoll && levelUp.sqrMagnitude > 0.001f
                ? Vector3.SignedAngle(levelUp, head * Vector3.up, forward) : 0f;
            return heading * Quaternion.Euler(-pitch, 0f, 0f)
                * Quaternion.AngleAxis(roll + rollOffset, Vector3.forward)
                * Quaternion.Euler(-verticalOffset, 0f, 0f);
        }

        public static Vector3 CylinderPoint(float u, float v, float horizontalFov, float aspect, float radius)
        {
            float arc = horizontalFov * Mathf.Deg2Rad;
            float angle = (u - 0.5f) * arc;
            return new Vector3(Mathf.Sin(angle) * radius, (v - 0.5f) * radius * arc / aspect,
                (Mathf.Cos(angle) - 1f) * radius);
        }

        public static float CurvedArcWidth(float distance, float horizontalFov, float curveDegrees)
        {
            float tangent = Mathf.Tan(horizontalFov * Mathf.Deg2Rad * 0.5f);
            if (curveDegrees < 0.01f) return 2f * distance * tangent;
            float halfCurve = curveDegrees * Mathf.Deg2Rad * 0.5f;
            float radius = distance * tangent / (Mathf.Sin(halfCurve) + tangent * (1f - Mathf.Cos(halfCurve)));
            return radius * 2f * halfCurve;
        }

        // Keep endpoint viewing angles constant while changing only the bend.
        public static Vector3 AdjustablePoint(float u, float v, float horizontalFov, float aspect, float distance, float curveDegrees)
        {
            float width = CurvedArcWidth(distance, horizontalFov, curveDegrees);
            if (curveDegrees < 0.01f) return new Vector3((u - 0.5f) * width, (v - 0.5f) * width / aspect, 0f);
            float arc = curveDegrees * Mathf.Deg2Rad;
            float radius = width / arc;
            float angle = (u - 0.5f) * arc;
            return new Vector3(Mathf.Sin(angle) * radius, (v - 0.5f) * width / aspect, (Mathf.Cos(angle) - 1f) * radius);
        }

        public static Mesh Create(int sourceWidth, int sourceHeight, float horizontalFov,
            float radius, int columns, int rows, bool flipHorizontal, bool flipVertical, bool cylindrical = false, float curveDegrees = -1f)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || radius <= 0f
                || horizontalFov < 30f || horizontalFov > (cylindrical ? 280f : 140f) || columns < 2 || rows < 2
                || (columns + 1) * (rows + 1) > 65000)
                throw new ArgumentOutOfRangeException("Invalid video projection dimensions.");
            float aspect = (float)sourceWidth / sourceHeight;
            int stride = columns + 1;
            var vertices = new Vector3[stride * (rows + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++)
            {
                float v = (float)y / rows;
                for (int x = 0; x <= columns; x++)
                {
                    float u = (float)x / columns;
                    int i = y * stride + x;
                    vertices[i] = cylindrical && curveDegrees >= 0f
                        ? AdjustablePoint(u, v, horizontalFov, aspect, radius, curveDegrees)
                        : cylindrical ? CylinderPoint(u, v, horizontalFov, aspect, radius)
                        : Point(u, v, horizontalFov, aspect, radius);
                    var inward = -(vertices[i] + Vector3.forward * radius);
                    if (cylindrical) inward.y = 0f;
                    normals[i] = inward.normalized;
                    if (cylindrical && curveDegrees >= 0f)
                    {
                        float angle = (u - 0.5f) * curveDegrees * Mathf.Deg2Rad;
                        normals[i] = new Vector3(-Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                    }
                    uv[i] = new Vector2(flipHorizontal ? 1f - u : u, flipVertical ? 1f - v : v);
                    if (x == columns || y == rows) continue;
                    int t = (y * columns + x) * 6;
                    triangles[t] = i;
                    triangles[t + 1] = i + stride;
                    triangles[t + 2] = i + 1;
                    triangles[t + 3] = i + 1;
                    triangles[t + 4] = i + stride;
                    triangles[t + 5] = i + stride + 1;
                }
            }
            var mesh = new Mesh { name = cylindrical ? "ROV Curved Cinema Surface" : "ROV Perspective Video Surface", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
