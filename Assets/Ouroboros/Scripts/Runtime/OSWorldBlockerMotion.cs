using UnityEngine;

namespace Ouroboros.Runtime
{
    internal static class OSWorldBlockerMotion
    {
        internal const float SkinWidth = 0.02f;
        private const float MinimumDistance = 0.000001f;

        internal static ContactFilter2D CreateFilter(LayerMask worldBlockerMask)
        {
            return new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = worldBlockerMask,
                useTriggers = false
            };
        }

        internal static bool TryGetClosestHit(
            Collider2D collider,
            Vector2 direction,
            float distance,
            ContactFilter2D filter,
            RaycastHit2D[] hits,
            out RaycastHit2D closestHit)
        {
            closestHit = default;
            if (collider == null || hits == null || hits.Length == 0 ||
                distance <= MinimumDistance || direction.sqrMagnitude <= MinimumDistance ||
                !filter.useLayerMask || filter.layerMask == 0)
            {
                return false;
            }

            direction.Normalize();
            var hitCount = collider.Cast(
                direction,
                filter,
                hits,
                distance + SkinWidth,
                true);
            var closestDistance = float.PositiveInfinity;
            var found = false;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = hits[index];
                if (hit.collider == null || hit.distance < 0f || hit.distance >= closestDistance ||
                    hit.distance <= MinimumDistance && Vector2.Dot(direction, hit.normal) >= -0.0001f)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }

            return found;
        }

        internal static bool ContainsLayer(LayerMask mask, int layer)
        {
            return layer is >= 0 and < 32 && (mask.value & (1 << layer)) != 0;
        }
    }
}
