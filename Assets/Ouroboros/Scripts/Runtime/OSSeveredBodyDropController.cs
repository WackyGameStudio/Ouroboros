using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Converts cut body segments into role-preserving pooled pickups before their chain views deactivate.
    /// Pool saturation is held in four fixed role buckets and retried after collection frees capacity.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    public sealed class OSSeveredBodyDropController : MonoBehaviour
    {
        private const int RoleCount = 4;

        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSPickupSpawner pickupSpawner;
        [SerializeField] private OSGameSessionController sessionController;

        private readonly int[] _pendingAmounts = new int[RoleCount];
        private readonly Vector2[] _pendingPositions = new Vector2[RoleCount];
        private bool _subscribed;

        public int LastCutDropCount { get; private set; }
        public int LastCutSpawnedCount { get; private set; }
        public int PendingDropCount { get; private set; }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSBodyChain chain,
            OSPickupSpawner spawner,
            OSGameSessionController session)
        {
            Unsubscribe();
            bodyChain = chain;
            pickupSpawner = spawner;
            sessionController = session;
            ClearPending();
            Subscribe();
        }

        internal void RetryPendingForTesting()
        {
            RetryPendingDrops();
        }

        private void HandleSegmentsRemoving(OSBodyRemovalEvent removal)
        {
            if (removal.Cause != OSBodyRemovalCause.Cut || bodyChain == null || pickupSpawner == null)
            {
                return;
            }

            LastCutDropCount = removal.RemovedCount;
            LastCutSpawnedCount = 0;
            for (var chainIndex = removal.StartIndex; chainIndex < removal.PreviousCount; chainIndex++)
            {
                var segment = bodyChain.GetActiveSegment(chainIndex);
                var position = segment.View != null
                    ? (Vector2)segment.View.transform.position
                    : removal.HitPosition;
                var spawn = pickupSpawner.SpawnSeveredBody(segment.Role, 1, position);
                if (spawn.IsAccepted)
                {
                    LastCutSpawnedCount++;
                    continue;
                }

                QueuePending(segment.Role, position, 1);
            }
        }

        private void QueuePending(OSBodyRoleType role, Vector2 position, int amount)
        {
            var roleIndex = (int)role;
            if ((uint)roleIndex >= RoleCount || amount <= 0)
            {
                return;
            }

            if (_pendingAmounts[roleIndex] == 0)
            {
                _pendingPositions[roleIndex] = position;
            }

            var available = int.MaxValue - _pendingAmounts[roleIndex];
            var accepted = Mathf.Min(amount, available);
            _pendingAmounts[roleIndex] += accepted;
            PendingDropCount += accepted;
        }

        private void RetryPendingDrops()
        {
            if (pickupSpawner == null || sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            for (var roleIndex = 0; roleIndex < RoleCount; roleIndex++)
            {
                var amount = _pendingAmounts[roleIndex];
                if (amount <= 0)
                {
                    continue;
                }

                var spawn = pickupSpawner.SpawnSeveredBody(
                    (OSBodyRoleType)roleIndex,
                    amount,
                    _pendingPositions[roleIndex]);
                if (!spawn.IsAccepted)
                {
                    continue;
                }

                _pendingAmounts[roleIndex] = 0;
                _pendingPositions[roleIndex] = default;
                PendingDropCount -= amount;
            }
        }

        private void HandlePickupCollected(OSPickupType pickupType, int amount)
        {
            RetryPendingDrops();
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                ClearPending();
            }
        }

        private void ClearPending()
        {
            Array.Clear(_pendingAmounts, 0, _pendingAmounts.Length);
            Array.Clear(_pendingPositions, 0, _pendingPositions.Length);
            PendingDropCount = 0;
            LastCutDropCount = 0;
            LastCutSpawnedCount = 0;
        }

        private void ResolveReferences()
        {
            bodyChain ??= FindAnyObjectByType<OSBodyChain>();
            pickupSpawner ??= FindAnyObjectByType<OSPickupSpawner>();
            sessionController ??= FindAnyObjectByType<OSGameSessionController>();
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving += HandleSegmentsRemoving;
            }

            if (pickupSpawner != null)
            {
                pickupSpawner.PickupCollected += HandlePickupCollected;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleSessionStateChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving -= HandleSegmentsRemoving;
            }

            if (pickupSpawner != null)
            {
                pickupSpawner.PickupCollected -= HandlePickupCollected;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }
    }
}
