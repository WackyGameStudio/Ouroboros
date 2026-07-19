using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSBodyDashResolution
    {
        public OSBodyDashResolution(
            int requestId,
            OSResultCode code,
            float travelledDistance,
            int convergedBodyCount)
        {
            RequestId = requestId;
            Code = code;
            TravelledDistance = travelledDistance;
            ConvergedBodyCount = convergedBodyCount;
        }

        public int RequestId { get; }
        public OSResultCode Code { get; }
        public float TravelledDistance { get; }
        public int ConvergedBodyCount { get; }
        public bool WasCancelled => Code != OSResultCode.Accepted;
    }

    /// <summary>
    /// Replaces the former body-consuming blast with a collision-safe head dash and a temporary
    /// body convergence visual. Body count, roles, enemy health and player invulnerability are untouched.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class OSBodyDashController : MonoBehaviour
    {
        private const float DefaultDuration = 0.5f;
        private const float DefaultDistance = 4.5f;
        private const float DefaultCooldown = 2f;
        private const float DefaultRecoveryDuration = 0.25f;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyBalanceData bodyBalance;

        private bool _active;
        private bool _subscribed;
        private int _requestId;
        private int _convergedBodyCount;
        private float _cooldownRemaining;
        private OSUpgradeModifiers _upgradeModifiers = OSUpgradeModifiers.Default;

        private float _testDuration = -1f;
        private float _testDistance = -1f;
        private float _testCooldown = -1f;
        private float _testRecoveryDuration = -1f;

        public event Action<OSBodyDashSnapshot> DashStarted;
        public event Action<OSBodyDashResolution> DashCompleted;
        public event Action<OSResultCode, string> RequestRejected;

        public bool IsDashActive => _active;
        public float DashRemaining => playerController != null ? playerController.BodyDashRemaining : 0f;
        public float CooldownRemaining => _cooldownRemaining;
        public float Duration => EffectiveDuration;
        public float Distance => EffectiveDistance;
        public float Cooldown => EffectiveCooldown;
        public float RecoveryDuration => EffectiveRecoveryDuration;
        public bool IsReady => !_active && _cooldownRemaining <= 0f &&
                               sessionController != null && sessionController.State == OSSessionState.Combat;

        private void Awake()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            if (sessionController != null && sessionController.IsSimulationRunning &&
                _cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelActive(OSResultCode.RejectedState, false);
        }

        public OSRuleResult<int> RequestBodyDash()
        {
            if (_active || sessionController == null || sessionController.State != OSSessionState.Combat)
            {
                return Reject(OSResultCode.RejectedState, "body_dash.request.invalid_state");
            }

            if (_cooldownRemaining > 0f)
            {
                return Reject(OSResultCode.RejectedRequirement, "body_dash.request.cooldown");
            }

            if (playerController == null || bodyChain == null)
            {
                return Reject(OSResultCode.ConfigurationError, "body_dash.request.missing_dependency");
            }

            var direction = OSBodyDashMath.ResolveDirection(
                playerController.MoveInput,
                playerController.LastDirection);
            var stateResult = sessionController.BeginBodyDash();
            if (!stateResult.IsAccepted)
            {
                return Reject(stateResult.Code, stateResult.ReasonKey);
            }

            if (!playerController.TryStartBodyDash(direction, EffectiveDuration, EffectiveDistance))
            {
                sessionController.CompleteBodyDash();
                return Reject(OSResultCode.ConfigurationError, "body_dash.request.player_rejected");
            }

            _requestId++;
            _convergedBodyCount = bodyChain.ActiveCount;
            _cooldownRemaining = EffectiveCooldown;
            _active = true;
            bodyChain.BeginBodyConvergence(EffectiveDuration, EffectiveRecoveryDuration);
            DashStarted?.Invoke(new OSBodyDashSnapshot(
                _requestId,
                EffectiveDuration,
                EffectiveDistance,
                direction,
                _convergedBodyCount));
            return OSRuleResult<int>.Accepted(_convergedBodyCount, "body_dash.request.accepted");
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _upgradeModifiers = modifiers;
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            OSBodyChain chain,
            OSPlayerController player,
            OSBodyGrowthController growth = null,
            float duration = DefaultDuration,
            float distance = DefaultDistance,
            float cooldown = DefaultCooldown,
            float recoveryDuration = DefaultRecoveryDuration)
        {
            Unsubscribe();
            sessionController = session;
            bodyChain = chain;
            playerController = player;
            bodyGrowth = growth;
            bodyBalance = null;
            _testDuration = Mathf.Max(OSBodyDashMath.MinimumDuration, duration);
            _testDistance = Mathf.Max(OSBodyDashMath.MinimumDistance, distance);
            _testCooldown = Mathf.Max(OSBodyDashMath.MinimumCooldown, cooldown);
            _testRecoveryDuration = Mathf.Max(0f, recoveryDuration);
            _active = false;
            _cooldownRemaining = 0f;
            Subscribe();
        }

        internal void SimulateCooldownForTesting(float deltaTime)
        {
            if (float.IsFinite(deltaTime) && deltaTime > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            }
        }

        private float EffectiveDuration => _testDuration >= 0f
            ? _testDuration
            : bodyBalance != null ? bodyBalance.BodyDash.Duration : DefaultDuration;
        private float EffectiveDistance => OSBodyDashMath.CalculateDistance(
            _testDistance >= 0f
                ? _testDistance
                : bodyBalance != null ? bodyBalance.BodyDash.Distance : DefaultDistance,
            _upgradeModifiers.DashDistanceMultiplier);
        private float EffectiveCooldown => OSBodyDashMath.CalculateCooldown(
            _testCooldown >= 0f
                ? _testCooldown
                : bodyBalance != null ? bodyBalance.BodyDash.Cooldown : DefaultCooldown,
            _upgradeModifiers.DashCooldownMultiplier);
        private float EffectiveRecoveryDuration => OSBodyDashMath.CalculateRecoveryDuration(
            _testRecoveryDuration >= 0f
                ? _testRecoveryDuration
                : bodyBalance != null
                    ? bodyBalance.BodyDash.BodyRecoveryDuration
                    : DefaultRecoveryDuration,
            _upgradeModifiers.DashRecoveryDurationDelta);

        private void HandlePlayerBodyDashCompleted(float travelledDistance)
        {
            if (!_active)
            {
                return;
            }

            var requestId = _requestId;
            var bodyCount = _convergedBodyCount;
            _active = false;
            sessionController?.CompleteBodyDash();
            DashCompleted?.Invoke(new OSBodyDashResolution(
                requestId,
                OSResultCode.Accepted,
                travelledDistance,
                bodyCount));

            if (sessionController != null && sessionController.State == OSSessionState.Combat)
            {
                bodyGrowth?.ResumeDeferredAfterCapacityChange();
                sessionController.ProcessPendingSelection();
            }
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                _cooldownRemaining = 0f;
                CancelActive(OSResultCode.RejectedState, false);
            }
            else if (_active && current != OSSessionState.BodyDash)
            {
                CancelActive(OSResultCode.RejectedState, true);
            }
        }

        private void CancelActive(OSResultCode code, bool publish)
        {
            if (!_active)
            {
                return;
            }

            var requestId = _requestId;
            var bodyCount = _convergedBodyCount;
            _active = false;
            playerController?.CancelBodyDash();
            bodyChain?.CancelBodyConvergence();
            if (publish)
            {
                DashCompleted?.Invoke(new OSBodyDashResolution(requestId, code, 0f, bodyCount));
            }
        }

        private OSRuleResult<int> Reject(OSResultCode code, string reasonKey)
        {
            RequestRejected?.Invoke(code, reasonKey);
            return OSRuleResult<int>.Rejected(code, reasonKey, 0);
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.BodyDashCompleted += HandlePlayerBodyDashCompleted;
            }

            if (sessionController != null)
            {
                sessionController.BodyDashRequested += HandleBodyDashRequested;
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

            if (playerController != null)
            {
                playerController.BodyDashCompleted -= HandlePlayerBodyDashCompleted;
            }

            if (sessionController != null)
            {
                sessionController.BodyDashRequested -= HandleBodyDashRequested;
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandleBodyDashRequested()
        {
            RequestBodyDash();
        }
    }
}
