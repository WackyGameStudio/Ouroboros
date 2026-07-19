using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Preserves pickup amounts by merging nearby requests and falling back to a same-type pickup at pool capacity.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class OSPickupSpawner : MonoBehaviour
    {
        private const string DefaultPoolKey = "body_fragment_pickup";
        private const int DefaultCapacity = 256;
        private const int ScatterSlotsPerRing = 8;
        private const int MaxScatterRings = 16;

        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private Transform collectionTarget;
        [SerializeField] private string pickupPoolKey = DefaultPoolKey;
        [SerializeField, Min(1)] private int capacity = DefaultCapacity;
        [SerializeField, Min(0f)] private float mergeRadius = 1.5f;
        [SerializeField, Min(0f)] private float spawnSeparation = 0.55f;
        [SerializeField, Min(0f)] private float magnetRadius = 1.25f;

        private OSPickup[] _activePickups = Array.Empty<OSPickup>();
        private bool _subscribed;
        private float _magnetMultiplier = 1f;

        public event Action<OSPickupType, int> PickupCollected;

        public int ActiveCount { get; private set; }
        public int Capacity => _activePickups.Length;
        public float MagnetRadius => EffectiveMagnetRadius;
        public float SpawnSeparation => spawnSeparation;

        private void Awake()
        {
            EnsureStorage();
        }

        private void OnEnable()
        {
            EnsureStorage();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Spawns or merges one amount-preserving pickup request.
        /// </summary>
        public OSRuleResult<OSPickup> Spawn(OSPickupType pickupType, int amount, Vector2 position)
        {
            if (pickupType == OSPickupType.SeveredBody)
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.spawn.severed_role_required");
            }

            return SpawnInternal(pickupType, amount, position, true, default, false);
        }

        /// <summary>
        /// Keeps each cut segment as a role-preserving pickup. Separate cut pieces do not merge
        /// on spawn, while an exhausted pool may merge only with the same severed role.
        /// </summary>
        public OSRuleResult<OSPickup> SpawnSeveredBody(
            OSBodyRoleType bodyRole,
            int amount,
            Vector2 position)
        {
            if (!Enum.IsDefined(typeof(OSBodyRoleType), bodyRole))
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.spawn.invalid_body_role");
            }

            return SpawnInternal(
                OSPickupType.SeveredBody,
                amount,
                position,
                false,
                bodyRole,
                true);
        }

        private OSRuleResult<OSPickup> SpawnInternal(
            OSPickupType pickupType,
            int amount,
            Vector2 position,
            bool mergeNearby,
            OSBodyRoleType bodyRole,
            bool preserveBodyRole)
        {
            if (poolRegistry == null || amount <= 0 || !float.IsFinite(position.x) ||
                !float.IsFinite(position.y))
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.spawn.invalid_input");
            }

            EnsureStorage();
            var nearby = mergeNearby
                ? FindNearestMergeTarget(
                    pickupType,
                    bodyRole,
                    preserveBodyRole,
                    position,
                    mergeRadius,
                    true)
                : null;
            if (nearby != null)
            {
                nearby.AddAmount(amount);
                return OSRuleResult<OSPickup>.Accepted(nearby, "pickup.spawn.nearby_merged");
            }

            var spawnPosition = ResolveSeparatedSpawnPosition(pickupType, position);
            var rent = poolRegistry.Rent(pickupPoolKey, spawnPosition, Quaternion.identity);
            if (rent.IsAccepted && rent.Payload is OSPickup pickup)
            {
                if (preserveBodyRole)
                {
                    pickup.ConfigureSeveredBodyPickup(
                        this,
                        sessionController,
                        collectionTarget,
                        bodyRole,
                        amount,
                        EffectiveMagnetRadius);
                }
                else
                {
                    pickup.ConfigurePickup(
                        this,
                        sessionController,
                        collectionTarget,
                        pickupType,
                        amount,
                        EffectiveMagnetRadius);
                }

                var registration = Register(pickup);
                if (!registration.IsAccepted)
                {
                    pickup.ReturnToPool();
                    return OSRuleResult<OSPickup>.Rejected(
                        registration.Code,
                        registration.ReasonKey);
                }

                return OSRuleResult<OSPickup>.Accepted(pickup, "pickup.spawn.rented");
            }

            var fallback = FindNearestMergeTarget(
                pickupType,
                bodyRole,
                preserveBodyRole,
                position,
                0f,
                false);
            if (fallback == null)
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "pickup.spawn.no_merge_target");
            }

            fallback.AddAmount(amount);
            return OSRuleResult<OSPickup>.Accepted(fallback, "pickup.spawn.capacity_merged");
        }

        /// <summary>
        /// Applies one enemy fragment drop roll and delegates successful requests to Spawn.
        /// </summary>
        public OSRuleResult<OSPickup> TrySpawnFragmentDrop(Vector2 position, int amount, float chance)
        {
            return TrySpawnDrop(OSPickupType.BodyFragment, position, amount, chance);
        }

        public OSRuleResult<OSPickup> TrySpawnDrop(
            OSPickupType pickupType,
            Vector2 position,
            int amount,
            float chance)
        {
            if (amount <= 0 || !float.IsFinite(chance) || chance <= 0f || UnityEngine.Random.value > chance)
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.drop.not_rolled");
            }

            return Spawn(pickupType, amount, position);
        }

        internal OSRuleResult<int> Collect(OSPickup pickup, OSPickupCollector collector)
        {
            if (pickup == null || collector == null || !pickup.IsRented ||
                collectionTarget != null && collector.CollectionTarget != collectionTarget)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.collect.invalid_collector");
            }

            var type = pickup.PickupType;
            var amount = pickup.Amount;
            OSRuleResult<int> applied;
            switch (type)
            {
                case OSPickupType.BodyFragment:
                    applied = bodyGrowth != null
                        ? bodyGrowth.AddFragments(amount)
                        : OSRuleResult<int>.Rejected(
                            OSResultCode.ConfigurationError,
                            "pickup.collect.growth_missing");
                    break;
                case OSPickupType.Experience:
                    applied = levelUpController != null
                        ? levelUpController.AddExperience(amount)
                        : OSRuleResult<int>.Rejected(
                            OSResultCode.ConfigurationError,
                            "pickup.collect.level_up_missing");
                    break;
                case OSPickupType.Heal:
                    var heal = playerHealth != null
                        ? playerHealth.TryHeal(amount)
                        : OSRuleResult<float>.Rejected(
                            OSResultCode.ConfigurationError,
                            "pickup.collect.health_missing");
                    applied = heal.IsAccepted
                        ? OSRuleResult<int>.Accepted(amount, "pickup.collect.heal_applied")
                        : OSRuleResult<int>.Rejected(heal.Code, heal.ReasonKey);
                    break;
                case OSPickupType.SeveredBody:
                    applied = bodyGrowth != null
                        ? bodyGrowth.ReclaimSegments(pickup.BodyRole, amount)
                        : OSRuleResult<int>.Rejected(
                            OSResultCode.ConfigurationError,
                            "pickup.collect.growth_missing");
                    break;
                default:
                    applied = OSRuleResult<int>.Rejected(
                        OSResultCode.RejectedRequirement,
                        "pickup.collect.type_not_implemented");
                    break;
            }

            if (!applied.IsAccepted)
            {
                return applied;
            }

            var collectedAmount = type == OSPickupType.SeveredBody
                ? Mathf.Clamp(applied.Payload, 0, amount)
                : amount;
            if (collectedAmount <= 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "pickup.collect.no_capacity");
            }

            if (collectedAmount < amount)
            {
                pickup.RemoveAmount(collectedAmount);
                PickupCollected?.Invoke(type, collectedAmount);
                return OSRuleResult<int>.Accepted(
                    collectedAmount,
                    "pickup.collect.partial_reclaim");
            }

            pickup.ReturnToPool();
            PickupCollected?.Invoke(type, collectedAmount);
            return OSRuleResult<int>.Accepted(collectedAmount, "pickup.collect.accepted");
        }

        internal void Unregister(OSPickup pickup)
        {
            if (pickup == null || pickup.RegistryIndex < 0 || pickup.RegistryIndex >= ActiveCount ||
                _activePickups[pickup.RegistryIndex] != pickup)
            {
                return;
            }

            var removedIndex = pickup.RegistryIndex;
            var lastIndex = --ActiveCount;
            if (removedIndex != lastIndex)
            {
                var moved = _activePickups[lastIndex];
                _activePickups[removedIndex] = moved;
                moved.RegistryIndex = removedIndex;
            }

            _activePickups[lastIndex] = null;
            pickup.RegistryIndex = -1;
        }

        internal void ConfigureForTesting(
            OSPoolRegistry pool,
            OSGameSessionController session,
            OSBodyGrowthController growth,
            Transform target,
            int maximumPickups,
            float nearbyMergeRadius = 1.5f,
            float pickupMagnetRadius = 1.25f,
            string poolKey = DefaultPoolKey,
            OSLevelUpController levels = null,
            OSPlayerHealth health = null,
            float minimumSpawnSeparation = 0.55f)
        {
            Unsubscribe();
            poolRegistry = pool;
            sessionController = session;
            bodyGrowth = growth;
            levelUpController = levels;
            playerHealth = health;
            collectionTarget = target;
            capacity = Mathf.Max(1, maximumPickups);
            mergeRadius = Mathf.Max(0f, nearbyMergeRadius);
            spawnSeparation = Mathf.Max(0f, minimumSpawnSeparation);
            magnetRadius = Mathf.Max(0f, pickupMagnetRadius);
            pickupPoolKey = poolKey;
            _activePickups = new OSPickup[capacity];
            ActiveCount = 0;
            Subscribe();
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _magnetMultiplier = Mathf.Max(0.01f, modifiers.MagnetMultiplier);
            var radius = EffectiveMagnetRadius;
            for (var index = 0; index < ActiveCount; index++)
            {
                _activePickups[index]?.SetMagnetRadius(radius);
            }
        }

        private float EffectiveMagnetRadius => Mathf.Max(0f, magnetRadius * _magnetMultiplier);

        private OSRuleResult<int> Register(OSPickup pickup)
        {
            if (pickup == null || ActiveCount >= _activePickups.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "pickup.registry.capacity");
            }

            pickup.RegistryIndex = ActiveCount;
            _activePickups[ActiveCount++] = pickup;
            return OSRuleResult<int>.Accepted(pickup.RegistryIndex, "pickup.registry.registered");
        }

        private OSPickup FindNearestMergeTarget(
            OSPickupType pickupType,
            OSBodyRoleType bodyRole,
            bool preserveBodyRole,
            Vector2 position,
            float radius,
            bool limitRadius)
        {
            OSPickup nearest = null;
            var nearestDistance = limitRadius ? radius * radius : float.PositiveInfinity;
            for (var index = 0; index < ActiveCount; index++)
            {
                var candidate = _activePickups[index];
                if (candidate == null || !candidate.IsRented || candidate.PickupType != pickupType ||
                    preserveBodyRole && candidate.BodyRole != bodyRole)
                {
                    continue;
                }

                var distance = (candidate.Position - position).sqrMagnitude;
                if (distance > nearestDistance)
                {
                    continue;
                }

                if (nearest == null || distance < nearestDistance ||
                    Mathf.Approximately(distance, nearestDistance) && candidate.RuntimeId < nearest.RuntimeId)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private Vector2 ResolveSeparatedSpawnPosition(OSPickupType pickupType, Vector2 origin)
        {
            if (spawnSeparation <= 0f || IsSpawnPositionClear(origin))
            {
                return origin;
            }

            var angleOffset = ((int)pickupType * 3 + ActiveCount) % ScatterSlotsPerRing;
            for (var ring = 1; ring <= MaxScatterRings; ring++)
            {
                var radius = spawnSeparation * ring;
                for (var slot = 0; slot < ScatterSlotsPerRing; slot++)
                {
                    var angleIndex = (angleOffset + slot) % ScatterSlotsPerRing;
                    var angle = angleIndex * Mathf.PI * 2f / ScatterSlotsPerRing;
                    var candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (IsSpawnPositionClear(candidate))
                    {
                        return candidate;
                    }
                }
            }

            // A local cluster dense enough to fill every bounded ring is exceptional. Keep the
            // request amount-preserving and place it just beyond the searched region.
            var fallbackAngle = angleOffset * Mathf.PI * 2f / ScatterSlotsPerRing;
            return origin + new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle)) *
                (spawnSeparation * (MaxScatterRings + 1));
        }

        private bool IsSpawnPositionClear(Vector2 candidate)
        {
            var minimumDistanceSquared = spawnSeparation * spawnSeparation;
            for (var index = 0; index < ActiveCount; index++)
            {
                var pickup = _activePickups[index];
                if (pickup != null && pickup.IsRented &&
                    (pickup.Position - candidate).sqrMagnitude < minimumDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureStorage()
        {
            var desired = Mathf.Max(1, capacity);
            if (_activePickups.Length != desired && ActiveCount == 0)
            {
                _activePickups = new OSPickup[desired];
            }
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleSessionStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                poolRegistry?.ReturnAll(pickupPoolKey);
            }
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(1, capacity);
            mergeRadius = Mathf.Max(0f, mergeRadius);
            spawnSeparation = Mathf.Max(0f, spawnSeparation);
            magnetRadius = Mathf.Max(0f, magnetRadius);
        }
    }
}
