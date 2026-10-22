using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ROVVR
{
    public static class RovMountProtocol
    {
        public const string MountCommand = "MAV_CMD_DO_MOUNT_CONTROL";
        public const int SourceSystem = 245;
        public const int SourceComponent = 191;
        public const float MinimumPitch = -35f;
        public const float MaximumPitch = 30f;

        // This BlueOS MAVLink2REST build accepts command 205. Its JSON decoder
        // rejects DO_GIMBAL_MANAGER_PITCHYAW. Use the verified compatible command.
        public static string CommandJson(float pitch, bool release = false)
        {
            if (float.IsNaN(pitch) || float.IsInfinity(pitch))
                throw new ArgumentOutOfRangeException(nameof(pitch));

            var packet = new CommandPacket();
            packet.message.param1 = release ? 0f : Mathf.Clamp(pitch, MinimumPitch, MaximumPitch);
            packet.message.param7 = release ? 3f : 2f; // RC Targeting / MAVLink Targeting.
            return JsonUtility.ToJson(packet);
        }

        public static UnityWebRequest Post(string baseUrl, float pitch, bool release = false)
        {
            var request = new UnityWebRequest(baseUrl.TrimEnd('/') + "/v1/mavlink", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(CommandJson(pitch, release)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 2;
            return request;
        }

        public static string TelemetryUrl(string baseUrl)
        {
            return baseUrl.TrimEnd('/') + "/v1/mavlink/vehicles/1/components/1";
        }

        [Serializable] public class EnumValue { public string type; }
        [Serializable] public class ModeFlags { public int bits; }
        [Serializable] public class Header
        {
            public int system_id = SourceSystem;
            public int component_id = SourceComponent;
            public int sequence;
        }
        [Serializable] public class CommandPacket
        {
            public Header header = new Header();
            public MountMessage message = new MountMessage();
        }
        [Serializable] public class MountMessage
        {
            public string type = "COMMAND_LONG";
            public EnumValue command = new EnumValue { type = MountCommand };
            public int target_system = 1;
            public int target_component = 1;
            public int confirmation;
            public float param1, param2, param3, param4, param5, param6, param7;
        }

        [Serializable] public class VehicleTelemetry { public Messages messages; }
        [Serializable] public class Messages
        {
            public MessageEnvelope HEARTBEAT;
            public MessageEnvelope COMMAND_ACK;
            public MessageEnvelope SERVO_OUTPUT_RAW;
            public MessageEnvelope GIMBAL_DEVICE_ATTITUDE_STATUS;
            public MessageEnvelope ATTITUDE;
            public MessageEnvelope SYS_STATUS;
            public MessageEnvelope BATTERY_STATUS;
            public MessageEnvelope AHRS2;
        }
        [Serializable] public class MessageEnvelope
        {
            public TelemetryMessage message;
            public MessageStatus status;
        }
        [Serializable] public class MessageStatus { public MessageTime time; }
        [Serializable] public class MessageTime { public long counter; }
        [Serializable] public class TelemetryMessage
        {
            public EnumValue mavtype, command, result;
            public int target_system, target_component, servo11_raw;
            public float[] q;
            public float roll, pitch, yaw;
            public ModeFlags base_mode;
            public int custom_mode, id;
            public int voltage_battery = 65535, current_battery = -1, battery_remaining = -1;
            public int[] voltages;
            public float altitude;
        }
    }

    public static class RovHeadTiltFocus
    {
        public static bool DesktopShortcutsAllowed
        {
            get
            {
                if (!Application.isFocused) return false;
#if UNITY_EDITOR
                // R/P typed into an Editor setting must not recenter/pause the
                // live controller while Quest independently retains VR focus.
                var window = UnityEditor.EditorWindow.focusedWindow;
                return window != null && window.GetType().Name == "GameView"
                    && !UnityEditor.EditorGUIUtility.editingTextField;
#else
                return true;
#endif
            }
        }

        // A Quest Link session can own VR input while a different desktop window
        // is foreground. Conversely, a foreground Unity window must not override
        // a Quest system menu which has taken VR focus.
        public static bool AllowsControl(bool desktopFocused, bool applicationPaused,
            bool xrActive, bool vrFocused, bool vrInputFocused)
        {
            return xrActive ? vrFocused && vrInputFocused : desktopFocused && !applicationPaused;
        }
    }

    public static class RovHeadTiltMath
    {
        // Elevation of the viewing direction: positive means looking up.
        // Unlike localEulerAngles.x, yaw and roll do not change this reading.
        public static float Pitch(Quaternion orientation)
        {
            Vector3 forward = orientation * Vector3.forward;
            return Mathf.Atan2(forward.y, new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;
        }

        public static float Target(float pitch, float neutral, float gain, bool invert,
            float deadZone, float minimum, float maximum)
        {
            return Mathf.Clamp(DesiredPitch(pitch, neutral, gain, invert, deadZone), minimum, maximum);
        }

        public static float DesiredPitch(float pitch, float neutral, float gain, bool invert, float deadZone)
        {
            float delta = pitch - neutral;
            if (Mathf.Abs(delta) <= deadZone) delta = 0f;
            return Mathf.Clamp(delta * gain * (invert ? -1f : 1f), -89f, 89f);
        }

        public static float Smooth(float current, float target, float seconds, float rate, float dt)
        {
            float filtered = seconds <= 0f ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-dt / seconds));
            return Mathf.MoveTowards(current, filtered, Mathf.Max(0f, rate) * dt);
        }

        public static float MountPitch(float[] q)
        {
            if (q == null || q.Length != 4) return 0f;
            // MAVLink quaternion is w,x,y,z in the vehicle coordinate convention.
            return Mathf.Asin(Mathf.Clamp(2f * (q[0] * q[2] - q[3] * q[1]), -1f, 1f)) * Mathf.Rad2Deg;
        }
    }

    // Two different heartbeat counters are required before treating cached REST
    // data as live. The ROV's wall clock is deliberately not used for freshness.
    public sealed class RovHeartbeatFreshness
    {
        private readonly float maximumAge;
        private long previous = -1;
        private float lastAdvance = float.NegativeInfinity;

        public RovHeartbeatFreshness(float maximumAge = 3f) { this.maximumAge = maximumAge; }

        public void Observe(long counter, float now)
        {
            if (previous >= 0 && previous != counter) lastAdvance = now;
            previous = counter;
        }

        public bool IsFresh(float now) { return now - lastAdvance < maximumAge; }
        public void Reset() { previous = -1; lastAdvance = float.NegativeInfinity; }
    }
}
