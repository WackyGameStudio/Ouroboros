using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    public static class OSExplosionMath
    {
        public static int CalculateConsumeCount(int activeSegmentCount, float consumeRate)
        {
            if (activeSegmentCount <= 0 || !float.IsFinite(consumeRate) || consumeRate <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(
                Mathf.Max(1, Mathf.CeilToInt(activeSegmentCount * consumeRate)),
                1,
                activeSegmentCount);
        }

        public static bool IsInsideAnyCircle(
            Vector2 position,
            IReadOnlyList<Vector2> centers,
            float radius)
        {
            if (centers == null || centers.Count == 0 || !float.IsFinite(radius) || radius <= 0f)
            {
                return false;
            }

            var radiusSquared = radius * radius;
            for (var index = 0; index < centers.Count; index++)
            {
                if ((position - centers[index]).sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
