using System;
using UnityEngine;

namespace ROVVR.Editor
{
    // Runs inside Unity: tests its quaternion math and JSON serializer, without
    // requiring a connected headset or moving the ROV during a compile/import.
    public static class RovHeadTiltChecks
    {
        public static int Run()
        {
            int checks = 0;
            Expect(RovHeadTiltFocus.AllowsControl(false, false, true, true, true), "Quest focus must work without desktop focus"); checks++;
            Expect(!RovHeadTiltFocus.AllowsControl(true, false, true, false, true), "Desktop focus must not override lost VR focus"); checks++;
            Expect(!RovHeadTiltFocus.AllowsControl(true, false, true, true, false), "Quest system menu must stop camera control"); checks++;
            Expect(RovHeadTiltFocus.AllowsControl(true, false, false, false, false), "Desktop-only focus fallback"); checks++;
            Expect(!RovHeadTiltFocus.AllowsControl(true, true, false, false, false), "Paused desktop app must not control camera"); checks++;
            Expect(RovHeadTiltFocus.AllowsControl(false, true, true, true, true), "An active Quest session owns focus independently of desktop pause"); checks++;
            ExpectNear(RovHeadTiltMath.Pitch(Quaternion.Euler(-20f, 0f, 0f)), 20f); checks++;
            ExpectNear(RovHeadTiltMath.Pitch(Quaternion.Euler(20f, 0f, 0f)), -20f); checks++;
            ExpectNear(RovHeadTiltMath.Pitch(Quaternion.Euler(-20f, 145f, 35f)), 20f); checks++;
            ExpectNear(RovHeadTiltMath.Target(25f, 5f, 1f, false, 0.5f, -35f, 30f), 20f); checks++;
            ExpectNear(RovHeadTiltMath.Target(80f, 0f, 1f, false, 0.5f, -35f, 30f), 30f); checks++;
            ExpectNear(RovHeadTiltMath.Target(-80f, 0f, 1f, false, 0.5f, -35f, 30f), -35f); checks++;
            ExpectNear(RovHeadTiltMath.Target(0.3f, 0f, 1f, false, 0.5f, -35f, 30f), 0f); checks++;
            ExpectNear(RovHeadTiltMath.Target(20f, 0f, 1f, true, 0.5f, -35f, 30f), -20f); checks++;
            ExpectNear(RovHeadTiltMath.Smooth(0f, 30f, 0.12f, 45f, 0.05f), 2.25f); checks++;
            ExpectNear(RovHeadTiltMath.Smooth(0f, 1f, 0f, 45f, 0.05f), 1f); checks++;
            ExpectNear(RovHeadTiltMath.Smooth(0f, 30f, 0f, 45f, 0.05f), 2.25f); checks++;
            ExpectNear(RovHeadTiltMath.MountPitch(new[] { 0.99904823f, 0f, 0.04361939f, 0f }), 5f); checks++;

            // The mount is body relative; the requested elevation is world relative.
            var noseUp = RovCameraPoseMath.VehicleRotation(0f, 15f * Mathf.Deg2Rad, 0f);
            var noseDown = RovCameraPoseMath.VehicleRotation(0f, -20f * Mathf.Deg2Rad, 0f);
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(0f, noseUp, -35f, 30f), -15f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(0f, noseDown, -35f, 30f), 20f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(10f, noseUp, -35f, 30f), -5f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(-10f, noseDown, -35f, 30f), 10f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(7f, Quaternion.identity, -35f, 30f), 7f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(0f,
                RovCameraPoseMath.VehicleRotation(0f, 50f * Mathf.Deg2Rad, 0f), -35f, 30f), -35f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(0f,
                RovCameraPoseMath.VehicleRotation(0f, -60f * Mathf.Deg2Rad, 0f), -35f, 30f), 30f); checks++;
            foreach (float roll in new[] {-20f, 0f, 20f})
                foreach (float pitch in new[] {-10f, 0f, 10f})
                    foreach (float desired in new[] {-10f, 0f, 10f})
                    {
                        var vehicle = RovCameraPoseMath.VehicleRotation(roll * Mathf.Deg2Rad, pitch * Mathf.Deg2Rad, 137f * Mathf.Deg2Rad);
                        float mount = RovCameraPoseMath.StabilizedMountPitch(desired, vehicle, -35f, 30f);
                        ExpectNear(RovHeadTiltMath.Pitch(RovCameraPoseMath.CameraRotation(vehicle, mount, -35f, 30f)), desired);
                    }
            checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(0f,
                RovCameraPoseMath.VehicleRotation(Mathf.PI, 0f, 0f), -35f, 30f), 0f); checks++;
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(20f,
                RovCameraPoseMath.VehicleRotation(Mathf.PI / 2f, 0f, 0f), -35f, 30f), 0f); checks++;
            float headInDeadzone = RovHeadTiltMath.DesiredPitch(0.2f, 0f, 1f, false, 0.5f);
            ExpectNear(RovCameraPoseMath.StabilizedMountPitch(headInDeadzone,
                RovCameraPoseMath.VehicleRotation(0f, 2f * Mathf.Deg2Rad, 0f), -35f, 30f), -2f); checks++;
            ExpectNear(RovHeadTiltMath.DesiredPitch(60f, 0f, 2f, false, 0f), 89f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(0f, noseUp, 0.9f, -35f, 30f), -13.5f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(0f, noseDown, 0.9f, -35f, 30f), 18f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(10f, noseUp, 0f, -35f, 30f), 10f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(7f, Quaternion.identity, 0.85f, -35f, 30f), 7f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(0f, noseUp, 1f, -35f, 30f), -15f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(0f,
                RovCameraPoseMath.VehicleRotation(0f, 60f * Mathf.Deg2Rad, 0f), 0.5f, -35f, 30f), -30f); checks++;
            ExpectNear(RovCameraPoseMath.TrimmedMountPitch(0f,
                RovCameraPoseMath.VehicleRotation(0f, 50f * Mathf.Deg2Rad, 0f), 1.5f, -35f, 30f), -35f); checks++;

            var packet = JsonUtility.FromJson<RovMountProtocol.CommandPacket>(RovMountProtocol.CommandJson(100f));
            Expect(packet.message.command.type == "MAV_CMD_DO_MOUNT_CONTROL", "Wrong mount command"); checks++;
            Expect(packet.header.system_id == 245 && packet.header.component_id == 191, "Wrong sender IDs"); checks++;
            Expect(packet.message.target_system == 1 && packet.message.target_component == 1, "Wrong target IDs"); checks++;
            ExpectNear(packet.message.param1, 30f); checks++;
            ExpectNear(packet.message.param7, 2f); checks++;
            ExpectNear(packet.message.param3, 0f); checks++; // Compensation never commands vehicle/gimbal yaw.
            packet = JsonUtility.FromJson<RovMountProtocol.CommandPacket>(RovMountProtocol.CommandJson(0f, true));
            ExpectNear(packet.message.param7, 3f); checks++;
            bool rejectedNaN = false;
            try { RovMountProtocol.CommandJson(float.NaN); }
            catch (ArgumentOutOfRangeException) { rejectedNaN = true; }
            Expect(rejectedNaN, "NaN must never be sent to the mount"); checks++;

            var freshness = new RovHeartbeatFreshness();
            freshness.Observe(100, 0f);
            Expect(!freshness.IsFresh(0f), "First cached sample must not enable movement"); checks++;
            freshness.Observe(101, 1f);
            Expect(freshness.IsFresh(1f), "Advancing heartbeat must enable communication"); checks++;
            freshness.Observe(101, 5f);
            Expect(!freshness.IsFresh(5f), "Repeated cached heartbeat must expire"); checks++;
            freshness.Reset();
            freshness.Observe(101, 6f);
            Expect(!freshness.IsFresh(6f), "Reconnect requires fresh evidence"); checks++;
            freshness.Observe(2, 7f);
            Expect(freshness.IsFresh(7f), "A restarted autopilot must be detected"); checks++;

            // Real BlueOS telemetry can have duplicate 'type' tags in MAVLink
            // messages. Check that the fields used for freshness remain readable.
            const string telemetry = "{\"messages\":{\"HEARTBEAT\":{\"message\":{\"type\":\"common\",\"type\":\"HEARTBEAT\",\"mavtype\":{\"type\":\"MAV_TYPE_SUBMARINE\"}},\"status\":{\"time\":{\"counter\":123}}}}}";
            var received = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>(telemetry);
            Expect(received.messages.HEARTBEAT.status.time.counter == 123, "Cannot read heartbeat counter"); checks++;
            Expect(received.messages.HEARTBEAT.message.mavtype.type == "MAV_TYPE_SUBMARINE", "Cannot identify ROV"); checks++;
            return checks;
        }

        private static void ExpectNear(float actual, float expected)
        {
            Expect(!float.IsNaN(actual) && Mathf.Abs(actual - expected) < 0.01f, $"Expected {expected}, received {actual}");
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROV head tilt check: " + message);
        }
    }
}
