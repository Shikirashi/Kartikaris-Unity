using UnityEngine;

namespace Kartikaris.Player
{
    /// <summary>
    /// Pure tracked-drive intent calculation shared by Player and Enemy.
    /// CharacterController movement remains authoritative; these outputs describe
    /// virtual roller commands and the tuned kinematic translation/yaw response.
    /// </summary>
    public static class TrackedDriveModel
    {
        private const float InputDeadZone = 0.0001f;

        public readonly struct Output
        {
            public Output(
                float leftTrackCommand,
                float rightTrackCommand,
                float longitudinalSpeedMultiplier,
                float yawRateDegreesPerSecond,
                bool isPivotTurn)
            {
                LeftTrackCommand = leftTrackCommand;
                RightTrackCommand = rightTrackCommand;
                LongitudinalSpeedMultiplier = longitudinalSpeedMultiplier;
                YawRateDegreesPerSecond = yawRateDegreesPerSecond;
                IsPivotTurn = isPivotTurn;
            }

            public float LeftTrackCommand { get; }
            public float RightTrackCommand { get; }
            public float LongitudinalSpeedMultiplier { get; }
            public float YawRateDegreesPerSecond { get; }
            public bool IsPivotTurn { get; }
        }

        public static Output Evaluate(
            float throttleInput,
            float steeringInput,
            float currentLongitudinalSpeed,
            float forwardSpeed,
            float reverseSpeed,
            float stationarySpeedThreshold,
            float pivotTurnSpeedDegreesPerSecond,
            float movingTurnSpeedDegreesPerSecond,
            float movingTurnSpeedMultiplier)
        {
            float throttle = Mathf.Clamp(throttleInput, -1f, 1f);
            float steering = Mathf.Clamp(steeringInput, -1f, 1f);
            float steeringMagnitude = Mathf.Abs(steering);
            float safeThreshold = Mathf.Max(0f, stationarySpeedThreshold);
            bool hasThrottle = Mathf.Abs(throttle) > InputDeadZone;
            bool isStationary = Mathf.Abs(currentLongitudinalSpeed) <= safeThreshold;
            bool isPivotTurn = isStationary && !hasThrottle && steeringMagnitude > InputDeadZone;

            if (isPivotTurn)
            {
                // Positive steering is a right turn: left side forward, right side reverse.
                return new Output(
                    steering,
                    -steering,
                    1f,
                    steering * Mathf.Max(0f, pivotTurnSpeedDegreesPerSecond),
                    true);
            }

            float travelDirection = ResolveTravelDirection(throttle, currentLongitudinalSpeed, safeThreshold);
            float trackMagnitude = ResolveTrackMagnitude(
                throttle,
                currentLongitudinalSpeed,
                travelDirection,
                forwardSpeed,
                reverseSpeed);
            float baseTrackCommand = travelDirection * trackMagnitude;
            float leftTrackCommand = baseTrackCommand;
            float rightTrackCommand = baseTrackCommand;

            if (steering > InputDeadZone)
            {
                if (travelDirection >= 0f)
                    rightTrackCommand *= 1f - steeringMagnitude;
                else
                    leftTrackCommand *= 1f - steeringMagnitude;
            }
            else if (steering < -InputDeadZone)
            {
                if (travelDirection >= 0f)
                    leftTrackCommand *= 1f - steeringMagnitude;
                else
                    rightTrackCommand *= 1f - steeringMagnitude;
            }

            float speedMultiplier = Mathf.Lerp(
                1f,
                Mathf.Clamp01(movingTurnSpeedMultiplier),
                steeringMagnitude);
            float yawRate = travelDirection == 0f
                ? 0f
                : steering * Mathf.Max(0f, movingTurnSpeedDegreesPerSecond);

            return new Output(
                Mathf.Clamp(leftTrackCommand, -1f, 1f),
                Mathf.Clamp(rightTrackCommand, -1f, 1f),
                speedMultiplier,
                yawRate,
                false);
        }

        private static float ResolveTravelDirection(
            float throttle,
            float currentLongitudinalSpeed,
            float stationarySpeedThreshold)
        {
            if (Mathf.Abs(throttle) > InputDeadZone)
                return Mathf.Sign(throttle);
            if (Mathf.Abs(currentLongitudinalSpeed) > stationarySpeedThreshold)
                return Mathf.Sign(currentLongitudinalSpeed);
            return 0f;
        }

        private static float ResolveTrackMagnitude(
            float throttle,
            float currentLongitudinalSpeed,
            float travelDirection,
            float forwardSpeed,
            float reverseSpeed)
        {
            if (Mathf.Abs(throttle) > InputDeadZone)
                return Mathf.Abs(throttle);
            if (travelDirection == 0f)
                return 0f;

            float speedLimit = travelDirection > 0f
                ? Mathf.Max(InputDeadZone, forwardSpeed)
                : Mathf.Max(InputDeadZone, reverseSpeed);
            return Mathf.Clamp01(Mathf.Abs(currentLongitudinalSpeed) / speedLimit);
        }
    }
}
