using System;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovVideoViewChecks
    {
        public static int Run()
        {
            const float fov = 80f, radius = 2f, aspect = 1920f / 1080f;
            int checks = 0;
            float vertical = RovVideoProjection.VerticalFov(fov, aspect);
            Near(Mathf.Tan(vertical * Mathf.Deg2Rad / 2f) * aspect, Mathf.Tan(fov * Mathf.Deg2Rad / 2f)); checks++;
            var origin = new Vector3(0f, 0f, -radius);
            Near(Vector3.Distance(RovVideoProjection.Point(0.5f, 0.5f, fov, aspect, radius), Vector3.zero), 0f); checks++;
            var right = RovVideoProjection.Point(1f, 0.5f, fov, aspect, radius) - origin;
            Near(Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg, 40f); checks++;
            var top = RovVideoProjection.Point(0.5f, 1f, fov, aspect, radius) - origin;
            Near(Mathf.Atan2(top.y, top.z) * Mathf.Rad2Deg, vertical / 2f); checks++;
            Near(RovVideoProjection.CenterPixelsPerDegree(1920, fov), 19.968f, 0.01f); checks++;

            var mesh = RovVideoProjection.Create(1920, 1080, fov, radius, 96, 54, false, true);
            try
            {
                Expect(mesh.vertexCount == 97 * 55, "Unexpected vertex count"); checks++;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                foreach (var vertex in vertices) Near(Vector3.Distance(vertex, origin), radius);
                checks++;
                for (int i = 0; i < vertices.Length; i++) Near(Vector3.Dot(normals[i], (origin - vertices[i]).normalized), 1f);
                checks++;
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Expect(Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normals[a]) > 0f,
                        "Triangle faces away from the viewer");
                }
                checks++;
                Near(mesh.uv[0].x, 0f); Near(mesh.uv[0].y, 1f); checks++;
                Near(mesh.uv[96].x, 1f); Near(mesh.uv[mesh.vertexCount - 1].y, 0f); checks++;
                for (int y = 0; y <= 4; y++) for (int x = 0; x <= 4; x++)
                {
                    float u = x / 4f, v = y / 4f;
                    var ray = RovVideoProjection.Point(u, v, fov, aspect, radius) - origin;
                    float tanHalf = Mathf.Tan(fov * Mathf.Deg2Rad / 2f);
                    Near(ray.x / ray.z, (2f * u - 1f) * tanHalf);
                    Near(ray.y / ray.z, (2f * v - 1f) * tanHalf / aspect);
                }
                checks++;
                var farther = RovVideoProjection.Point(0.8f, 0.7f, fov, aspect, 3f) + Vector3.forward * 3f;
                var nearer = RovVideoProjection.Point(0.8f, 0.7f, fov, aspect, radius) - origin;
                Near(Vector3.Angle(farther, nearer), 0f, 0.05f); checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            var cylinder = RovVideoProjection.Create(1920, 1080, 100f, radius, 96, 54, false, true, true);
            try
            {
                foreach (var vertex in cylinder.vertices)
                    Near(new Vector2(vertex.x, vertex.z + radius).magnitude, radius);
                checks++;
                var edge = RovVideoProjection.CylinderPoint(1f, 0.5f, 100f, aspect, radius) - origin;
                Near(Mathf.Atan2(edge.x, edge.z) * Mathf.Rad2Deg, 50f); checks++;
                var lower = RovVideoProjection.CylinderPoint(0.5f, 0f, 100f, aspect, radius);
                var upper = RovVideoProjection.CylinderPoint(0.5f, 1f, 100f, aspect, radius);
                Near(radius * 100f * Mathf.Deg2Rad / Vector3.Distance(lower, upper), aspect); checks++;
                Expect(RovVideoProjection.CylinderPoint(1f, 0.5f, 100f, aspect, radius).z < -0.7f,
                    "Cylinder must visibly curve toward the viewer at its sides"); checks++;
                var vertices = cylinder.vertices; var normals = cylinder.normals; var triangles = cylinder.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    Expect(Vector3.Dot(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]), normals[triangles[i]]) > 0f, "Cylinder winding");
                checks++;
                var lowered = Quaternion.Euler(8f, 0f, 0f) * Vector3.forward;
                Near(Mathf.Atan2(lowered.y, lowered.z) * Mathf.Rad2Deg, -8f); checks++;
                var head = Quaternion.Euler(-12f, 20f, 7f);
                Near(Quaternion.Angle(RovVideoProjection.ViewOrientation(head, Quaternion.Euler(0f, 20f, 0f),
                    true, true, 0f, 0f), head), 0f, 0.05f); checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(cylinder); }
            return checks;
        }

        private static void Near(float actual, float expected, float tolerance = 0.0001f)
        {
            Expect(!float.IsNaN(actual) && Mathf.Abs(actual - expected) <= tolerance, $"Expected {expected}, got {actual}");
        }
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Video projection check: " + message);
        }
    }
}
