using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovPilotTelemetryChecks
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }

        private static void ProcessRequest()
        {
            const string request = "Temp/rov-pilot-telemetry.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            var result = new CheckResult();
            try { result.checks = Run(); result.ok = true; result.message = "Network-free pilot telemetry checks passed."; }
            catch (Exception error) { result.message = error.ToString(); Debug.LogException(error); }
            File.WriteAllText("Temp/rov-pilot-telemetry-result.json", JsonUtility.ToJson(result, true));
        }

        public static int Run()
        {
            int checks = 0;
            var data = new RovHudTelemetry();
            Expect(float.IsNaN(data.PilotGain(0f)) && float.IsNaN(data.Light1Level(0f)) && float.IsNaN(data.Light2Level(0f)), "Absent levels must be unknown"); checks++;
            Expect(data.ObservePilotStream(Frame("PilotGain", "0.5"), 0f), "Actual BlueOS gain frame must parse");
            Near(data.PilotGain(0f), 0.5f); checks++;
            Expect(data.ObservePilotStream(Frame("Lights1", "0"), 0.1f), "Zero is a valid light output command");
            Near(data.Light1Level(0.1f), 0f); checks++;
            Expect(data.ObservePilotStream(Frame("Lights2", "0.5"), 0.2f), "Second light channel must remain distinct");
            Near(data.Light2Level(0.2f), 0.5f); Near(data.Light1Level(0.2f), 0f); Near(data.PilotGain(0.2f), 0.5f); checks++;
            Expect(!data.ObservePilotStream(Frame("PilotGain", "1", 2, 1), 1f), "Wrong vehicle must be ignored");
            Expect(!data.ObservePilotStream(Frame("PilotGain", "1", 1, 2), 1f), "Wrong component must be ignored");
            Near(data.PilotGain(1f), 0.5f); checks++;
            Expect(!data.ObservePilotStream(Frame("CamTilt", "0.9"), 2f), "Other named values must not overwrite pilot telemetry");
            Near(data.PilotGain(2f), 0.5f); checks++;
            Expect(!data.ObservePilotStream(Frame("PilotGain", "0.8").Replace("NAMED_VALUE_FLOAT", "NAMED_VALUE_INT"), 2f), "Message type must match"); checks++;
            Expect(!data.ObservePilotStream("{broken", 2f), "Malformed JSON must be rejected"); checks++;
            Expect(!data.ObservePilotStream(Frame("PilotGain", "0.8").Replace("\"P\",", "\"P\\u0000\","), 2f), "Each name element must be exactly one character"); checks++;
            Expect(!data.ObservePilotStream(Frame("PilotGain", "0.8").Replace("\"i\",", "\"\\u0000\","), 2f), "Embedded name terminator must not allow a suffix"); checks++;
            data.ObservePilotStream(Frame("Lights1", "1"), 2.9f);
            Expect(float.IsNaN(data.PilotGain(3f)), "A light update cannot refresh expired gain");
            Near(data.Light1Level(3f), 1f); checks++;
            Expect(float.IsNaN(data.Light2Level(3.2f)), "Each light expires independently at three seconds"); checks++;
            data.ObservePilotStream(Frame("PilotGain", "0"), 4f);
            Near(data.PilotGain(4f), 0f); checks++;
            Expect(!data.ObservePilotStream(Frame("PilotGain", "-0.01"), 4.1f) && float.IsNaN(data.PilotGain(4.1f)), "Negative gain invalidates prior value"); checks++;
            Expect(!data.ObservePilotStream(Frame("Lights2", "1.01"), 4.1f) && float.IsNaN(data.Light2Level(4.1f)), "Out-of-range light output is unknown"); checks++;
            data.ObservePilotStream(Frame("PilotGain", "0.5"), 5f);
            Expect(!data.ObservePilotStream(Frame("PilotGain", "0.5").Replace(",\"value\":0.5", ""), 5.1f)
                && float.IsNaN(data.PilotGain(5.1f)), "Omitted value must not fabricate zero"); checks++;
            var nonfinite = new RovHudTelemetry();
            nonfinite.ObservePilotStream(Frame("PilotGain", "1e39"), 5f);
            Expect(float.IsNaN(nonfinite.PilotGain(5f)), "Nonfinite numeric input cannot become a valid level"); checks++;
            data.ObservePilotStream(Frame("PilotGain", "0.5"), 6f);
            Expect(!data.ObservePilotStream(Frame("PilotGain", "0.5"), float.NaN) && float.IsNaN(data.PilotGain(float.NaN))
                && float.IsNaN(data.PilotGain(5.9f)), "Invalid time and a future sample must not appear live"); checks++;
            data.Observe(new RovMountProtocol.Messages { HEARTBEAT = new RovMountProtocol.MessageEnvelope
                { message = new RovMountProtocol.TelemetryMessage { base_mode = new RovMountProtocol.ModeFlags { bits = 209 } } } }, 9f);
            Expect(data.Armed && float.IsNaN(data.PilotGain(9f)), "Heartbeat must not refresh stream fields"); checks++;
            data.ObservePilotStream(Frame("Lights1", "0.5"), 9f);
            data.ResetPilotValues();
            Expect(data.Armed && float.IsNaN(data.PilotGain(9f)) && float.IsNaN(data.Light1Level(9f)), "Stream reset clears levels without changing arm state"); checks++;
            Expect(RovPilotTelemetryStream.TryStreamUri("http://192.168.2.2:6040", out Uri stream)
                && stream.Scheme == "ws" && stream.Port == 6040 && stream.AbsolutePath == "/v1/ws/mavlink"
                && Uri.UnescapeDataString(stream.Query) == "?filter=^NAMED_VALUE_FLOAT$", "Filtered stream URL must preserve BlueOS port"); checks++;
            Expect(RovPilotTelemetryStream.TryStreamUri("https://blueos.example", out Uri secure) && secure.Scheme == "wss"
                && !RovPilotTelemetryStream.TryStreamUri("file:///tmp", out _), "Only HTTP/HTTPS sources become WebSocket endpoints"); checks++;
            using (var neverStarted = new RovPilotTelemetryStream()) { neverStarted.Dispose(); }
            checks++;
            return checks;
        }

        private static string Frame(string name, string value, int system = 1, int component = 1)
        {
            string characters = "";
            foreach (char character in name) characters += (characters.Length == 0 ? "" : ",") + "\"" + character + "\"";
            for (int i = name.Length; i < 10; i++) characters += ",\"\\u0000\"";
            return "{\"header\":{\"system_id\":" + system + ",\"component_id\":" + component
                + "},\"message\":{\"type\":\"NAMED_VALUE_FLOAT\",\"name\":[" + characters + "],\"time_boot_ms\":14043733,\"value\":" + value + "}}";
        }

        private static void Near(float actual, float expected) => Expect(RovCameraPoseMath.Finite(actual) && Mathf.Abs(actual - expected) < 0.0001f, $"Expected {expected}, received {actual}");
        private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException("ROV pilot telemetry: " + message); }
        [Serializable] private sealed class CheckResult { public bool ok; public int checks; public string message; }
    }
}
