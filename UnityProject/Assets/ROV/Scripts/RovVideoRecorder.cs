using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace ROVVR
{
    // Encoding and pipe writes run off the Unity thread. Only the latest frame is
    // retained, so a slow encoder never builds an unbounded queue or stalls tracking.
    public sealed class RovVideoRecorder : MonoBehaviour
    {
        private sealed class Session
        {
            public Process process;
            public volatile bool stopping, done;
            public byte[] latest;
            public volatile string status = "Starting recorder";
            public int frames;
        }
        private Session session;
        private RenderTexture target, mirror;
        private AsyncGPUReadbackRequest pending;
        private bool reading;
        public bool Busy => session != null && !session.done;
        public bool Recording => Busy && !session.stopping;
        public string Status => session == null ? "Ready / MP4 / no audio" : session.status;
        public string LastPath { get; private set; }
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SubHorizon ROV");
        public static string FindLauncher()
        {
            string[] roots = { Path.Combine(Application.dataPath, "../GStreamer"), Path.Combine(Application.dataPath, "../../Builds/pilot-tools-preview/GStreamer"), Path.Combine(Application.dataPath, "../../Builds/v1/GStreamer"), Environment.GetEnvironmentVariable("GSTREAMER_1_0_ROOT_MSVC_X86_64") };
            foreach (string root in roots)
                if (!string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, "bin/gst-launch-1.0.exe"))) return Path.GetFullPath(Path.Combine(root, "bin/gst-launch-1.0.exe"));
            throw new FileNotFoundException("GStreamer recorder missing. Run Prepare-PilotPreview.ps1 or install the complete MSVC x64 runtime.");
        }
        public void Begin(Func<Texture> source, bool presentation, bool flip, int width)
        {
            if (Busy) return;
            if (!SystemInfo.supportsAsyncGPUReadback) { session = new Session { done = true, status = "GPU readback unsupported on this PC" }; return; }
            try
            {
                if (!presentation && source() == null) throw new InvalidOperationException("No camera video yet");
                string launcher = FindLauncher(), root = Path.GetDirectoryName(Path.GetDirectoryName(launcher));
                Directory.CreateDirectory(Folder);
                LastPath = Path.Combine(Folder, "ROV-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff") + ".mp4");
                width = width >= 1920 ? 1920 : 1280; int height = width * 9 / 16;
                var info = new ProcessStartInfo(launcher, Arguments(width, height, LastPath)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true };
                info.EnvironmentVariables["PATH"] = Path.Combine(root, "bin") + ";" + Environment.GetEnvironmentVariable("PATH");
                info.EnvironmentVariables["GST_PLUGIN_SYSTEM_PATH_1_0"] = Path.Combine(root, "lib/gstreamer-1.0");
                info.EnvironmentVariables["GST_PLUGIN_PATH_1_0"] = Path.Combine(root, "lib/gstreamer-1.0");
                info.EnvironmentVariables["GST_PLUGIN_SCANNER_1_0"] = Path.Combine(root, "libexec/gstreamer-1.0/gst-plugin-scanner.exe");
                var s = new Session { process = new Process { StartInfo = info } };
                session = s;
                s.process.Start();
                s.process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) s.status = "Encoder: " + e.Data; };
                s.process.BeginErrorReadLine(); s.process.BeginOutputReadLine();
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32); target.Create();
                new Thread(() => Encode(s)) { IsBackground = true, Name = "ROV MP4 encoder" }.Start();
                StartCoroutine(Capture(s, source, presentation, flip));
            }
            catch (Exception e) { if (session != null && session.process != null) { try { session.process.Kill(); } catch {} } session = new Session { done = true, status = "Recording failed: " + e.Message }; ReleaseTargets(); }
        }
        public static string Arguments(int width, int height, string path) => "-q fdsrc fd=0 ! rawvideoparse format=rgba width=" + width + " height=" + height
            + " framerate=30/1 ! videoconvert ! video/x-raw,format=I420 ! x264enc tune=zerolatency speed-preset=ultrafast bitrate=8000 key-int-max=60 ! h264parse ! mp4mux fragment-duration=1000 ! filesink location=\"" + path.Replace('\\', '/') + "\"";
        private IEnumerator Capture(Session s, Func<Texture> source, bool presentation, bool flip)
        {
            var end = new WaitForEndOfFrame(); float next = 0;
            while (!s.stopping && !s.done)
            {
                yield return end;
                if (s.stopping || s.done || reading || Time.unscaledTime < next) continue;
                Texture tex = source();
                if (presentation)
                {
                    if (mirror == null || mirror.width != Screen.width || mirror.height != Screen.height)
                    { if (mirror != null) { mirror.Release(); Destroy(mirror); } mirror = new RenderTexture(Mathf.Max(2, Screen.width), Mathf.Max(2, Screen.height), 0); mirror.Create(); }
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(mirror); tex = mirror;
                }
                if (tex == null) continue;
                Graphics.Blit(tex, target, new Vector2(1, flip ? -1 : 1), new Vector2(0, flip ? 1 : 0));
                next = Time.unscaledTime + 1f / 30f; reading = true;
                pending = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, r =>
                {
                    reading = false;
                    if (r.hasError) { s.status = "GPU capture failed"; s.stopping = true; }
                    else if (!s.stopping && !s.done) Interlocked.Exchange(ref s.latest, r.GetData<byte>().ToArray());
                });
            }
        }
        private static void Encode(Session s)
        {
            try
            {
                byte[] frame = null; var clock = new Stopwatch(); int tick = 0;
                while (!s.stopping)
                {
                    if (s.process.HasExited) throw new IOException("Encoder exited (" + s.process.ExitCode + "): " + s.status);
                    var incoming = Interlocked.Exchange(ref s.latest, null);
                    if (incoming != null) frame = incoming;
                    if (frame == null) { Thread.Sleep(5); continue; }
                    if (!clock.IsRunning) clock.Start();
                    double wait = tick * (1000.0 / 30.0) - clock.Elapsed.TotalMilliseconds;
                    if (wait > 1) { Thread.Sleep((int)Math.Min(10, wait)); continue; }
                    s.process.StandardInput.BaseStream.Write(frame, 0, frame.Length); tick++; s.frames++;
                    s.status = "REC " + (s.frames / 30) + " s / MP4";
                }
                s.status = "Finalizing MP4..."; s.process.StandardInput.Close();
                if (!s.process.WaitForExit(15000)) { s.process.Kill(); throw new IOException("Encoder finalization timed out"); }
                if (s.process.ExitCode != 0) throw new IOException("Encoder failed: " + s.status);
                s.status = s.frames > 0 ? "Saved MP4 / " + (s.frames / 30) + " s" : "No frames captured";
            }
            catch (Exception e) { s.status = "Recording failed: " + e.Message; try { s.process.Kill(); } catch {} }
            finally { s.process.Dispose(); s.done = true; }
        }
        public void Stop()
        {
            if (session != null) session.stopping = true;
            StopAllCoroutines();
            if (reading) { pending.WaitForCompletion(); reading = false; }
            ReleaseTargets();
        }
        private void ReleaseTargets() { if (target != null) { target.Release(); Destroy(target); target = null; } if (mirror != null) { mirror.Release(); Destroy(mirror); mirror = null; } }
        private void OnDisable() => Stop();
    }
}
