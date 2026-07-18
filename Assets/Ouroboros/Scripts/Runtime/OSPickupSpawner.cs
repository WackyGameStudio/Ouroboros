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

        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private Transform collectionTarget;
        [SerializeField] private string pickupPoolKey = DefaultPoolKey;
        [SerializeField, Min(1)] private int capacity = DefaultCapacity;
        [SerializeField, Min(0f)] private float mergeRadius = 1.5f;
        [SerializeField, Min(0f)] private float magnetRadius = 1.25f;

        private OSPickup[] _activePickups = Array.Empty<OSPickup>();
        private bool _subscribed;

        public event Action<OSPickupType, int> PickupCollected;

        public int ActiveCount { get; private set; }
        public int Capacity => _activePickups.Length;

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
            if (poolRegistry == null || amount <= 0 || !float.IsFinite(position.x) ||
                !float.IsFinite(position.y))
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.spawn.invalid_input");
            }

            EnsureStorage();
            var nearby = FindNearestSameType(pickupType, position, mergeRadius, true);
            if (nearby != null)
            {
                nearby.AddAmount(amount);
                return OSRuleResult<OSPickup>.Accepted(nearby, "pickup.spawn.nearby_merged");
            }

            var rent = poolRegistry.Rent(pickupPoolKey, position, Quaternion.identity);
            if (rent.IsAccepted && rent.Payload is OSPickup pickup)
            {
                pickup.ConfigurePickup(
                    this,
                    sessionController,
                    collectionTarget,
                    pickupType,
                    amount,
                    magnetRadius);
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

            var fallback = FindNearestSameType(pickupType, position, 0f, false);
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
            if (amount <= 0 || !float.IsFinite(chance) || chance <= 0f || UnityEngine.Random.value > chance)
            {
                return OSRuleResult<OSPickup>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.drop.not_rolled");
            }

            return Spawn(OSPickupType.BodyFragment, amount, position);
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

            PickupCollected?.Invoke(type, amount);
            pickup.ReturnToPool();
            return OSRuleResult<int>.Accepted(amount, "pickup.collect.accepted");
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
            string poolKey = DefaultPoolKey)
        {
            Unsubscribe();
            poolRegistry = pool;
            sessionController = session;
            bodyGrowth = growth;
            collectionTarget = target;
            capacity = Mathf.Max(1, maximumPickups);
            mergeRadius = Mathf.Max(0f, nearbyMergeRadius);
            magnetRadius = Mathf.Max(0f, pickupMagnetRadius);
            pickupPoolKey = poolKey;
            _activePickups = new OSPickup[capacity];
            ActiveCount = 0;
            Subscribe();
        }

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

        private OSPickup FindNearestSameType(
            OSPickupType pickupType,
            Vector2 position,
            float radius,
            bool limitRadius)
        {
            OSPickup nearest = null;
            var nearestDistance = limitRadius ? radius * radius : float.PositiveInfinity;
            for (var index = 0; index < ActiveCount; index++)
            {
                var candidate = _activePickups[index];
                if (candidate == null || !candidate.IsRented || candidate.PickupType != pickupType)
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
            magnetRadius = Mathf.Max(0f, magnetRadius);
        }
    }
}
