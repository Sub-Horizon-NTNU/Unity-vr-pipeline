using UnityEngine;

namespace ROVVR
{
    public static class RovCameraPoseMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // MAVLink aerospace ZYX: X forward, Y right, Z down. Unity uses
        // Z forward, X right, Y up: nose up is -X and right wing down is -Z.
        public static Quaternion VehicleRotation(float rollRadians, float pitchRadians, float yawRadians)
        {
            return Quaternion.AngleAxis(yawRadians * Mathf.Rad2Deg, Vector3.up)
                * Quaternion.AngleAxis(-pitchRadians * Mathf.Rad2Deg, Vector3.right)
                * Quaternion.AngleAxis(-rollRadians * Mathf.Rad2Deg, Vector3.forward);
        }

        public static Quaternion CameraRotation(Quaternion vehicle, float mountPitch, float minimum, float maximum)
        {
            // This Navigator uses MNT1_TYPE=7, an unstabilized PWM tilt mount.
            // Its reported mount target is body relative, not encoder feedback.
            return vehicle * Quaternion.Euler(-Mathf.Clamp(mountPitch, minimum, maximum), 0f, 0f);
        }

        public static Quaternion VehicleRotationWithoutYaw(Vector3 rollPitchYawDegrees)
        {
            // Keep screen heading fixed while the ROV turns. Use the original
            // ATTITUDE roll/pitch, avoiding Euler extraction near vertical pitch.
            // Physical mount control still uses the complete VehicleRotation.
            return VehicleRotation(rollPitchYawDegrees.x * Mathf.Deg2Rad,
                rollPitchYawDegrees.y * Mathf.Deg2Rad, 0f);
        }

        public static float StabilizedMountPitch(float desiredWorldPitch, Quaternion vehicle, float minimum, float maximum)
        {
            // Camera forward = vehicle * (0, sin(mount), cos(mount)). Solve
            // its vertical component, including roll's effect on the tilt axis.
            float upY = (vehicle * Vector3.up).y;
            float forwardY = (vehicle * Vector3.forward).y;
            float amplitude = Mathf.Sqrt(upY * upY + forwardY * forwardY);
            if (amplitude < 0.0001f) return Mathf.Clamp(0f, minimum, maximum);
            float phase = Mathf.Atan2(forwardY, upY);
            float root = Mathf.Asin(Mathf.Clamp(Mathf.Sin(desiredWorldPitch * Mathf.Deg2Rad) / amplitude, -1f, 1f));
            float first = Mathf.DeltaAngle(0f, (root - phase) * Mathf.Rad2Deg);
            float second = Mathf.DeltaAngle(0f, (Mathf.PI - root - phase) * Mathf.Rad2Deg);
            float best = Mathf.Clamp(0f, minimum, maximum);
            float error = ElevationError(best, desiredWorldPitch, vehicle);
            ChooseMount(first, desiredWorldPitch, vehicle, minimum, maximum, ref best, ref error);
            ChooseMount(second, desiredWorldPitch, vehicle, minimum, maximum, ref best, ref error);
            ChooseMount(minimum, desiredWorldPitch, vehicle, minimum, maximum, ref best, ref error);
            ChooseMount(maximum, desiredWorldPitch, vehicle, minimum, maximum, ref best, ref error);
            return best;
        }

        public static float TrimmedMountPitch(float desiredWorldPitch, Quaternion vehicle, float strength, float minimum, float maximum)
        {
            strength = Mathf.Clamp(strength, 0f, 1.5f);
            if (Mathf.Abs(strength - 1f) < 0.00001f)
                return StabilizedMountPitch(desiredWorldPitch, vehicle, minimum, maximum);
            // Scale only the IMU correction, before applying output limits.
            // Head response stays unchanged when the ROV is level.
            float ideal = StabilizedMountPitch(desiredWorldPitch, vehicle, -180f, 180f);
            float correction = Mathf.DeltaAngle(desiredWorldPitch, ideal);
            return Mathf.Clamp(desiredWorldPitch + strength * correction, minimum, maximum);
        }

        private static float ElevationError(float mount, float desired, Quaternion vehicle)
        {
            return Mathf.Abs(RovHeadTiltMath.Pitch(vehicle * Quaternion.Euler(-mount, 0f, 0f)) - desired);
        }

        private static void ChooseMount(float candidate, float desired, Quaternion vehicle, float minimum, float maximum,
            ref float best, ref float error)
        {
            candidate = Mathf.Clamp(candidate, minimum, maximum);
            float nextError = ElevationError(candidate, desired, vehicle);
            if (nextError < error - 0.0001f || (Mathf.Abs(nextError - error) <= 0.0001f && Mathf.Abs(candidate) < Mathf.Abs(best)))
            { best = candidate; error = nextError; }
        }

        public static bool TryMountPitch(float[] q, out float pitch)
        {
            pitch = 0f;
            if (q == null || q.Length != 4) return false;
            float lengthSquared = 0f;
            foreach (float value in q)
            {
                if (!Finite(value)) return false;
                lengthSquared += value * value;
            }
            if (!Finite(lengthSquared) || lengthSquared < 0.25f || lengthSquared > 4f) return false;
            pitch = Mathf.Asin(Mathf.Clamp(2f * (q[0] * q[2] - q[3] * q[1]) / lengthSquared, -1f, 1f)) * Mathf.Rad2Deg;
            return Finite(pitch);
        }

        public static float FitCylinderFov(Matrix4x4 projection, float aspect, float marginDegrees = 3f)
        {
            if (!Finite(projection.m11) || projection.m11 <= 0.01f) return 210f;
            float verticalTan = (1f + Mathf.Abs(projection.m12)) / projection.m11;
            float halfHeight = Mathf.Atan(verticalTan) + marginDegrees * Mathf.Deg2Rad;
            return Mathf.Clamp(2f * aspect * Mathf.Tan(halfHeight) * Mathf.Rad2Deg, 50f, 280f);
        }
    }
}
