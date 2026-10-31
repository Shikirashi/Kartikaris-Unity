using UnityEngine;

namespace Kartikaris.Combat
{
    public enum KartikarisTrajectoryPreference
    {
        LowAngle,
        HighAngle
    }

    public static class KartikarisBallisticSolver
    {
        /// <summary>
        /// Solves using base ballistic velocity and gravity at the default 1x
        /// real-time traversal rate.
        /// </summary>
        public static bool TrySolveTrajectory(
            Vector3 origin,
            Vector3 target,
            float muzzleVelocity,
            Vector3 gravity,
            KartikarisTrajectoryPreference preference,
            out Vector3 launchDirection,
            out float flightTime)
        {
            return TrySolveTrajectory(origin, target, muzzleVelocity, gravity, 1f,
                preference, out launchDirection, out flightTime);
        }

        /// <summary>
        /// Solves the unchanged world-space parabola from base velocity/gravity.
        /// Only the returned real-time duration is scaled: base time / k. The
        /// projectile must launch at base velocity x k with gravity x k squared.
        /// </summary>
        public static bool TrySolveTrajectory(
            Vector3 origin,
            Vector3 target,
            float muzzleVelocity,
            Vector3 gravity,
            float ballisticTravelSpeedMultiplier,
            KartikarisTrajectoryPreference preference,
            out Vector3 launchDirection,
            out float flightTime)
        {
            launchDirection = Vector3.zero;
            flightTime = 0f;
            float speed = Mathf.Max(0f, muzzleVelocity);
            float gravityMagnitude = gravity.magnitude;
            float timeMultiplier = Mathf.Max(0.01f, ballisticTravelSpeedMultiplier);
            if (speed <= Mathf.Epsilon || gravityMagnitude <= Mathf.Epsilon)
                return false;

            Vector3 up = -gravity / gravityMagnitude;
            Vector3 displacement = target - origin;
            float vertical = Vector3.Dot(displacement, up);
            Vector3 horizontalVector = displacement - up * vertical;
            float horizontal = horizontalVector.magnitude;
            if (horizontal <= Mathf.Epsilon)
            {
                if (vertical <= 0f)
                    return false;
                launchDirection = up;
                float discriminantVertical = speed * speed - 2f * gravityMagnitude * vertical;
                if (discriminantVertical < 0f)
                    return false;
                float baseFlightTime =
                    (speed + Mathf.Sqrt(discriminantVertical)) / gravityMagnitude;
                flightTime = baseFlightTime / timeMultiplier;
                return flightTime > 0f;
            }

            float speedSquared = speed * speed;
            float discriminant = speedSquared * speedSquared - gravityMagnitude *
                (gravityMagnitude * horizontal * horizontal + 2f * vertical * speedSquared);
            if (discriminant < 0f)
                return false;

            float root = Mathf.Sqrt(Mathf.Max(0f, discriminant));
            float tangent = (speedSquared +
                (preference == KartikarisTrajectoryPreference.HighAngle ? root : -root)) /
                (gravityMagnitude * horizontal);
            float cosine = 1f / Mathf.Sqrt(1f + tangent * tangent);
            float sine = tangent * cosine;
            launchDirection = (horizontalVector / horizontal * cosine + up * sine).normalized;
            float baseHorizontalFlightTime =
                horizontal / Mathf.Max(Mathf.Epsilon, speed * cosine);
            flightTime = baseHorizontalFlightTime / timeMultiplier;
            return float.IsFinite(flightTime) && flightTime > 0f;
        }
    }
}
