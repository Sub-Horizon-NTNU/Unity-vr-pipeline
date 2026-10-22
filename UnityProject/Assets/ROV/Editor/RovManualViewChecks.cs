using System;
using System.IO;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovManualViewChecks
    {
        public static int Run()
        {
            int checks = 0;
            foreach (float curve in new[] {0f, 10f, 45f, 100f, 150f})
            {
                var edge = RovVideoProjection.AdjustablePoint(1f, 0.5f, 110f, 16f / 9f, 2f, curve);
                Near(Mathf.Atan2(edge.x, edge.z + 2f) * Mathf.Rad2Deg, 55f);
            }
            checks++;
            var flat = RovVideoProjection.AdjustablePoint(0.8f, 0.3f, 110f, 16f / 9f, 2f, 0f);
            Near(flat.z, 0f); checks++;
            var mildlyCurved = RovVideoProjection.AdjustablePoint(1f, 0.5f, 110f, 16f / 9f, 2f, 45f);
            Expect(mildlyCurved.z > -0.5f && mildlyCurved.z < 0f, "Mild curve must stay mild"); checks++;
            var farther = RovVideoProjection.AdjustablePoint(1f, 0.5f, 110f, 16f / 9f, 3f, 45f);
            Near(Vector3.Angle(mildlyCurved + Vector3.forward * 2f, farther + Vector3.forward * 3f), 0f); checks++;
            float nearWidth = RovVideoProjection.ApparentHorizontalFov(110f, 45f, 1.5f, true);
            float baseWidth = RovVideoProjection.ApparentHorizontalFov(110f, 45f, 2f, true);
            float farWidth = RovVideoProjection.ApparentHorizontalFov(110f, 45f, 3f, true);
            Near(baseWidth, 110f); checks++;
            Expect(nearWidth > baseWidth + 15f && farWidth < baseWidth - 20f, "Distance must visibly change picture size"); checks++;
            Expect(RovVideoProjection.ApparentVerticalFov(110f, 45f, 1.5f, true)
                > RovVideoProjection.ApparentVerticalFov(110f, 45f, 3f, true), "Vertical size must also change with distance"); checks++;
            var partialCompensation = RovVideoViewProfile.Parse("{\"schemaVersion\":1}");
            Expect(partialCompensation.compensateRovPitch, "Existing configuration must enable pitch compensation by default"); checks++;
            Near(partialCompensation.pitchCompensationStrength, 1f); checks++;
            Near(RovVideoViewProfile.Parse("{\"pitchCompensationStrength\":0.9}").pitchCompensationStrength, 0.9f); checks++;
            foreach (float curve in new[] {0f, 45f, 150f})
            {
                var mesh = RovVideoProjection.Create(1920, 1080, 110f, 2f, 96, 54, false, true, true, curve);
                try
                {
                    var vertices = mesh.vertices; var triangles = mesh.triangles; var normals = mesh.normals;
                    for (int i = 0; i < triangles.Length; i += 3)
                        Expect(Vector3.Dot(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                            vertices[triangles[i + 2]] - vertices[triangles[i]]), normals[triangles[i]]) > 0f, "Manual curve winding");
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            checks++;
            var partial = RovVideoViewProfile.Parse("{\"schemaVersion\":1,\"curveDegrees\":0}");
            Near(partial.horizontalFov, 110f); Near(partial.curveDegrees, 0f); Near(partial.verticalPositionMeters, 0f); checks++;
            bool badVersion = false;
            try { RovVideoViewProfile.Parse("{\"schemaVersion\":999}"); } catch (ArgumentException) { badVersion = true; }
            Expect(badVersion, "Unknown profile version must not overwrite settings"); checks++;
            bool badNumber = false;
            try { new RovVideoViewProfile { distance = float.NaN }.Validate(); } catch (ArgumentException) { badNumber = true; }
            Expect(badNumber, "NaN config value must be rejected"); checks++;
            var limited = RovVideoViewProfile.Parse("{\"horizontalFov\":999,\"curveDegrees\":-20,\"distance\":-1}");
            Near(limited.horizontalFov, 150f); Near(limited.curveDegrees, 0f); Near(limited.distance, 1f); checks++;

            string path = Path.Combine(Path.GetTempPath(), "rov-view-profile-check-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var profile = new RovVideoViewProfile { horizontalFov = 106f, curveDegrees = 25f, distance = 2.3f, verticalPositionMeters = -0.25f };
                profile.Save(path);
                var loaded = RovVideoViewProfile.Parse(File.ReadAllText(path));
                Near(loaded.horizontalFov, 106f); Near(loaded.curveDegrees, 25f); Near(loaded.distance, 2.3f); Near(loaded.verticalPositionMeters, -0.25f); checks++;
                profile.curveDegrees = 35f;
                profile.Save(path);
                Near(RovVideoViewProfile.Parse(File.ReadAllText(path)).curveDegrees, 35f);
                Near(RovVideoViewProfile.Parse(File.ReadAllText(path + ".bak")).curveDegrees, 25f); checks++;
            }
            finally
            {
                foreach (string file in new[] {path, path + ".bak", path + ".tmp"}) if (File.Exists(file)) File.Delete(file);
            }
            return checks;
        }

        private static void Near(float actual, float expected)
        {
            Expect(RovCameraPoseMath.Finite(actual) && Mathf.Abs(actual - expected) < 0.05f, $"Expected {expected}, received {actual}");
        }
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Manual video view check: " + message);
        }
    }
}
