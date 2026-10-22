using System;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovCameraPoseChecks
    {
        public static int Run()
        {
            int checks = 0;
            Near(Vector3.Distance(RovCameraPoseMath.VehicleRotation(0f, 0f, 0f) * Vector3.forward, Vector3.forward), 0f); checks++;
            Near(Vector3.Distance(RovCameraPoseMath.VehicleRotation(0f, 0f, Mathf.PI / 2f) * Vector3.forward, Vector3.right), 0f); checks++;
            Near(RovHeadTiltMath.Pitch(RovCameraPoseMath.VehicleRotation(0f, 20f * Mathf.Deg2Rad, 0f)), 20f); checks++;
            var rolledRight = RovCameraPoseMath.VehicleRotation(20f * Mathf.Deg2Rad, 0f, 0f) * Vector3.right;
            Near(rolledRight.y, -Mathf.Sin(20f * Mathf.Deg2Rad)); checks++;
            var vehicle = RovCameraPoseMath.VehicleRotation(0f, 10f * Mathf.Deg2Rad, 0f);
            Near(RovHeadTiltMath.Pitch(RovCameraPoseMath.CameraRotation(vehicle, 20f, -35f, 30f)), 30f); checks++;
            Near(Quaternion.Angle(RovCameraPoseMath.CameraRotation(vehicle, 80f, -35f, 30f),
                RovCameraPoseMath.CameraRotation(vehicle, 30f, -35f, 30f)), 0f); checks++;
            Near(Quaternion.Angle(RovCameraPoseMath.CameraRotation(vehicle, -80f, -35f, 30f),
                RovCameraPoseMath.CameraRotation(vehicle, -35f, -35f, 30f)), 0f); checks++;
            var refVehicle = RovCameraPoseMath.VehicleRotation(0.02f, -0.03f, 3.1f);
            var headReference = Quaternion.Euler(-5f, 25f, 0f);
            var alignment = headReference * Quaternion.Inverse(refVehicle);
            Near(Quaternion.Angle(alignment * RovCameraPoseMath.CameraRotation(refVehicle, 0f, -35f, 30f), headReference), 0f); checks++;
            var wrappedYaw = RovCameraPoseMath.VehicleRotation(0.02f, -0.03f, -3.1f);
            Near(Quaternion.Angle(refVehicle, wrappedYaw), (2f * Mathf.PI - 6.2f) * Mathf.Rad2Deg); checks++;

            // The screen must not drift sideways with ROV yaw, even while
            // pitch/roll and the mount contribute to its displayed pose.
            foreach (float yaw in new[] {-180f, -179f, -90f, 0f, 90f, 179f, 180f})
            foreach (float pitch in new[] {-95f, -25f, 0f, 30f, 95f})
            foreach (float roll in new[] {-25f, 0f, 35f})
            {
                var displayed = RovCameraPoseMath.CameraRotation(
                    RovCameraPoseMath.VehicleRotationWithoutYaw(new Vector3(roll, pitch, yaw)), -12f, -35f, 30f);
                var expected = headReference * RovCameraPoseMath.CameraRotation(
                    RovCameraPoseMath.VehicleRotation(roll * Mathf.Deg2Rad, pitch * Mathf.Deg2Rad, 0f), -12f, -35f, 30f);
                Near(Quaternion.Angle(headReference * displayed, expected), 0f);
            }
            checks++;
            var viewLevel = RovCameraPoseMath.VehicleRotationWithoutYaw(new Vector3(0f, 0f, 90f));
            Near(Quaternion.Angle(viewLevel, Quaternion.identity), 0f);
            Near(RovHeadTiltMath.Pitch(RovCameraPoseMath.VehicleRotationWithoutYaw(new Vector3(0f, 20f, 90f))), 20f);
            Near((RovCameraPoseMath.VehicleRotationWithoutYaw(new Vector3(20f, 0f, 90f)) * Vector3.right).y,
                -Mathf.Sin(20f * Mathf.Deg2Rad));
            checks++;

            Expect(!RovCameraPoseMath.TryMountPitch(new float[4], out _), "Zero quaternion is not a measurement"); checks++;
            Expect(!RovCameraPoseMath.TryMountPitch(new[] {float.NaN, 0f, 0f, 0f}, out _), "NaN mount input"); checks++;
            Expect(RovCameraPoseMath.TryMountPitch(new[] {0.99904823f, 0f, 0.04361939f, 0f}, out float mount), "Valid mount input");
            Near(mount, 5f); checks++;
            var fresh = new RovHeartbeatFreshness(0.75f);
            fresh.Observe(100, 0f);
            Expect(!fresh.IsFresh(0f), "A cached IMU sample must not move the screen"); checks++;
            fresh.Observe(101, 0.1f);
            Expect(fresh.IsFresh(0.5f), "Advancing IMU counter must be usable"); checks++;
            fresh.Observe(101, 1f);
            Expect(!fresh.IsFresh(1f), "Repeated IMU sample must expire independently of heartbeat"); checks++;

            var projection = Matrix4x4.Perspective(90f, 1f, 0.05f, 50f);
            float fov = RovCameraPoseMath.FitCylinderFov(projection, 1920f / 1080f);
            Near(RovVideoProjection.CylinderVerticalFov(fov, 1920f / 1080f), 96f); checks++;
            projection.m12 = 0.15f;
            Expect(RovCameraPoseMath.FitCylinderFov(projection, 1920f / 1080f) > fov,
                "Asymmetric eye projection needs more coverage"); checks++;
            var large = RovVideoProjection.Create(1920, 1080, fov, 2f, 96, 54, false, true, true);
            try
            {
                foreach (var v in large.vertices)
                    Expect(RovCameraPoseMath.Finite(v.x) && RovCameraPoseMath.Finite(v.y) && RovCameraPoseMath.Finite(v.z), "Invalid fit geometry");
                checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(large); }
            const string json = "{\"messages\":{\"ATTITUDE\":{\"message\":{\"roll\":0.1,\"pitch\":-0.2,\"yaw\":0.3},\"status\":{\"time\":{\"counter\":99}}}}}";
            var data = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>(json).messages.ATTITUDE;
            Near(data.message.pitch, -0.2f); Expect(data.status.time.counter == 99, "IMU counter deserialization"); checks++;
            return checks;
        }

        private static void Near(float value, float expected)
        {
            Expect(RovCameraPoseMath.Finite(value) && Mathf.Abs(value - expected) < 0.05f, $"Expected {expected}, received {value}");
        }
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROV camera pose check: " + message);
        }
    }
}
