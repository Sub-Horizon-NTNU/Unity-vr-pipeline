using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
namespace ROVVR.Editor
{
    public static class RovRecordingChecks
    {
        private static RovVideoRecorder live;
        private static Texture2D pattern;
        private static double stopAt;
        [InitializeOnLoadMethod] private static void Register() { EditorApplication.update -= Request; EditorApplication.update += Request; }
        private static void Request()
        {
            if (live != null && EditorApplication.timeSinceStartup >= stopAt)
            {
                if (live.Recording) live.Stop();
                if (!live.Busy)
                {
                    File.WriteAllText("Temp/rov-recording-gpu-result.json", JsonUtility.ToJson(new CaptureResult { status = live.Status, path = live.LastPath }));
                    UnityEngine.Object.Destroy(live.gameObject); UnityEngine.Object.Destroy(pattern); live = null;
                }
            }
            if (!File.Exists("Temp/rov-recording-test.request") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText("Temp/rov-recording-test.request").Trim();
            File.Delete("Temp/rov-recording-test.request");
            if (action == "gpu" || action == "mirror") { VerifyGpu(action == "mirror"); return; }
            VerifyEncoder();
        }
        private static void VerifyGpu(bool mirror)
        {
            if (!Application.isPlaying || live != null) throw new InvalidOperationException("GPU recording test requires Play and no running test");
            pattern = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = i < pixels.Length / 2 ? new Color32(255, 0, 0, 255) : new Color32(0, 255, 0, 255);
            pattern.SetPixels32(pixels); pattern.Apply();
            live = new GameObject("ROV GPU recording check").AddComponent<RovVideoRecorder>();
            live.Begin(() => pattern, mirror, true, 1280); stopAt = EditorApplication.timeSinceStartup + 3;
        }
        [Serializable] private sealed class CaptureResult { public string status, path; }
        [MenuItem("ROV/View/Verify MP4 encoder (no movement)")]
        public static void VerifyEncoder()
        {
            string result = "Temp/rov-recording-test-result.json";
            try
            {
                string launcher = RovVideoRecorder.FindLauncher();
                string root = Path.GetDirectoryName(Path.GetDirectoryName(launcher));
                string path = Path.GetFullPath("Temp/rov-encoder-check.mp4");
                var info = new ProcessStartInfo(launcher, RovVideoRecorder.Arguments(64, 64, path)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
                info.EnvironmentVariables["PATH"] = Path.Combine(root, "bin") + ";" + Environment.GetEnvironmentVariable("PATH");
                info.EnvironmentVariables["GST_PLUGIN_SYSTEM_PATH_1_0"] = Path.Combine(root, "lib/gstreamer-1.0");
                using (var p = Process.Start(info))
                {
                    var errors = p.StandardError.ReadToEndAsync();
                    byte[] frame = new byte[64 * 64 * 4];
                    for (int i = 0; i < 30; i++) { for (int j = 0; j < frame.Length; j += 4) { frame[j] = (byte)(i * 8); frame[j + 1] = 160; frame[j + 3] = 255; } p.StandardInput.BaseStream.Write(frame, 0, frame.Length); }
                    p.StandardInput.Close();
                    if (!p.WaitForExit(10000)) { p.Kill(); throw new IOException("Encoder timeout"); }
                    if (p.ExitCode != 0 || new FileInfo(path).Length < 1000) throw new IOException("Encoder failed: " + errors.Result);
                }
                File.WriteAllText(result, "{\"success\":true,\"path\":\"Temp/rov-encoder-check.mp4\"}");
                UnityEngine.Debug.Log("[ROV recording] MP4 smoke test passed: " + path);
            }
            catch (Exception e) { File.WriteAllText(result, JsonUtility.ToJson(new Error { message = e.ToString() })); UnityEngine.Debug.LogException(e); }
        }
        [Serializable] private sealed class Error { public bool success; public string message; }
    }
}
