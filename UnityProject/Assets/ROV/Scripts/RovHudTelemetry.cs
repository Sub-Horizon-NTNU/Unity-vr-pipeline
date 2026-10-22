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
        public bool Armed { get; private set; }

        public void Reset()
        {
            batteryAge.Reset(); systemAge.Reset(); depthAge.Reset();
            battery = system = null; depth = float.NaN; Armed = false;
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
