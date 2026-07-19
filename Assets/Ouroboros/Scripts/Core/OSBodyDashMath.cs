using UnityEngine;

namespace Ouroboros.Core
{
    public static class OSBodyDashMath
    {
        public const float MinimumDuration = 0.01f;
        public const float MinimumDistance = 0.1f;
        public const float MinimumCooldown = 0.5f;

        public static Vector2 ResolveDirection(Vector2 moveInput, Vector2 lastDirection)
        {
            var candidate = IsFinite(moveInput) && moveInput.sqrMagnitude > 0.0001f
                ? moveInput
                : lastDirection;
            return IsFinite(candidate) && candidate.sqrMagnitude > 0.0001f
                ? candidate.normalized
                : Vector2.right;
        }

        public static float EaseOutCubic(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            var inverse = 1f - t;
            return 1f - (inverse * inverse * inverse);
        }

        public static float CalculateStepDistance(
            float totalDistance,
            float duration,
            float previousElapsed,
            float nextElapsed)
        {
            if (!float.IsFinite(totalDistance) || !float.IsFinite(duration) ||
                !float.IsFinite(previousElapsed) || !float.IsFinite(nextElapsed) ||
                totalDistance <= 0f || duration <= 0f || nextElapsed <= previousElapsed)
            {
                return 0f;
            }

            var previous = EaseOutCubic(previousElapsed / duration);
            var next = EaseOutCubic(nextElapsed / duration);
            return Mathf.Max(0f, (next - previous) * totalDistance);
        }

        public static float CalculateDistance(float baseDistance, float multiplier)
        {
            if (!float.IsFinite(baseDistance) || !float.IsFinite(multiplier))
            {
                return MinimumDistance;
            }

            return Mathf.Max(MinimumDistance, baseDistance * Mathf.Max(0.01f, multiplier));
        }

        public static float CalculateCooldown(float baseCooldown, float multiplier, float delta = 0f)
        {
            if (!float.IsFinite(baseCooldown) || !float.IsFinite(multiplier) || !float.IsFinite(delta))
            {
                return MinimumCooldown;
            }

            return Mathf.Max(
                MinimumCooldown,
                (baseCooldown * Mathf.Max(0.01f, multiplier)) + delta);
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }
}
