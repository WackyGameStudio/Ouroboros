using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-4800)]
    [DisallowMultipleComponent]
    public sealed class OSEnemyRegistry : MonoBehaviour
    {
        private const float DistanceTieEpsilon = 0.000001f;

        [SerializeField, Min(1)] private int capacity = 200;

        private OSEnemyController[] _activeEnemies;

        public int Count { get; private set; }
        public int Capacity => _activeEnemies?.Length ?? Mathf.Max(1, capacity);

        private void Awake()
        {
            EnsureStorage();
        }

        public OSEnemyController GetAt(int index)
        {
            return index >= 0 && index < Count ? _activeEnemies[index] : null;
        }

        public bool TryGetByRuntimeId(int runtimeId, out OSEnemyController enemy)
        {
            for (var index = 0; index < Count; index++)
            {
                var candidate = _activeEnemies[index];
                if (candidate != null && candidate.RuntimeId == runtimeId)
                {
                    enemy = candidate;
                    return true;
                }
            }

            enemy = null;
            return false;
        }

        public OSEnemyController FindNearestTarget(
            Vector2 origin,
            float range,
            int currentTargetRuntimeId = 0,
            bool prioritizeEliteOrBoss = false)
        {
            if (!float.IsFinite(range) || range <= 0f)
            {
                return null;
            }

            var rangeSquared = range * range;
            var bestDistanceSquared = float.PositiveInfinity;
            var bestPriority = -1;
            OSEnemyController best = null;
            for (var index = 0; index < Count; index++)
            {
                var candidate = _activeEnemies[index];
                if (candidate == null || !candidate.IsRented || candidate.IsDeathConfirmed ||
                    candidate.CurrentHealth <= 0f)
                {
                    continue;
                }

                var distanceSquared = (candidate.Position - origin).sqrMagnitude;
                if (distanceSquared > rangeSquared)
                {
                    continue;
                }

                var priority = prioritizeEliteOrBoss && candidate.IsEliteOrBoss ? 1 : 0;
                if (priority < bestPriority)
                {
                    continue;
                }

                if (priority > bestPriority)
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                    bestPriority = priority;
                    continue;
                }

                if (distanceSquared < bestDistanceSquared - DistanceTieEpsilon)
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                    bestPriority = priority;
                    continue;
                }

                if (Mathf.Abs(distanceSquared - bestDistanceSquared) > DistanceTieEpsilon)
                {
                    continue;
                }

                if (candidate.RuntimeId == currentTargetRuntimeId)
                {
                    best = candidate;
                }
                else if (best != null && best.RuntimeId != currentTargetRuntimeId &&
                         candidate.RuntimeId < best.RuntimeId)
                {
                    best = candidate;
                }
            }

            return best;
        }

        public OSRuleResult<int> Register(OSEnemyController enemy)
        {
            EnsureStorage();
            if (enemy == null || !enemy.IsRented || enemy.RuntimeId <= 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "enemy_registry.register.invalid_enemy");
            }

            if (enemy.RegistryIndex >= 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.Duplicate,
                    "enemy_registry.register.duplicate",
                    enemy.RuntimeId);
            }

            if (Count >= _activeEnemies.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "enemy_registry.register.capacity",
                    enemy.RuntimeId);
            }

            enemy.RegistryIndex = Count;
            _activeEnemies[Count++] = enemy;
            return OSRuleResult<int>.Accepted(enemy.RuntimeId, "enemy_registry.register.accepted");
        }

        public OSRuleResult<int> Unregister(OSEnemyController enemy)
        {
            if (enemy == null || enemy.RegistryIndex < 0 || enemy.RegistryIndex >= Count ||
                _activeEnemies[enemy.RegistryIndex] != enemy)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.Duplicate,
                    "enemy_registry.unregister.missing",
                    enemy != null ? enemy.RuntimeId : 0);
            }

            var removedRuntimeId = enemy.RuntimeId;
            var removeIndex = enemy.RegistryIndex;
            var lastIndex = --Count;
            var movedEnemy = _activeEnemies[lastIndex];
            _activeEnemies[lastIndex] = null;
            enemy.RegistryIndex = -1;

            if (removeIndex != lastIndex)
            {
                _activeEnemies[removeIndex] = movedEnemy;
                movedEnemy.RegistryIndex = removeIndex;
            }

            return OSRuleResult<int>.Accepted(removedRuntimeId, "enemy_registry.unregister.accepted");
        }

        public Vector2 CalculateSeparation(
            OSEnemyController self,
            Vector2 position,
            float radius)
        {
            if (self == null || radius <= 0f)
            {
                return Vector2.zero;
            }

            var radiusSquared = radius * radius;
            var separation = Vector2.zero;
            for (var index = 0; index < Count; index++)
            {
                var other = _activeEnemies[index];
                if (other == null || other == self)
                {
                    continue;
                }

                var offset = position - other.Position;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared <= 0.000001f || distanceSquared >= radiusSquared)
                {
                    continue;
                }

                separation += offset * ((radiusSquared - distanceSquared) / radiusSquared);
            }

            return Vector2.ClampMagnitude(separation, 1f);
        }

        public int ReturnAll()
        {
            var returned = 0;
            var safety = Count;
            while (Count > 0 && safety-- > 0)
            {
                var enemy = _activeEnemies[Count - 1];
                if (enemy != null && enemy.ReturnToPool())
                {
                    returned++;
                    continue;
                }

                Unregister(enemy);
            }

            return returned;
        }

        internal void ConfigureForTesting(int requestedCapacity)
        {
            capacity = Mathf.Max(1, requestedCapacity);
            _activeEnemies = new OSEnemyController[capacity];
            Count = 0;
        }

        private void EnsureStorage()
        {
            _activeEnemies ??= new OSEnemyController[Mathf.Max(1, capacity)];
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(1, capacity);
        }
    }
}
