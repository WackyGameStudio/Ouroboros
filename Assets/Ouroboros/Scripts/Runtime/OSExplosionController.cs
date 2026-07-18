using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSExplosionResolution
    {
        public OSExplosionResolution(
            int requestId,
            OSResultCode code,
            int consumedCount,
            float damagePerEnemy,
            int hitCount,
            int killCount)
        {
            RequestId = requestId;
            Code = code;
            ConsumedCount = consumedCount;
            DamagePerEnemy = damagePerEnemy;
            HitCount = hitCount;
            KillCount = killCount;
        }

        public int RequestId { get; }
        public OSResultCode Code { get; }
        public int ConsumedCount { get; }
        public float DamagePerEnemy { get; }
        public int HitCount { get; }
        public int KillCount { get; }
        public bool WasCancelled => Code == OSResultCode.CancelledNoReservedSegment;
    }

    /// <summary>
    /// Owns the deterministic tail reservation, telegraph and atomic explosion resolution.
    /// It runs after OSPlayerCombatResolver so lethal head damage and body cuts win the tick.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class OSExplosionController : MonoBehaviour
    {
        private const int ReservationCapacity = 64;
        private const int DefaultMinimumSegments = 4;
        private const float DefaultConsumeRate = 0.30f;
        private const float DefaultTelegraphDuration = 0.25f;
        private const float DefaultRadius = 1.8f;
        private const float DefaultDamagePerSegment = 35f;
        private const float DefaultHeadInvulnerability = 0.4f;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyBalanceData bodyBalance;

        private readonly int[] _reservedStableIds = new int[ReservationCapacity];
        private readonly Vector2[] _reservedCenters = new Vector2[ReservationCapacity];
        private int _reservedCount;
        private int _initialBodyCount;
        private int _requestId;
        private float _telegraphRemaining;
        private bool _active;
        private bool _subscribed;
        private OSUpgradeModifiers _upgradeModifiers = OSUpgradeModifiers.Default;

        private int _testMinimumSegments;
        private float _testConsumeRate = -1f;
        private float _testTelegraphDuration = -1f;
        private float _testRadius = -1f;
        private float _testDamagePerSegment = -1f;
        private float _testHeadInvulnerability = -1f;

        public event Action<OSExplosionSnapshot> TelegraphStarted;
        public event Action<OSExplosionSnapshot> ReservationChanged;
        public event Action<OSExplosionResolution> ExplosionResolved;
        public event Action<OSResultCode, string> RequestRejected;

        public bool IsTelegraphActive => _active;
        public int ReservedCount => _reservedCount;
        public int InitialBodyCount => _initialBodyCount;
        public int ExpectedRemainingBodyCount => bodyChain != null
            ? Mathf.Max(0, bodyChain.ActiveCount - _reservedCount)
            : 0;
        public float TelegraphRemaining => _telegraphRemaining;
        public float Radius => EffectiveRadius;
        public float ConsumeRate => EffectiveConsumeRate;
        public float DamagePerSegment => EffectiveDamagePerSegment;
        public int LastHitCount { get; private set; }
        public int LastKillCount { get; private set; }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            if (!_active)
            {
                return;
            }

            if (sessionController == null || sessionController.State != OSSessionState.ExplosionTelegraph)
            {
                CancelWithoutReturningToCombat(OSResultCode.CancelledNoReservedSegment);
                return;
            }

            SimulateTelegraph(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearReservation();
        }

        public OSRuleResult<int> RequestExplosion()
        {
            if (_active || sessionController == null || bodyChain == null ||
                sessionController.State != OSSessionState.Combat)
            {
                return Reject(OSResultCode.RejectedState, "explosion.request.invalid_state");
            }

            var activeCount = bodyChain.ActiveCount;
            if (activeCount < EffectiveMinimumSegments)
            {
                return Reject(OSResultCode.RejectedRequirement, "explosion.request.not_enough_body");
            }

            var consumeCount = OSExplosionMath.CalculateConsumeCount(activeCount, EffectiveConsumeRate);
            if (consumeCount <= 0 || consumeCount > ReservationCapacity)
            {
                return Reject(OSResultCode.ConfigurationError, "explosion.request.invalid_reservation");
            }

            var startIndex = activeCount - consumeCount;
            for (var index = 0; index < consumeCount; index++)
            {
                var segment = bodyChain.GetActiveSegment(startIndex + index);
                if (segment == null || !segment.IsActive || segment.View == null)
                {
                    ClearReservation();
                    return Reject(OSResultCode.ConfigurationError, "explosion.request.segment_missing");
                }

                _reservedStableIds[index] = segment.StableId;
                _reservedCenters[index] = segment.View.transform.position;
            }

            var stateResult = sessionController.BeginExplosionTelegraph();
            if (!stateResult.IsAccepted)
            {
                ClearReservation();
                return Reject(stateResult.Code, stateResult.ReasonKey);
            }

            _requestId++;
            _initialBodyCount = activeCount;
            _reservedCount = consumeCount;
            _telegraphRemaining = EffectiveTelegraphDuration;
            _active = true;
            LastHitCount = 0;
            LastKillCount = 0;
            TelegraphStarted?.Invoke(CreateSnapshot());

            if (_telegraphRemaining <= 0f)
            {
                CompleteExplosion();
            }

            return OSRuleResult<int>.Accepted(consumeCount, "explosion.request.accepted");
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            OSBodyChain chain,
            OSEnemyRegistry registry,
            OSPlayerHealth health,
            OSBodyGrowthController growth = null,
            int minimumSegments = DefaultMinimumSegments,
            float consumeRate = DefaultConsumeRate,
            float telegraphDuration = DefaultTelegraphDuration,
            float radius = DefaultRadius,
            float damagePerSegment = DefaultDamagePerSegment,
            float headInvulnerability = DefaultHeadInvulnerability)
        {
            Unsubscribe();
            sessionController = session;
            bodyChain = chain;
            enemyRegistry = registry;
            playerHealth = health;
            bodyGrowth = growth;
            bodyBalance = null;
            _testMinimumSegments = Mathf.Max(1, minimumSegments);
            _testConsumeRate = Mathf.Max(0.0001f, consumeRate);
            _testTelegraphDuration = Mathf.Max(0f, telegraphDuration);
            _testRadius = Mathf.Max(0.0001f, radius);
            _testDamagePerSegment = Mathf.Max(0.0001f, damagePerSegment);
            _testHeadInvulnerability = Mathf.Max(0f, headInvulnerability);
            ClearReservation();
            Subscribe();
        }

        internal void SimulateTelegraphForTesting(float deltaTime)
        {
            SimulateTelegraph(deltaTime);
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _upgradeModifiers = modifiers;
        }

        private int EffectiveMinimumSegments => _testMinimumSegments > 0
            ? _testMinimumSegments
            : bodyBalance != null ? bodyBalance.Explosion.MinimumSegments : DefaultMinimumSegments;
        private float EffectiveConsumeRate => _testConsumeRate >= 0f
            ? _testConsumeRate
            : OSUpgradeMath.CalculateExplosionConsumeRate(
                bodyBalance != null ? bodyBalance.Explosion.ConsumeRate : DefaultConsumeRate,
                _upgradeModifiers.ExplosionConsumeRateDelta);
        private float EffectiveTelegraphDuration => _testTelegraphDuration >= 0f
            ? _testTelegraphDuration
            : bodyBalance != null ? bodyBalance.Explosion.TelegraphDuration : DefaultTelegraphDuration;
        private float EffectiveRadius => _testRadius >= 0f
            ? _testRadius
            : (bodyBalance != null ? bodyBalance.Explosion.Radius : DefaultRadius) *
              _upgradeModifiers.ExplosionRadiusMultiplier;
        private float EffectiveDamagePerSegment => _testDamagePerSegment >= 0f
            ? _testDamagePerSegment
            : (bodyBalance != null ? bodyBalance.Explosion.DamagePerSegment : DefaultDamagePerSegment) *
              _upgradeModifiers.ExplosionDamageMultiplier;
        private float EffectiveHeadInvulnerability => _testHeadInvulnerability >= 0f
            ? _testHeadInvulnerability
            : bodyBalance != null ? bodyBalance.Explosion.HeadInvulnerability : DefaultHeadInvulnerability;

        private void SimulateTelegraph(float deltaTime)
        {
            if (!_active || !float.IsFinite(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            _telegraphRemaining = Mathf.Max(0f, _telegraphRemaining - deltaTime);
            if (_telegraphRemaining <= 0f)
            {
                CompleteExplosion();
            }
        }

        private void CompleteExplosion()
        {
            if (!_active || sessionController == null ||
                sessionController.State != OSSessionState.ExplosionTelegraph)
            {
                return;
            }

            var requestId = _requestId;
            var consumeCount = _reservedCount;
            if (consumeCount <= 0 || bodyChain == null ||
                !bodyChain.AreReservedIdsCurrentTail(_reservedStableIds, consumeCount))
            {
                FinishAndResumeSelections(new OSExplosionResolution(
                    requestId,
                    OSResultCode.CancelledNoReservedSegment,
                    0,
                    0f,
                    0,
                    0));
                return;
            }

            var damagePerEnemy = consumeCount * EffectiveDamagePerSegment;
            ApplyDamageToUniqueEnemies(damagePerEnemy, out var hitCount, out var killCount);
            var consumeResult = bodyChain.ConsumeReservedTail(_reservedStableIds, consumeCount);
            if (!consumeResult.IsAccepted)
            {
                FinishAndResumeSelections(new OSExplosionResolution(
                    requestId,
                    consumeResult.Code,
                    0,
                    damagePerEnemy,
                    hitCount,
                    killCount));
                return;
            }

            playerHealth?.GrantExplosionInvulnerability(EffectiveHeadInvulnerability);
            LastHitCount = hitCount;
            LastKillCount = killCount;
            FinishAndResumeSelections(new OSExplosionResolution(
                requestId,
                OSResultCode.Accepted,
                consumeResult.Payload,
                damagePerEnemy,
                hitCount,
                killCount));
        }

        private void ApplyDamageToUniqueEnemies(float damage, out int hitCount, out int killCount)
        {
            hitCount = 0;
            killCount = 0;
            if (enemyRegistry == null || damage <= 0f)
            {
                return;
            }

            var index = 0;
            while (index < enemyRegistry.Count)
            {
                var enemy = enemyRegistry.GetAt(index);
                if (enemy == null || !enemy.IsRented || enemy.CurrentHealth <= 0f ||
                    !IsInsideReservedUnion(enemy.Position))
                {
                    index++;
                    continue;
                }

                var result = enemy.TryApplyDamage(damage);
                if (result.IsAccepted)
                {
                    hitCount++;
                    if (!enemy.IsRented || result.Payload <= 0f)
                    {
                        killCount++;
                    }
                }

                if (enemy.RegistryIndex == index)
                {
                    index++;
                }
            }
        }

        private bool IsInsideReservedUnion(Vector2 position)
        {
            var radiusSquared = EffectiveRadius * EffectiveRadius;
            for (var index = 0; index < _reservedCount; index++)
            {
                if ((position - _reservedCenters[index]).sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void FinishAndResumeSelections(OSExplosionResolution resolution)
        {
            ClearReservation();
            sessionController.CompleteExplosionTelegraph();
            ExplosionResolved?.Invoke(resolution);

            if (sessionController.State == OSSessionState.Combat)
            {
                bodyGrowth?.ResumeDeferredAfterCapacityChange();
            }

            if (sessionController.State == OSSessionState.Combat &&
                sessionController.PendingSelectionCount > 0)
            {
                sessionController.ProcessPendingSelection();
            }
        }

        private void HandleSegmentsRemoving(OSBodyRemovalEvent removal)
        {
            if (!_active || removal.Cause == OSBodyRemovalCause.Explosion || _reservedCount <= 0)
            {
                return;
            }

            var write = 0;
            for (var index = 0; index < _reservedCount; index++)
            {
                var removed = false;
                for (var removedIndex = 0; removedIndex < removal.RemovedStableIds.Count; removedIndex++)
                {
                    if (_reservedStableIds[index] == removal.RemovedStableIds[removedIndex])
                    {
                        removed = true;
                        break;
                    }
                }

                if (removed)
                {
                    continue;
                }

                _reservedStableIds[write] = _reservedStableIds[index];
                _reservedCenters[write] = _reservedCenters[index];
                write++;
            }

            Array.Clear(_reservedStableIds, write, _reservedCount - write);
            Array.Clear(_reservedCenters, write, _reservedCount - write);
            _reservedCount = write;
            ReservationChanged?.Invoke(CreateSnapshot());
        }

        private void HandleSessionExplosionRequested()
        {
            RequestExplosion();
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (!_active || current is not OSSessionState.Dead and not OSSessionState.Cleared and
                not OSSessionState.Boot and not OSSessionState.Result)
            {
                return;
            }

            CancelWithoutReturningToCombat(OSResultCode.CancelledNoReservedSegment);
        }

        private void CancelWithoutReturningToCombat(OSResultCode code)
        {
            if (!_active)
            {
                return;
            }

            var resolution = new OSExplosionResolution(_requestId, code, 0, 0f, 0, 0);
            ClearReservation();
            ExplosionResolved?.Invoke(resolution);
        }

        private OSExplosionSnapshot CreateSnapshot()
        {
            var ids = new int[_reservedCount];
            var centers = new Vector2[_reservedCount];
            Array.Copy(_reservedStableIds, ids, _reservedCount);
            Array.Copy(_reservedCenters, centers, _reservedCount);
            return new OSExplosionSnapshot(_requestId, _reservedCount, ids, centers);
        }

        private OSRuleResult<int> Reject(OSResultCode code, string reasonKey)
        {
            RequestRejected?.Invoke(code, reasonKey);
            return OSRuleResult<int>.Rejected(code, reasonKey, _reservedCount);
        }

        private void ClearReservation()
        {
            Array.Clear(_reservedStableIds, 0, _reservedStableIds.Length);
            Array.Clear(_reservedCenters, 0, _reservedCenters.Length);
            _reservedCount = 0;
            _initialBodyCount = 0;
            _telegraphRemaining = 0f;
            _active = false;
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.ExplosionRequested += HandleSessionExplosionRequested;
                sessionController.StateChanged += HandleSessionStateChanged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving += HandleSegmentsRemoving;
            }

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
                sessionController.ExplosionRequested -= HandleSessionExplosionRequested;
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving -= HandleSegmentsRemoving;
            }

            _subscribed = false;
        }
    }
}
