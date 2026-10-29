using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace ROVVR.Editor
{
    public static class RovRcGateLiveCheck
    {
        [InitializeOnLoadMethod] private static void Register() { EditorApplication.update -= Request; EditorApplication.update += Request; }
        private static void Request()
        {
            const string path = "Temp/rov-rc-gate.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(path).Trim(); File.Delete(path);
            if (action == "restore") Restore(); else if (action == "verify") Verify();
        }
        [MenuItem("ROV/Restore camera RC mappings from recovery file")]
        public static async void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Camera control: RC in Play, or stop Play first");
            var gate = new RovRcCameraGate("http://192.168.2.2:6040");
            try { await gate.RestoreAsync(); File.WriteAllText("Temp/rov-rc-gate-result.json", "{\"success\":true,\"message\":\"RC restored\"}"); }
            catch (Exception e) { Error(e); }
        }
        public static async void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before gate-only verification");
            var gate = new RovRcCameraGate("http://192.168.2.2:6040");
            Exception failure = null;
            try { await gate.AcquireAsync(); }
            catch (Exception e) { failure = e; }
            finally { try { await gate.RestoreAsync(); } catch (Exception e) { failure = e; } }
            if (failure != null) Error(failure);
            else { File.WriteAllText("Temp/rov-rc-gate-result.json", "{\"success\":true,\"message\":\"Camera RC mappings suppressed and restored; no tilt targets sent\"}"); Debug.Log("[ROV RC gate] Live acquire/restore passed; no tilt targets sent"); }
        }
        private static void Error(Exception e) { File.WriteAllText("Temp/rov-rc-gate-result.json", JsonUtility.ToJson(new Failure { message = e.ToString() })); Debug.LogException(e); }
        [Serializable] private sealed class Failure { public bool success; public string message; }
    }
}
