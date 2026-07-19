using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Connects fragment progress, Body-priority selection requests, and one tail append per confirmation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class OSBodyGrowthController : MonoBehaviour
    {
        private const int DefaultFragmentRequirement = 6;
        private const int DefaultTechnicalGuard = 64;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyBalanceData bodyBalance;

        private OSBodyGrowthProgress _progress;
        private bool _subscribed;

        public event Action<int, int> FragmentProgressChanged;
        public event Action<OSBodyRoleType, int> RoleConfirmed;

        public int FragmentProgress => Progress.FragmentProgress;
        public int FragmentRequirement => Progress.FragmentRequirement;
        public bool HasDeferredRequest => Progress.HasDeferredRequest;
        public int ActiveBodyCount => bodyChain != null ? bodyChain.ActiveCount : 0;
        public int PendingBodyRequestCount => sessionController != null
            ? sessionController.PendingBodySelectionCount
            : 0;

        private OSBodyGrowthProgress Progress => _progress ??= CreateProgress();

        private void Awake()
        {
            ResolveReferences();
            _progress = CreateProgress();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Applies collected body fragments and opens the first generated BodyRole request.
        /// </summary>
        public OSRuleResult<int> AddFragments(int amount)
        {
            if (sessionController == null || bodyChain == null ||
                sessionController.State is not OSSessionState.Combat and
                    not OSSessionState.BodyDash)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "body.fragment.invalid_state");
            }

            var result = Progress.AddFragments(
                amount,
                bodyChain.ActiveCount,
                sessionController.PendingBodySelectionCount);
            if (!result.IsAccepted)
            {
                return result;
            }

            var queued = QueueBodyRequests(result.Payload);
            FragmentProgressChanged?.Invoke(FragmentProgress, FragmentRequirement);
            return queued.IsAccepted
                ? OSRuleResult<int>.Accepted(queued.Payload, result.ReasonKey)
                : queued;
        }

        /// <summary>
        /// Appends exactly one selected role and only then completes the active Body request.
        /// </summary>
        public OSRuleResult<int> ConfirmRole(OSBodyRoleType role)
        {
            if (sessionController == null || bodyChain == null ||
                !sessionController.ActiveSelection.HasValue ||
                sessionController.ActiveSelection.Value.Kind is not OSSelectionKind.StartBody and
                    not OSSelectionKind.BodyRole)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "body.role.no_active_request");
            }

            var append = bodyChain.AppendSegment(role);
            if (!append.IsAccepted)
            {
                return append;
            }

            var completion = sessionController.CompleteActiveSelection();
            if (!completion.IsAccepted)
            {
                return OSRuleResult<int>.Rejected(
                    completion.Code,
                    completion.ReasonKey,
                    append.Payload);
            }

            RoleConfirmed?.Invoke(role, append.Payload);
            return append;
        }

        /// <summary>
        /// Reattaches severed body pickups with their original role without opening a new choice.
        /// A partial result is accepted when only part of a merged pickup fits under the technical guard.
        /// </summary>
        public OSRuleResult<int> ReclaimSegments(OSBodyRoleType role, int amount)
        {
            if (amount <= 0 || !Enum.IsDefined(typeof(OSBodyRoleType), role))
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.reclaim.invalid_request");
            }

            if (sessionController == null || bodyChain == null ||
                sessionController.State is not OSSessionState.Combat and
                    not OSSessionState.BodyDash)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "body.reclaim.invalid_state");
            }

            return bodyChain.AppendReclaimedSegments(role, amount);
        }

        /// <summary>
        /// Test and debug entry point for releasing tail capacity without Step 09 damage rules.
        /// </summary>
        public OSRuleResult<int> DebugRemoveTailSegments(int removeCount)
        {
            return bodyChain != null
                ? bodyChain.RemoveTailSegments(removeCount)
                : OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "body.remove_tail.chain_missing");
        }

        public OSRuleResult<int> ResumeDeferredAfterCapacityChange()
        {
            if (sessionController == null || bodyChain == null ||
                sessionController.State != OSSessionState.Combat || !Progress.HasDeferredRequest)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "body.deferred.resume.invalid_state",
                    0);
            }

            var resume = Progress.TryResumeDeferred(
                bodyChain.ActiveCount,
                sessionController.PendingBodySelectionCount);
            if (!resume.IsAccepted || resume.Payload <= 0)
            {
                return resume;
            }

            var queued = QueueBodyRequests(resume.Payload);
            FragmentProgressChanged?.Invoke(FragmentProgress, FragmentRequirement);
            return queued;
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            var baseRequirement = _testFragmentRequirement > 0
                ? _testFragmentRequirement
                : bodyBalance != null ? bodyBalance.FragmentRequirement : DefaultFragmentRequirement;
            var requirement = OSUpgradeMath.CalculateFragmentRequirement(
                baseRequirement,
                modifiers.FragmentRequirementMultiplier);
            Progress.SetFragmentRequirement(requirement);
            FragmentProgressChanged?.Invoke(FragmentProgress, FragmentRequirement);
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            OSBodyChain chain,
            int fragmentRequirement = DefaultFragmentRequirement,
            int technicalGuard = DefaultTechnicalGuard)
        {
            Unsubscribe();
            sessionController = session;
            bodyChain = chain;
            bodyBalance = null;
            _testFragmentRequirement = Mathf.Max(1, fragmentRequirement);
            _testTechnicalGuard = Mathf.Max(1, technicalGuard);
            _progress = CreateProgress();
            Subscribe();
        }

        private int _testFragmentRequirement;
        private int _testTechnicalGuard;

        private OSBodyGrowthProgress CreateProgress()
        {
            return new OSBodyGrowthProgress(
                _testFragmentRequirement > 0
                    ? _testFragmentRequirement
                    : bodyBalance != null ? bodyBalance.FragmentRequirement : DefaultFragmentRequirement,
                _testTechnicalGuard > 0
                    ? _testTechnicalGuard
                    : bodyBalance != null ? bodyBalance.TechnicalGuard : DefaultTechnicalGuard);
        }

        private OSRuleResult<int> QueueBodyRequests(int requestCount)
        {
            var queued = 0;
            for (var index = 0; index < requestCount; index++)
            {
                var result = sessionController.QueueSelection(OSSelectionKind.BodyRole);
                if (!result.IsAccepted)
                {
                    return OSRuleResult<int>.Rejected(result.Code, result.ReasonKey, queued);
                }

                queued++;
            }

            if (queued > 0 && sessionController.State == OSSessionState.Combat)
            {
                var process = sessionController.ProcessPendingSelection();
                if (!process.IsAccepted)
                {
                    return OSRuleResult<int>.Rejected(process.Code, process.ReasonKey, queued);
                }
            }

            return OSRuleResult<int>.Accepted(queued, "body.selection_requests.queued");
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged += HandleSegmentCountChanged;
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
                bodyChain.SegmentCountChanged -= HandleSegmentCountChanged;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandleSegmentCountChanged(int count)
        {
            if (!Progress.HasDeferredRequest || sessionController == null || bodyChain == null ||
                sessionController.State != OSSessionState.Combat)
            {
                return;
            }

            ResumeDeferredAfterCapacityChange();
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current != OSSessionState.Boot)
            {
                return;
            }

            Progress.Reset();
            FragmentProgressChanged?.Invoke(FragmentProgress, FragmentRequirement);
        }

        private void ResolveReferences()
        {
            sessionController ??= FindAnyObjectByType<OSGameSessionController>();
            bodyChain ??= FindAnyObjectByType<OSBodyChain>();
        }
    }
}
