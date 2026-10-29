using System;
using System.Reflection;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovCommandRecoveryChecks
    {
        public static int Run()
        {
            int checks = 0;
            var go = new GameObject("ROV command recovery (no networking)");
            go.SetActive(false);
            try
            {
                var tilt = go.AddComponent<RovHeadTiltController>();
                Set(tilt, "calibrated", true); Set(tilt, "neutralPitch", 12f);
                Set(tilt, "canSend", true); Set(tilt, "firstCommandAt", 10f);
                Invoke(tilt, "CheckAcknowledgementTimeout", 13f);
                Expect(Get<bool>(tilt, "canSend") && Get<float>(tilt, "firstCommandAt") == 10f, "An in-flight ACK must be given time to arrive"); checks++;

                Invoke(tilt, "CheckAcknowledgementTimeout", 14.1f);
                Expect(!Get<bool>(tilt, "canSend") && !Get<bool>(tilt, "commandRejected")
                    && Get<float>(tilt, "firstCommandAt") < 0f, "Missing ACK must release the attempt without latching an explicit refusal");
                Preserved(tilt); checks++;
                Expect(!(bool)Invoke(tilt, "AcknowledgementRetryReady", 14.5f)
                    && (bool)Invoke(tilt, "AcknowledgementRetryReady", 15.2f), "Missing ACK must become retryable after the delay without P or recalibration");
                Preserved(tilt); checks++;

                foreach (float delay in new[] {2f, 4f, 8f, 8f})
                {
                    Set(tilt, "firstCommandAt", 20f);
                    Invoke(tilt, "CheckAcknowledgementTimeout", 25f);
                    Near(Get<float>(tilt, "ackRetryAt"), 25f + delay);
                }
                Preserved(tilt); checks++;

                Invoke(tilt, "ObserveCommandResult", "MAV_RESULT_ACCEPTED", 40f);
                Expect(tilt.CommandAccepted && Get<int>(tilt, "ackRetryCount") == 0, "Acceptance must reset backoff for later interruptions"); checks++;
                Set(tilt, "firstCommandAt", 38f); Set(tilt, "canSend", true);
                Invoke(tilt, "CheckAcknowledgementTimeout", 43.9f);
                Expect(tilt.CommandAccepted, "Recent ACK must remain valid"); checks++;
                Invoke(tilt, "CheckAcknowledgementTimeout", 44.1f);
                Expect(!tilt.CommandAccepted && !Get<bool>(tilt, "canSend"), "An old accepted ACK must expire during an active session"); checks++;

                Invoke(tilt, "ObserveCommandResult", "MAV_RESULT_ACCEPTED", 44.5f);
                Invoke(tilt, "ObserveCommandResult", "MAV_RESULT_TEMPORARILY_REJECTED", 45f);
                Expect(!tilt.CommandAccepted && !Get<bool>(tilt, "commandRejected")
                    && !(bool)Invoke(tilt, "AcknowledgementRetryReady", 45.5f)
                    && (bool)Invoke(tilt, "AcknowledgementRetryReady", 46.1f), "Temporary refusal must retry with bounded backoff");
                Preserved(tilt); checks++;

                foreach (string refusal in new[] {"MAV_RESULT_DENIED", "MAV_RESULT_UNSUPPORTED", "MAV_RESULT_FAILED"})
                {
                    Set(tilt, "commandRejected", false);
                    Invoke(tilt, "ObserveCommandResult", refusal, 50f);
                    tilt.CompensateRovPitch = !tilt.CompensateRovPitch;
                    Invoke(tilt, "AcknowledgementRetryReady", 100f);
                    Expect(Get<bool>(tilt, "commandRejected"), "Settings or retry expiration must never clear an explicit refusal");
                }
                checks++;
                Set(tilt, "canSend", true); Set(tilt, "commandRejected", false);
                Invoke(tilt, "SuspendForInterruption", "Test IMU gap");
                Expect(tilt.HasHeadNeutral && Get<float>(tilt, "neutralPitch") == 12f && !Get<bool>(tilt, "canSend")
                    && Get<bool>(tilt, "resumeTarget") && tilt.LastStopReason == "Test IMU gap", "Interruption must stop output while preserving neutral and recording its reason"); checks++;
                // A cached ACK must be consumed once, and unrelated ACKs must not prove tilt control.
                Set(tilt, "firstCommandAt", 0f); Set(tilt, "ackBaseline", 5L); Set(tilt, "canSend", true);
                string envelope = "{\"messages\":{\"HEARTBEAT\":{\"message\":{\"mavtype\":{\"type\":\"MAV_TYPE_SUBMARINE\"}},\"status\":{\"time\":{\"counter\":10}}},\"COMMAND_ACK\":{\"message\":{\"command\":{\"type\":\"MAV_CMD_DO_MOUNT_CONTROL\"},\"result\":{\"type\":\"MAV_RESULT_ACCEPTED\"},\"target_system\":245,\"target_component\":191},\"status\":{\"time\":{\"counter\":6}}}}}";
                Invoke(tilt, "ReadTelemetry", envelope);
                Expect(tilt.CommandAccepted, "Fresh matching ACK must be observed"); checks++;
                Set(tilt, "lastAcceptedAt", -50f); Invoke(tilt, "ReadTelemetry", envelope);
                Expect(Get<float>(tilt, "lastAcceptedAt") == -50f, "Cached ACK must not refresh watchdog"); checks++;
                Invoke(tilt, "ReadTelemetry", envelope.Replace("DO_MOUNT_CONTROL", "REQUEST_MESSAGE").Replace("\"counter\":6", "\"counter\":7"));
                Expect(Get<float>(tilt, "lastAcceptedAt") == -50f, "Other commands must not refresh tilt ACK"); checks++;
                Expect(!RovHeadTiltFocus.AllowsControl(true, false, true, false, true)
                    && !RovHeadTiltFocus.AllowsControl(true, false, true, true, false), "ACK recovery must retain native VR-focus stop conditions"); checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            return checks + RovRcGateChecks.Run();
        }

        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
        private static void Near(float actual, float expected) => Expect(Mathf.Abs(actual - expected) < 0.001f, "Unexpected retry backoff");
        private static void Preserved(RovHeadTiltController tilt) => Expect(tilt.HasHeadNeutral
            && Get<float>(tilt, "neutralPitch") == 12f && tilt.CommandsSent == 0, "Retries must preserve neutral and issue no test commands");
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROV command recovery: " + message);
        }
    }
}
