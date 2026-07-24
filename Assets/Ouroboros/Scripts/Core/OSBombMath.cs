using System;
using UnityEngine;

namespace Ouroboros.Core
{
    public static class OSBombMath
    {
        public const int DefaultMinimumBodyCount = 10;
        public const float DefaultConsumeRate = 0.1f;
        public const float DefaultDrawDuration = 1f;
        public const float DefaultGatherDuration = 0.5f;
        public const float DefaultRadiusMultiplier = 1.5f;
        public const float DefaultDamagePerBody = 10f;
        public const float DefaultCooldown = 10f;
        public const float MinimumCooldown = 5f;
        public const float SideEpsilon = 0.01f;

        public static int CalculateConsumeCount(int bodyCount, float consumeRate)
        {
            if (bodyCount <= 0 || !float.IsFinite(consumeRate) || consumeRate <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(
                Mathf.FloorToInt(bodyCount * Mathf.Clamp01(consumeRate)),
                0,
                bodyCount);
        }

        public static float CalculateRadius(
            int remainingBodyCount,
            float segmentSpacing,
            float radiusMultiplier = DefaultRadiusMultiplier)
        {
            if (remainingBodyCount <= 0 || !float.IsFinite(segmentSpacing) ||
                !float.IsFinite(radiusMultiplier) || segmentSpacing <= 0f ||
                radiusMultiplier <= 0f)
            {
                return 0f;
            }

            return (remainingBodyCount * segmentSpacing * radiusMultiplier) / (Mathf.PI * 2f);
        }

        public static float CalculateRhythmicProgress(float linearProgress)
        {
            if (!float.IsFinite(linearProgress))
            {
                return 0f;
            }

            var clamped = Mathf.Clamp01(linearProgress);
            return 0.5f - (0.5f * Mathf.Cos(clamped * Mathf.PI));
        }

        public static int ClassifySide(
            Vector2 forward,
            Vector2 offset,
            out float signedLateralDistance,
            float epsilon = SideEpsilon)
        {
            var direction = NormalizeDirection(forward);
            if (!IsFinite(offset))
            {
                signedLateralDistance = 0f;
                return 0;
            }

            signedLateralDistance = (direction.x * offset.y) - (direction.y * offset.x);
            var threshold = Mathf.Max(0f, epsilon);
            if (signedLateralDistance > threshold)
            {
                return 1;
            }

            return signedLateralDistance < -threshold ? -1 : 0;
        }

        public static OSBombTurnSide ResolveTurnSide(
            int leftCount,
            int rightCount,
            float signedLateralSum)
        {
            if (leftCount > rightCount)
            {
                return OSBombTurnSide.Right;
            }

            if (rightCount > leftCount)
            {
                return OSBombTurnSide.Left;
            }

            if (signedLateralSum > SideEpsilon)
            {
                return OSBombTurnSide.Right;
            }

            return signedLateralSum < -SideEpsilon
                ? OSBombTurnSide.Left
                : OSBombTurnSide.Right;
        }

        public static Vector2 CalculateCenter(Vector2 start, Vector2 forward, float radius)
        {
            return start + (NormalizeDirection(forward) * Mathf.Max(0f, radius));
        }

        public static Vector2 CalculateOrbitPoint(
            Vector2 start,
            Vector2 forward,
            float radius,
            float progress,
            OSBombTurnSide turnSide)
        {
            var safeRadius = Mathf.Max(0f, radius);
            var direction = NormalizeDirection(forward);
            var center = CalculateCenter(start, direction, safeRadius);
            var radial = -direction * safeRadius;
            var angle = Mathf.Clamp01(progress) * Mathf.PI * 2f *
                        (turnSide == OSBombTurnSide.Right ? 1f : -1f);
            var cosine = Mathf.Cos(angle);
            var sine = Mathf.Sin(angle);
            var rotated = new Vector2(
                (radial.x * cosine) - (radial.y * sine),
                (radial.x * sine) + (radial.y * cosine));
            return center + rotated;
        }

        public static float CalculateDamage(
            int bodyCount,
            float damagePerBody,
            float multiplier)
        {
            if (bodyCount <= 0 || !float.IsFinite(damagePerBody) ||
                !float.IsFinite(multiplier))
            {
                return 0f;
            }

            return bodyCount * Mathf.Max(0f, damagePerBody) * Mathf.Max(0.01f, multiplier);
        }

        public static float CalculateCooldown(float baseCooldown, float delta)
        {
            if (!float.IsFinite(baseCooldown) || !float.IsFinite(delta))
            {
                return MinimumCooldown;
            }

            return Mathf.Max(MinimumCooldown, Mathf.Max(0f, baseCooldown) + delta);
        }

        private static Vector2 NormalizeDirection(Vector2 direction)
        {
            return IsFinite(direction) && direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.right;
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }
}
