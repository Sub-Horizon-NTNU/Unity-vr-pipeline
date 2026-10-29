using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace ROVVR.Editor
{
    public static class RovRcGateChecks
    {
        public static int Run()
        {
            int checks = 0;
            Expect(RovRcCameraGate.ParameterName(new[] {"R", "C", "8", "_OPTION", "\\u0000"}) == "RC8_OPTION"
                && RovRcCameraGate.ParameterName(new[] {"RC8_OPTION", "\0"}) == "RC8_OPTION", "Both MAVLink2REST null-padding encodings must be accepted"); checks++;
            string dir = Path.Combine(Path.GetTempPath(), "rov-rc-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir); string path = Path.Combine(dir, "journal.json");
            var values = new Dictionary<string, float>(); for (int i = 1; i <= 16; i++) values["RC" + i + "_OPTION"] = 0;
            values["RC7_OPTION"] = 214; values["RC8_OPTION"] = 213; values["RC6_OPTION"] = 36;
            var writes = new List<string>(); bool failPitch = false;
            Func<string, Task<float>> read = name => Task.FromResult(values[name]);
            Func<string, float, Task> write = (name, value) => {
                if (!File.Exists(path)) throw new Exception("Write before durable journal");
                if (failPitch && name == "RC8_OPTION" && value == 0) throw new IOException("Simulated failed write");
                writes.Add(name); values[name] = value; return Task.CompletedTask;
            };
            RovRcCameraGate gate = null;
            try
            {
                gate = new RovRcCameraGate("http://test", path, read, write);
                gate.AcquireAsync().GetAwaiter().GetResult();
                Expect(gate.Active && gate.PendingRestore && values["RC7_OPTION"] == 0 && values["RC8_OPTION"] == 0, "VR must suppress only mapped camera RC inputs"); checks++;
                Expect(values["RC6_OPTION"] == 36 && writes.Count == 2, "Vehicle/emergency RC options must stay untouched"); checks++;
                bool locked = false;
                try { new RovRcCameraGate("http://test", path, read, write).AcquireAsync().GetAwaiter().GetResult(); } catch (IOException) { locked = true; }
                Expect(locked, "Two local VR sessions must not acquire the same camera gate"); checks++;
                gate.RestoreAsync().GetAwaiter().GetResult();
                Expect(!gate.Active && !gate.PendingRestore && values["RC7_OPTION"] == 214 && values["RC8_OPTION"] == 213, "RC handoff must restore exact original values"); checks++;
                failPitch = true; bool failed = false;
                try { gate.AcquireAsync().GetAwaiter().GetResult(); } catch (IOException) { failed = true; }
                Expect(failed && !gate.Active && values["RC7_OPTION"] == 214 && values["RC8_OPTION"] == 213 && !File.Exists(path), "Partial acquisition must roll back and refuse VR"); checks++;
                failPitch = false;
                File.WriteAllText(path, "{\"url\":\"http://test\",\"entries\":[{\"name\":\"RC8_OPTION\",\"value\":213}]}"); values["RC8_OPTION"] = 0;
                gate.RestoreAsync().GetAwaiter().GetResult();
                Expect(values["RC8_OPTION"] == 213 && !File.Exists(path), "Interrupted-session journal must restore RC without head tracking"); checks++;
                gate.AcquireAsync().GetAwaiter().GetResult(); values["RC8_OPTION"] = 99;
                failed = false; try { gate.RestoreAsync().GetAwaiter().GetResult(); } catch (IOException) { failed = true; }
                Expect(failed && File.Exists(path) && values["RC8_OPTION"] == 99, "External edits must not be overwritten and journal must survive"); checks++;
                values["RC8_OPTION"] = 0; gate.RestoreAsync().GetAwaiter().GetResult();
                Expect(!RovRcCameraGate.AllowedEntry(new RovRcCameraGate.Entry {name="RC3_OPTION",value=36})
                    && !RovRcCameraGate.AllowedEntry(new RovRcCameraGate.Entry {name="SERVO1_FUNCTION",value=213}), "Recovery input must never write non-camera parameters"); checks++;
            }
            finally
            {
                if (gate != null && File.Exists(path)) { values["RC8_OPTION"] = 0; try { gate.RestoreAsync().GetAwaiter().GetResult(); } catch {} }
                foreach (string file in new[] {path, path + ".tmp", path + ".lock"}) if (File.Exists(file)) File.Delete(file);
                Directory.Delete(dir);
            }
            return checks;
        }
        private static void Expect(bool ok, string message) { if (!ok) throw new InvalidOperationException("RC camera gate: " + message); }
    }
}
