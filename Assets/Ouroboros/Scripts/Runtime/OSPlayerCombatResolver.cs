using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Resolves the normalized hostile damage batch after enemy FixedUpdate logic.
    /// Head damage is always finalized before the nearest-to-head body cut candidate.
    /// </summary>
    [DefaultExecutionOrder(8000)]
    [DisallowMultipleComponent]
    public sealed class OSPlayerCombatResolver : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSShieldBodyRole shieldBodyRole;

        private readonly OSCombatEventBuffer _eventBuffer = new();
        private readonly OSDamageEvent[] _drainBuffer = new OSDamageEvent[OSCombatEventBuffer.DefaultCapacity];
        private int _combatTick;
        private bool _subscribed;

        public event Action<OSDamageEvent, OSResultCode> DamageResolved;

        public int PendingDamageCount => _eventBuffer.Count;
        public int CombatTick => _combatTick;

        private void Awake()
        {
            _eventBuffer.BeginTick(_combatTick);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            ResolvePendingDamage();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _eventBuffer.Clear();
        }

        public OSRuleResult<int> EnqueueDamage(OSDamageEvent damageEvent)
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "player_combat.enqueue.invalid_state",
                    _eventBuffer.Count);
            }

            return _eventBuffer.EnqueueDamage(damageEvent);
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            OSPlayerHealth health,
            OSBodyChain chain,
            OSShieldBodyRole shield = null)
        {
            Unsubscribe();
            sessionController = session;
            playerHealth = health;
            bodyChain = chain;
            shieldBodyRole = shield;
            ResetBuffer();
            Subscribe();
        }

        internal void ProcessPendingForTesting()
        {
            ResolvePendingDamage();
        }

        private void ResolvePendingDamage()
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                ResetBuffer();
                return;
            }

            var count = _eventBuffer.DrainTo(_drainBuffer);
            if (count > 0 && playerHealth != null)
            {
                ResolveHeadDamage(count);
                if (playerHealth.CurrentHealth > 0f &&
                    (sessionController == null || sessionController.IsSimulationRunning))
                {
                    ResolveBodyCut(count);
                }
            }

            Array.Clear(_drainBuffer, 0, count);
            _eventBuffer.BeginTick(++_combatTick);
        }

        private void ResolveHeadDamage(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var damageEvent = _drainBuffer[index];
                if (damageEvent.TargetKind != OSTargetKind.PlayerHead)
                {
                    continue;
                }

                if (!playerHealth.IsInvulnerable && shieldBodyRole != null &&
                    shieldBodyRole.TryBlockDamage(damageEvent).IsAccepted)
                {
                    DamageResolved?.Invoke(damageEvent, OSResultCode.BlockedByShield);
                    continue;
                }

                var result = playerHealth.TryApplyHeadDamage(damageEvent);
                DamageResolved?.Invoke(damageEvent, result.Code);
                if (playerHealth.CurrentHealth <= 0f ||
                    sessionController != null && !sessionController.IsSimulationRunning)
                {
                    return;
                }
            }
        }

        private void ResolveBodyCut(int count)
        {
            if (bodyChain == null || bodyChain.ActiveCount <= 0)
            {
                return;
            }

            var selectedIndex = int.MaxValue;
            var selectedEvent = default(OSDamageEvent);
            for (var index = 0; index < count; index++)
            {
                var damageEvent = _drainBuffer[index];
                if (damageEvent.TargetKind != OSTargetKind.PlayerBody)
                {
                    continue;
                }

                var chainIndex = bodyChain.FindChainIndexByStableId(damageEvent.TargetRuntimeId);
                if (chainIndex < 0 || chainIndex >= selectedIndex)
                {
                    continue;
                }

                selectedIndex = chainIndex;
                selectedEvent = damageEvent;
            }

            if (selectedIndex == int.MaxValue)
            {
                return;
            }

            if (bodyChain.CutGuardRemaining <= 0f && shieldBodyRole != null &&
                shieldBodyRole.TryBlockDamage(selectedEvent).IsAccepted)
            {
                DamageResolved?.Invoke(selectedEvent, OSResultCode.BlockedByShield);
                return;
            }

            var result = bodyChain.TryCutFrom(selectedIndex, selectedEvent.HitPosition);
            DamageResolved?.Invoke(selectedEvent, result.Code);
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleStateChanged;
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
                sessionController.StateChanged -= HandleStateChanged;
            }

            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current is OSSessionState.Dead or OSSessionState.Cleared ||
                previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                ResetBuffer();
            }
        }

        private void ResetBuffer()
        {
            _eventBuffer.Clear();
            _combatTick = 0;
            _eventBuffer.BeginTick(_combatTick);
        }
    }
}
