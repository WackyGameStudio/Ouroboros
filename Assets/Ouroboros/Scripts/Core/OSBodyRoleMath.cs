using UnityEngine;

namespace Ouroboros.Core
{
    public static class OSBodyRoleMath
    {
        private const float DirectionEpsilon = 0.000001f;

        public static bool IsInsideBeam(
            Vector2 position,
            Vector2 origin,
            Vector2 direction,
            float length,
            float width)
        {
            if (!IsFinite(position) || !IsFinite(origin) || !IsFinite(direction) ||
                !float.IsFinite(length) || length <= 0f ||
                !float.IsFinite(width) || width <= 0f ||
                direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            var normalizedDirection = direction.normalized;
            var offset = position - origin;
            var along = Vector2.Dot(offset, normalizedDirection);
            if (along < 0f || along > length)
            {
                return false;
            }

            var perpendicular = offset - (normalizedDirection * along);
            var halfWidth = width * 0.5f;
            return perpendicular.sqrMagnitude <= halfWidth * halfWidth;
        }

        public static float SelectControlDuration(
            OSEnemyArchetype archetype,
            float normalDuration,
            float eliteDuration)
        {
            return archetype is OSEnemyArchetype.EliteAccelerator or OSEnemyArchetype.BossSwarmCore
                ? eliteDuration
                : normalDuration;
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }
}
