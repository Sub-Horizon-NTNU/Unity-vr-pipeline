using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ROVVR
{
    // Only the mount's AUX options are changed. Never disable vehicle RC overrides.
    // Journal before writes; verify every write; retain journal on failed restoration.
    public sealed class RovRcCameraGate
    {
        [Serializable] public sealed class Entry { public string name; public float value; }
        [Serializable] private sealed class Journal { public string url; public List<Entry> entries = new List<Entry>(); }
        [Serializable] private sealed class ParamEnvelope { public ParamMessage message; }
        [Serializable] private sealed class ParamMessage { public string[] param_id; public float param_value; }
        private readonly string url, path;
        private readonly Func<string, Task<float>> read;
        private readonly Func<string, float, Task> write;
        private FileStream lease;
        private Journal journal;
        public bool Active { get; private set; }
        public bool PendingRestore => File.Exists(path);
        public string JournalPath => path;
        public RovRcCameraGate(string baseUrl, string journalPath = null, Func<string, Task<float>> reader = null, Func<string, float, Task> writer = null)
        {
            url = baseUrl.TrimEnd('/');
            using (var sha = SHA256.Create())
                path = journalPath ?? Path.Combine(Application.persistentDataPath, "rov-camera-rc-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "").Substring(0, 16) + ".json");
            read = reader ?? ReadParameter; write = writer ?? WriteParameter;
        }
        public static bool CameraOption(float value) => value == 212 || value == 213 || value == 214;
        public static string ParameterName(string[] chars) => chars == null ? "" : string.Concat(chars).Replace("\\u0000", "").TrimEnd('\0');
        public static bool AllowedEntry(Entry entry)
        {
            if (entry == null || !CameraOption(entry.value)) return false;
            for (int i = 1; i <= 16; i++) if (entry.name == "RC" + i + "_OPTION") return true;
            return false;
        }
        private void Lock()
        {
            if (lease != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private Journal Load()
        {
            var saved = JsonUtility.FromJson<Journal>(File.ReadAllText(path));
            if (saved == null || saved.url != url || saved.entries == null) throw new IOException("Invalid RC recovery journal: " + path);
            var names = new HashSet<string>();
            foreach (var e in saved.entries) if (!AllowedEntry(e) || !names.Add(e.name)) throw new IOException("Invalid camera option in recovery journal");
            return saved;
        }
        public async Task AcquireAsync()
        {
            if (Active) return;
            Lock();
            try
            {
                // Recover a prior interrupted session before taking a new snapshot.
                if (File.Exists(path)) { journal = Load(); await RestoreEntries(); }
                journal = new Journal { url = url };
                for (int i = 1; i <= 16; i++)
                {
                    string name = "RC" + i + "_OPTION"; float value = await read(name);
                    if (CameraOption(value)) journal.entries.Add(new Entry { name = name, value = value });
                }
                // The final file must be durable before any autopilot change occurs.
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(journal, true));
                File.Move(path + ".tmp", path);
                foreach (var e in journal.entries) await SetVerified(e.name, 0);
                Active = true;
            }
            catch
            {
                try { if (journal != null && File.Exists(path)) await RestoreEntries(); }
                finally { Active = false; lease?.Dispose(); lease = null; }
                throw;
            }
        }
        public async Task RestoreAsync()
        {
            Lock();
            try
            {
                if (File.Exists(path)) { journal = Load(); await RestoreEntries(); }
                Active = false;
            }
            finally { Active = false; lease?.Dispose(); lease = null; }
        }
        private async Task RestoreEntries()
        {
            foreach (var e in journal.entries)
            {
                float current = await read(e.name);
                if (current == e.value) continue;
                if (current != 0) throw new IOException(e.name + " changed externally; recovery journal retained");
                await SetVerified(e.name, e.value);
            }
            File.Delete(path); journal = null;
        }
        private async Task SetVerified(string name, float value)
        {
            await write(name, value);
            if (await read(name) != value) throw new IOException("Camera RC write verification failed: " + name);
        }
        private string Id(string name)
        {
            var chars = new string[16]; for (int i = 0; i < 16; i++) chars[i] = i < name.Length ? name[i].ToString() : "\0";
            return "[" + string.Join(",", Array.ConvertAll(chars, x => x == "\0" ? "\"\\u0000\"" : "\"" + x + "\"")) + "]";
        }
        private string Packet(string body) => "{\"header\":{\"system_id\":246,\"component_id\":191,\"sequence\":0},\"message\":{" + body + "}}";
        private async Task<float> ReadParameter(string name)
        {
            // Read a counter baseline so a matching stale PARAM_VALUE cannot confirm a write.
            string endpoint = url + "/v1/mavlink/vehicles/1/components/1/messages/PARAM_VALUE";
            string baseline = await Get(endpoint);
            var before = JsonUtility.FromJson<RovMountProtocol.MessageEnvelope>(baseline)?.status?.time?.counter ?? -1;
            string query = Packet("\"type\":\"PARAM_REQUEST_READ\",\"target_system\":1,\"target_component\":1,\"param_index\":-1,\"param_id\":" + Id(name));
            await Post(query);
            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(80);
                string json = await Get(endpoint);
                var envelope = JsonUtility.FromJson<ParamEnvelope>(json);
                long counter = JsonUtility.FromJson<RovMountProtocol.MessageEnvelope>(json)?.status?.time?.counter ?? -1;
                if (counter != before && ParameterName(envelope?.message?.param_id) == name)
                    return envelope.message.param_value;
                if (i % 3 == 2) await Post(query); // The REST cache may have been overwritten by another parameter.
            }
            throw new IOException("No fresh parameter response for " + name);
        }
        private Task WriteParameter(string name, float value) => Post(Packet("\"type\":\"PARAM_SET\",\"target_system\":1,\"target_component\":1,\"param_id\":" + Id(name) + ",\"param_value\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"param_type\":{\"type\":\"MAV_PARAM_TYPE_INT32\"}"));
        private async Task<string> Get(string endpoint) { using (var r = UnityWebRequest.Get(endpoint)) { await Send(r); return r.downloadHandler.text; } }
        private async Task Post(string json)
        {
            using (var r = new UnityWebRequest(url + "/v1/mavlink", "POST"))
            { r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)); r.downloadHandler = new DownloadHandlerBuffer(); r.SetRequestHeader("Content-Type", "application/json"); await Send(r); }
        }
        private static async Task Send(UnityWebRequest request)
        {
            request.timeout = 2;
            var done = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest(); op.completed += _ => done.TrySetResult(true);
            if (op.isDone) done.TrySetResult(true);
            await done.Task;
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
        }
    }
}
