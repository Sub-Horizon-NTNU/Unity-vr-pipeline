using System;
using UnityEditor;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovHudChecks
    {
        public static int Run()
        {
            int checks = 0;
            var data = new RovHudTelemetry(); data.Reset();
            Expect(float.IsNaN(data.Voltage(0f)) && float.IsNaN(data.Remaining(0f)) && float.IsNaN(data.DepthEstimate(0f)), "Absent sensors must be unknown"); checks++;
            var fixture = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>("{\"messages\":{\"HEARTBEAT\":{\"message\":{\"base_mode\":{\"bits\":81}}},\"BATTERY_STATUS\":{\"message\":{\"id\":0,\"voltages\":[15476,65535,65535],\"battery_remaining\":-1},\"status\":{\"time\":{\"counter\":99}}},\"SYS_STATUS\":{\"message\":{\"voltage_battery\":15470,\"battery_remaining\":-1},\"status\":{\"time\":{\"counter\":99}}},\"AHRS2\":{\"message\":{\"altitude\":-6.74},\"status\":{\"time\":{\"counter\":99}}}}}").messages;
            data.Observe(fixture, 0f);
            Expect(float.IsNaN(data.Voltage(0f)), "Cached REST sensor is not live on first sample"); checks++;
            fixture.BATTERY_STATUS.status.time.counter++; fixture.SYS_STATUS.status.time.counter++; fixture.AHRS2.status.time.counter++;
            data.Observe(fixture, 0.1f);
            Near(data.Voltage(0.1f), 15.476f); checks++;
            Expect(float.IsNaN(data.Remaining(0.1f)), "MAVLink -1 percent must not become zero or guessed percent"); checks++;
            var missingPercent = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>("{\"messages\":{\"SYS_STATUS\":{\"message\":{\"voltage_battery\":15400},\"status\":{\"time\":{\"counter\":1}}}}}").messages;
            var missing = new RovHudTelemetry(); missing.Observe(missingPercent, 0f);
            missingPercent.SYS_STATUS.status.time.counter++;
            missing.Observe(missingPercent, 0.1f);
            Expect(float.IsNaN(missing.Remaining(0.1f)), "Omitted percentage must also remain unknown"); checks++;
            Near(data.DepthEstimate(0.1f), 6.74f); Expect(!data.Armed, "Bit 128 controls armed state"); checks++;
            fixture.HEARTBEAT.message.base_mode.bits = 209;
            data.Observe(fixture, 1f); Expect(data.Armed, "Armed bit must be shown"); checks++;
            data.Observe(fixture, 3.2f);
            Expect(float.IsNaN(data.Voltage(3.2f)) && float.IsNaN(data.DepthEstimate(3.2f)), "Heartbeat alone must not refresh sensors"); checks++;
            fixture.BATTERY_STATUS.message.voltages = new[] {65535};
            fixture.BATTERY_STATUS.status.time.counter++; fixture.SYS_STATUS.status.time.counter++;
            fixture.SYS_STATUS.message.battery_remaining = 42;
            data.Observe(fixture, 3.3f);
            Near(data.Voltage(3.3f), 15.470f); Near(data.Remaining(3.3f), 42f); checks++;
            Near(RovHudTelemetry.PackVoltage(new[] {4000, 4000, 4000, 4000, 65535}), 16f);
            Expect(float.IsNaN(RovHudTelemetry.PackVoltage(new[] {65535, 0})), "Invalid cells are not a voltage"); checks++;
            var oldProfile = RovVideoViewProfile.Parse("{\"distance\":1.53,\"verticalPositionMeters\":-0.328}");
            Expect(oldProfile.hudEnabled && oldProfile.hudAnchorMode == RovHudAnchorMode.VideoSurface, "Old profiles acquire stream-following HUD defaults");
            Near(oldProfile.distance, 1.53f); Near(oldProfile.verticalPositionMeters, -0.328f); checks++;
            oldProfile.hudEnabled = false;
            oldProfile.hudShowBattery = false; oldProfile.hudShowDepth = false; oldProfile.hudShowAttitude = false;
            oldProfile.hudShowDiagnostics = true; oldProfile.hudScale = 1.2f; oldProfile.hudOpacity = 0.6f;
            oldProfile.hudShowCompass = false; oldProfile.compassRadius = 2.1f; oldProfile.compassDrop = 1.1f;
            oldProfile.hudAnchorMode = RovHudAnchorMode.Headset;
            var loaded = RovVideoViewProfile.Parse(JsonUtility.ToJson(oldProfile));
            Expect(!loaded.hudEnabled && !loaded.hudShowBattery && !loaded.hudShowDepth && !loaded.hudShowAttitude && loaded.hudShowDiagnostics && !loaded.hudShowCompass
                && loaded.hudAnchorMode == RovHudAnchorMode.Headset, "All HUD toggles and anchoring must round trip");
            Near(loaded.hudScale, 1.2f); Near(loaded.hudOpacity, 0.6f); checks++;
            var migrated = RovVideoViewProfile.Parse("{\"comfortVignetteEnabled\":true,\"comfortReticleEnabled\":true,\"poseSmoothingEnabled\":true,\"telemetrySmoothingSeconds\":0.2,\"distance\":2.23}");
            Near(migrated.distance, 2.23f);
            Expect(!JsonUtility.ToJson(migrated).Contains("comfort") && !JsonUtility.ToJson(migrated).Contains("hudDistance")
                && !JsonUtility.ToJson(migrated).Contains("poseSmoothingEnabled"), "Removed comfort effects must not return on save"); checks++;
            Near(oldProfile.telemetrySmoothingSeconds, 0.06f);
            Near(oldProfile.servoSmoothingSeconds, 0.12f); Near(oldProfile.servoMaximumSpeed, 45f); checks++;
            oldProfile.telemetrySmoothingSeconds = 0f; oldProfile.servoSmoothingSeconds = 0f;
            oldProfile.servoMaximumSpeed = 32f;
            oldProfile.hudLayout.battery.position = new Vector3(-1.1f, -0.35f, 1.8f);
            oldProfile.hudLayout.battery.angleOffset = new Vector3(4f, 8f, -2f);
            oldProfile.hudLayout.battery.scale = 1.4f; oldProfile.hudLayout.battery.faceViewer = false;
            var floating = RovVideoViewProfile.Parse(JsonUtility.ToJson(oldProfile));
            Near(floating.telemetrySmoothingSeconds, 0f); Near(floating.servoSmoothingSeconds, 0f);
            Near(floating.servoMaximumSpeed, 32f);
            Near(Vector3.Distance(floating.hudLayout.battery.position, oldProfile.hudLayout.battery.position), 0f);
            Near(Vector3.Distance(floating.hudLayout.battery.angleOffset, oldProfile.hudLayout.battery.angleOffset), 0f);
            Near(floating.hudLayout.battery.scale, 1.4f);
            Expect(!floating.hudLayout.battery.faceViewer, "Box orientation mode must round trip"); checks++;
            var copy = floating.hudLayout.Copy(); copy.battery.position.x = 0.5f;
            Near(floating.hudLayout.battery.position.x, -1.1f); checks++;
            var aimed = new RovHudBoxLayout(-0.8f, 0.18f, 2f);
            Near(Vector3.Angle(aimed.Rotation * Vector3.forward, aimed.position), 0f); checks++;
            var clipped = RovVideoViewProfile.Parse("{\"telemetrySmoothingSeconds\":-2,\"servoSmoothingSeconds\":9,\"servoMaximumSpeed\":500,\"hudLayout\":{\"battery\":{\"position\":{\"x\":8,\"y\":-8,\"z\":0},\"scale\":10}}}");
            Near(clipped.telemetrySmoothingSeconds, 0f); Near(clipped.servoSmoothingSeconds, 0.5f); Near(clipped.servoMaximumSpeed, 90f);
            Near(clipped.hudLayout.battery.position.z, 0.6f); Near(clipped.hudLayout.battery.scale, 2f); checks++;
            foreach (float curve in new[] {0f, 45f, 115.8f, 150f})
            foreach (float x in new[] {-800f, 0f, 800f})
            {
                Vector3 video = RovVideoProjection.AdjustablePoint(x / 1920f + 0.5f, 0.75f, 110f, 16f / 9f, 2f, curve);
                Vector3 hud = RovHudSurfaceGraphic.SurfacePoint(x, 270f, 110f, curve, true);
                Near(Vector3.Distance(video, hud), 0.003f);
            }
            checks++;
            var sphereVideo = RovVideoProjection.Point(0.75f, 0.75f, 80f, 16f / 9f, 2f);
            Near(Vector3.Distance(sphereVideo, RovHudSurfaceGraphic.SurfacePoint(480f, 270f, 80f, 45f, false)), 0.003f); checks++;
            foreach (var hud in UnityEngine.Object.FindObjectsByType<RovVrOverlay>(FindObjectsSortMode.None))
                if (hud.Built)
                {
                    Expect(hud.FloatingAnchored && !hud.AttachedToStream, "Floating HUD must have its own world reference");
                    Near(Quaternion.Angle(hud.LayoutRoot.rotation, hud.view.PilotHudRotation), 0f);
                    Expect(hud.LayoutRoot.GetComponentsInChildren<RovHudSurfaceGraphic>(true).Length == 0, "Floating boxes must not warp with the stream");
                }
            checks++;
            Near(loaded.compassRadius, 2.1f); Near(loaded.compassDrop, 1.1f);
            Near(Vector3.Distance(RovVrCompass.BearingDirection(90f, 90f), Vector3.forward), 0f);
            Near(Vector3.Distance(RovVrCompass.BearingDirection(0f, 90f), Vector3.left), 0f);
            Near(Vector3.Distance(RovVrCompass.BearingDirection(0f, 359f), RovVrCompass.BearingDirection(0f, -1f)), 0f); checks++;
            Expect(RovVrOverlay.Compass(-1f) == "N" && RovVrOverlay.Compass(90f) == "Ø" && RovVrOverlay.Compass(270f) == "V", "Compass wrap and cardinal directions"); checks++;
            var shader = Resources.Load<Shader>("RovHudOverlay");
            Expect(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Stereo overlay shader must compile and be included in builds"); checks++;
            return checks;
        }

        private static void Near(float actual, float expected) => Expect(RovCameraPoseMath.Finite(actual) && Mathf.Abs(actual - expected) < 0.005f, $"Expected {expected}, received {actual}");
        private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException("ROV HUD: " + message); }
    }
}
