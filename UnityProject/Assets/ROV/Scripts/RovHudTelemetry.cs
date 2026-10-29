using System;
using Newtonsoft.Json;
using UnityEngine;

namespace ROVVR
{
    // Reuse the controller's existing GET. Each slow sensor expires separately;
    // an advancing heartbeat cannot make an old battery reading live again.
    public sealed class RovHudTelemetry
    {
        private readonly RovHeartbeatFreshness batteryAge = new RovHeartbeatFreshness(3f);
        private readonly RovHeartbeatFreshness systemAge = new RovHeartbeatFreshness(3f);
        private readonly RovHeartbeatFreshness depthAge = new RovHeartbeatFreshness(3f);
        private RovMountProtocol.TelemetryMessage battery, system;
        private float depth;
        private readonly NamedLevel pilotGain = new NamedLevel();
        private readonly NamedLevel light1 = new NamedLevel();
        private readonly NamedLevel light2 = new NamedLevel();
        public bool Armed { get; private set; }

        public void Reset()
        {
            batteryAge.Reset(); systemAge.Reset(); depthAge.Reset();
            battery = system = null; depth = float.NaN; Armed = false;
            ResetPilotValues();
        }

        // ArduSub publishes its current joystick gain and normalized light output
        // commands (not measured brightness) as separate NAMED_VALUE_FLOAT frames.
        // Read these from the stream: REST caches only the last named value.
        public float PilotGain(float now) => pilotGain.Read(now);
        public float Light1Level(float now) => light1.Read(now);
        public float Light2Level(float now) => light2.Read(now);

        public void ResetPilotValues()
        {
            pilotGain.Reset(); light1.Reset(); light2.Reset();
        }

        public bool ObservePilotStream(string json, float receivedAt)
        {
            if (!RovCameraPoseMath.Finite(receivedAt)) return false;
            PilotFrame frame;
            // JsonUtility truncates a string at U+0000, losing both legitimate
            // MAVLink padding and evidence of malformed embedded terminators.
            // The project's existing Newtonsoft package preserves these chars.
            try { frame = JsonConvert.DeserializeObject<PilotFrame>(json); }
            catch (JsonException) { return false; }
            if (frame?.header == null || frame.header.system_id != 1 || frame.header.component_id != 1
                || frame.message?.type != "NAMED_VALUE_FLOAT") return false;
            string name = DecodeName(frame.message.name);
            NamedLevel target = name == "PilotGain" ? pilotGain : name == "Lights1" ? light1 : name == "Lights2" ? light2 : null;
            if (target == null) return false;
            return target.Observe(frame.message.value, receivedAt);
        }

        private static string DecodeName(string[] characters)
        {
            if (characters == null || characters.Length > 10) return null;
            var name = new char[characters.Length];
            int length = 0;
            bool ended = false;
            foreach (string character in characters)
            {
                if (character == null || character.Length != 1) return null;
                if (character[0] == '\0') ended = true;
                else if (ended) return null;
                else name[length++] = character[0];
            }
            return new string(name, 0, length);
        }

        [Serializable] private sealed class PilotFrame { public PilotHeader header = null; public PilotMessage message = null; }
        [Serializable] private sealed class PilotHeader { public int system_id = 0, component_id = 0; }
        [Serializable] private sealed class PilotMessage
        {
            public string type = null;
            public string[] name = null;
            public float value = float.NaN;
        }

        private sealed class NamedLevel
        {
            private float value = float.NaN, receivedAt = float.NegativeInfinity;
            public bool Observe(float next, float now)
            {
                // Preserve invalid and absent values as unknown, including when
                // they replace a previously valid reading. Zero remains valid.
                value = RovCameraPoseMath.Finite(next) && next >= 0f && next <= 1f ? next : float.NaN;
                receivedAt = now;
                return RovCameraPoseMath.Finite(value);
            }
            public float Read(float now) => RovCameraPoseMath.Finite(now) && now >= receivedAt && now - receivedAt < 3f ? value : float.NaN;
            public void Reset() { value = float.NaN; receivedAt = float.NegativeInfinity; }
        }

        public void Observe(RovMountProtocol.Messages messages, float now)
        {
            if (messages == null) return;
            if (messages.HEARTBEAT?.message?.base_mode != null)
                Armed = (messages.HEARTBEAT.message.base_mode.bits & 128) != 0;
            var b = messages.BATTERY_STATUS;
            if (b?.message != null && b.status?.time != null && b.message.id == 0)
            { battery = b.message; batteryAge.Observe(b.status.time.counter, now); }
            var s = messages.SYS_STATUS;
            if (s?.message != null && s.status?.time != null)
            { system = s.message; systemAge.Observe(s.status.time.counter, now); }
            var d = messages.AHRS2;
            if (d?.message != null && d.status?.time != null && RovCameraPoseMath.Finite(d.message.altitude))
            { depth = -d.message.altitude; depthAge.Observe(d.status.time.counter, now); }
        }

        public float Voltage(float now)
        {
            float pack = batteryAge.IsFresh(now) ? PackVoltage(battery?.voltages) : float.NaN;
            if (RovCameraPoseMath.Finite(pack)) return pack;
            return systemAge.IsFresh(now) && system != null && system.voltage_battery >= 0 && system.voltage_battery < 65535
                ? system.voltage_battery / 1000f : float.NaN;
        }

        public float Remaining(float now)
        {
            if (batteryAge.IsFresh(now) && battery != null && battery.battery_remaining >= 0 && battery.battery_remaining <= 100)
                return battery.battery_remaining;
            return systemAge.IsFresh(now) && system != null && system.battery_remaining >= 0 && system.battery_remaining <= 100
                ? system.battery_remaining : float.NaN;
        }

        public float DepthEstimate(float now) => depthAge.IsFresh(now) ? depth : float.NaN;

        public static float PackVoltage(int[] cells)
        {
            if (cells == null) return float.NaN;
            float millivolts = 0f;
            foreach (int cell in cells) if (cell > 0 && cell < 65535) millivolts += cell;
            return millivolts > 0f ? millivolts / 1000f : float.NaN;
        }
    }
}
