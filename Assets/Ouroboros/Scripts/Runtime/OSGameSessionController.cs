using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-8000)]
    [DisallowMultipleComponent]
    public sealed class OSGameSessionController : MonoBehaviour
    {
        [SerializeField] private OSInputRouter inputRouter;
        [SerializeField] private bool autoStartSession = true;

        private readonly OSSelectionQueue _selectionQueue = new();
        private OSSelectionRequest? _activeSelection;
        private int _nextRequestId = 1;
        private int _nextCreatedTick;
        private bool _routerSubscribed;

        public event Action<OSSessionState, OSSessionState> StateChanged;
        public event Action<OSSelectionRequest?> ActiveSelectionChanged;
        public event Action BodyDashRequested;

        public OSSessionState State { get; private set; } = OSSessionState.Boot;
        public OSSessionResultKind ResultKind { get; private set; }
        public float SessionElapsedTime { get; private set; }
        public float UiElapsedTime { get; private set; }
        public OSSelectionRequest? ActiveSelection => _activeSelection;
        public int PendingSelectionCount => _selectionQueue.Count;
        public int PendingBodySelectionCount => _selectionQueue.BodyCount +
                                                (_activeSelection.HasValue &&
                                                 _activeSelection.Value.Kind is OSSelectionKind.StartBody or
                                                     OSSelectionKind.BodyRole
                                                    ? 1
                                                    : 0);
        public bool IsSimulationRunning => State is OSSessionState.Combat or OSSessionState.BodyDash;
        public bool IsPlayerInputAllowed => State is OSSessionState.Combat or OSSessionState.BodyDash;

        private void OnEnable()
        {
            SubscribeRouter();
            ApplyStatePolicy();
        }

        private void Start()
        {
            if (autoStartSession && State == OSSessionState.Boot)
            {
                BeginSession();
            }
        }

        private void Update()
        {
            var unscaledDelta = Mathf.Max(0f, Time.unscaledDeltaTime);
            UiElapsedTime += unscaledDelta;
            if (IsSimulationRunning)
            {
                SessionElapsedTime += Mathf.Max(0f, Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            UnsubscribeRouter();
            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
            }

            inputRouter?.SetInputMode(OSInputMode.None);
        }

        /// <summary>
        /// Replaces the input router and controls whether Start automatically begins a session.
        /// </summary>
        public void Configure(OSInputRouter router, bool startAutomatically)
        {
            UnsubscribeRouter();
            inputRouter = router;
            autoStartSession = startAutomatically;
            SubscribeRouter();
            ApplyStatePolicy();
        }

        /// <summary>
        /// Resets all session-owned state and starts the two mock StartBody requests.
        /// </summary>
        public OSRuleResult<OSSessionState> BeginSession()
        {
            if (State is not OSSessionState.Boot and not OSSessionState.Result)
            {
                return RejectState("session.begin.invalid_state");
            }

            if (State == OSSessionState.Result)
            {
                var bootResult = SetState(OSSessionState.Boot);
                if (!bootResult.IsAccepted)
                {
                    return bootResult;
                }
            }

            _selectionQueue.Clear();
            _activeSelection = null;
            _nextRequestId = 1;
            _nextCreatedTick = 0;
            SessionElapsedTime = 0f;
            UiElapsedTime = 0f;
            ResultKind = OSSessionResultKind.None;
            ActiveSelectionChanged?.Invoke(null);

            QueueInternal(OSSelectionKind.StartBody);
            QueueInternal(OSSelectionKind.StartBody);
            return ActivateNextSelectionOrCombat();
        }

        /// <summary>
        /// Queues a BodyRole or LevelUp selection without changing state until ProcessPendingSelection is called.
        /// </summary>
        public OSRuleResult<OSSelectionRequest> QueueSelection(OSSelectionKind kind)
        {
            if (State is not OSSessionState.Combat and not OSSessionState.BodyDash)
            {
                return OSRuleResult<OSSelectionRequest>.Rejected(
                    OSResultCode.RejectedState,
                    "selection.queue.invalid_state");
            }

            if (kind is not OSSelectionKind.BodyRole and not OSSelectionKind.LevelUp)
            {
                return OSRuleResult<OSSelectionRequest>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "selection.queue.invalid_kind");
            }

            return QueueInternal(kind);
        }

        /// <summary>
        /// Enters the next queued selection after all same-tick requests have been collected.
        /// </summary>
        public OSRuleResult<OSSessionState> ProcessPendingSelection()
        {
            if (State != OSSessionState.Combat || _activeSelection.HasValue)
            {
                return RejectState("selection.process.invalid_state");
            }

            if (_selectionQueue.Count == 0)
            {
                return OSRuleResult<OSSessionState>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "selection.process.empty",
                    State);
            }

            return ActivateNextSelectionOrCombat();
        }

        /// <summary>
        /// Completes exactly one active selection and advances to the next priority request or Combat.
        /// </summary>
        public OSRuleResult<OSSessionState> CompleteActiveSelection()
        {
            if (!_activeSelection.HasValue || !IsSelectionState(State))
            {
                return RejectState("selection.complete.invalid_state");
            }

            _activeSelection = null;
            ActiveSelectionChanged?.Invoke(null);
            return ActivateNextSelectionOrCombat();
        }

        /// <summary>
        /// Accepts a body-dash request only while Player input is valid.
        /// </summary>
        public OSRuleResult<OSSessionState> TryRequestBodyDash()
        {
            if (State != OSSessionState.Combat)
            {
                return RejectState("body_dash.invalid_state");
            }

            BodyDashRequested?.Invoke();
            return OSRuleResult<OSSessionState>.Accepted(State, "body_dash.requested");
        }

        /// <summary>
        /// Enters the simulation-running body-dash state.
        /// </summary>
        public OSRuleResult<OSSessionState> BeginBodyDash()
        {
            return State == OSSessionState.Combat
                ? SetState(OSSessionState.BodyDash)
                : RejectState("body_dash.begin.invalid_state");
        }

        /// <summary>
        /// Returns from body dash to Combat before queued selections are processed.
        /// </summary>
        public OSRuleResult<OSSessionState> CompleteBodyDash()
        {
            return State == OSSessionState.BodyDash
                ? SetState(OSSessionState.Combat)
                : RejectState("body_dash.complete.invalid_state");
        }

        /// <summary>
        /// Stops simulation immediately and enters Dead.
        /// </summary>
        public OSRuleResult<OSSessionState> RequestDeath()
        {
            if (State is not OSSessionState.Combat and not OSSessionState.BodyDash)
            {
                return RejectState("session.death.invalid_state");
            }

            ResultKind = OSSessionResultKind.PlayerDefeated;
            CancelPendingSelections();
            return SetState(OSSessionState.Dead);
        }

        /// <summary>
        /// Stops simulation immediately and enters Cleared.
        /// </summary>
        public OSRuleResult<OSSessionState> RequestClear()
        {
            if (State is not OSSessionState.Combat and not OSSessionState.BodyDash)
            {
                return RejectState("session.clear.invalid_state");
            }

            ResultKind = OSSessionResultKind.BossDefeated;
            CancelPendingSelections();
            return SetState(OSSessionState.Cleared);
        }

        /// <summary>
        /// Ends the boss encounter as a timeout failure while preserving a distinct result reason.
        /// </summary>
        public OSRuleResult<OSSessionState> RequestBossTimeout()
        {
            if (State is not OSSessionState.Combat and not OSSessionState.BodyDash)
            {
                return RejectState("session.boss_timeout.invalid_state");
            }

            ResultKind = OSSessionResultKind.BossTimeout;
            CancelPendingSelections();
            return SetState(OSSessionState.Dead);
        }

        /// <summary>
        /// Confirms a terminal state and opens Result.
        /// </summary>
        public OSRuleResult<OSSessionState> ConfirmResult()
        {
            return State is OSSessionState.Dead or OSSessionState.Cleared
                ? SetState(OSSessionState.Result)
                : RejectState("session.result.invalid_state");
        }

        /// <summary>
        /// Starts a clean session from Result through the normal StartBodySelection path.
        /// </summary>
        public OSRuleResult<OSSessionState> RestartSession()
        {
            return State == OSSessionState.Result
                ? BeginSession()
                : RejectState("session.restart.invalid_state");
        }

        private OSRuleResult<OSSelectionRequest> QueueInternal(OSSelectionKind kind)
        {
            var request = new OSSelectionRequest(_nextRequestId++, kind, _nextCreatedTick++);
            return _selectionQueue.Enqueue(request);
        }

        private OSRuleResult<OSSessionState> ActivateNextSelectionOrCombat()
        {
            if (!_selectionQueue.TryDequeue(out var request))
            {
                _activeSelection = null;
                ActiveSelectionChanged?.Invoke(null);
                return SetState(OSSessionState.Combat);
            }

            _activeSelection = request;
            var targetState = request.Kind switch
            {
                OSSelectionKind.StartBody => OSSessionState.StartBodySelection,
                OSSelectionKind.BodyRole => OSSessionState.BodyRoleSelection,
                OSSelectionKind.LevelUp => OSSessionState.LevelUpSelection,
                _ => State
            };

            var result = SetState(targetState);
            if (result.IsAccepted)
            {
                ActiveSelectionChanged?.Invoke(request);
            }

            return result;
        }

        private OSRuleResult<OSSessionState> SetState(OSSessionState nextState)
        {
            if (State == nextState)
            {
                return OSRuleResult<OSSessionState>.Accepted(State, "session.state_unchanged");
            }

            if (!CanTransition(State, nextState))
            {
                return RejectState("session.transition.rejected");
            }

            var previousState = State;
            State = nextState;
            ApplyStatePolicy();
            StateChanged?.Invoke(previousState, nextState);
            return OSRuleResult<OSSessionState>.Accepted(nextState, "session.transition.accepted");
        }

        private void ApplyStatePolicy()
        {
            if (Application.isPlaying)
            {
                Time.timeScale = IsSimulationRunning ? 1f : 0f;
            }

            inputRouter?.SetForState(State);
        }

        private void CancelPendingSelections()
        {
            _selectionQueue.Clear();
            _activeSelection = null;
            ActiveSelectionChanged?.Invoke(null);
        }

        private void SubscribeRouter()
        {
            if (_routerSubscribed || inputRouter == null || !isActiveAndEnabled)
            {
                return;
            }

            inputRouter.BodyDashRequested += HandleBodyDashRequested;
            inputRouter.SubmitRequested += HandleSubmitRequested;
            _routerSubscribed = true;
        }

        private void UnsubscribeRouter()
        {
            if (!_routerSubscribed || inputRouter == null)
            {
                _routerSubscribed = false;
                return;
            }

            inputRouter.BodyDashRequested -= HandleBodyDashRequested;
            inputRouter.SubmitRequested -= HandleSubmitRequested;
            _routerSubscribed = false;
        }

        private void HandleBodyDashRequested()
        {
            TryRequestBodyDash();
        }

        private void HandleSubmitRequested()
        {
            if (State is OSSessionState.Dead or OSSessionState.Cleared)
            {
                ConfirmResult();
            }
            else if (State == OSSessionState.Result)
            {
                RestartSession();
            }
        }

        private OSRuleResult<OSSessionState> RejectState(string reasonKey)
        {
            return OSRuleResult<OSSessionState>.Rejected(OSResultCode.RejectedState, reasonKey, State);
        }

        private static bool IsSelectionState(OSSessionState state)
        {
            return state is OSSessionState.StartBodySelection or OSSessionState.BodyRoleSelection or
                OSSessionState.LevelUpSelection;
        }

        private static bool CanTransition(OSSessionState from, OSSessionState to)
        {
            return from switch
            {
                OSSessionState.Boot => to == OSSessionState.StartBodySelection,
                OSSessionState.StartBodySelection => to is OSSessionState.StartBodySelection or OSSessionState.Combat,
                OSSessionState.Combat => to is OSSessionState.BodyDash or
                    OSSessionState.BodyRoleSelection or OSSessionState.LevelUpSelection or
                    OSSessionState.Dead or OSSessionState.Cleared,
                OSSessionState.BodyDash => to is OSSessionState.Combat or
                    OSSessionState.Dead or OSSessionState.Cleared,
                OSSessionState.BodyRoleSelection => to is OSSessionState.BodyRoleSelection or
                    OSSessionState.LevelUpSelection or OSSessionState.Combat,
                OSSessionState.LevelUpSelection => to is OSSessionState.BodyRoleSelection or
                    OSSessionState.LevelUpSelection or OSSessionState.Combat,
                OSSessionState.Dead => to == OSSessionState.Result,
                OSSessionState.Cleared => to == OSSessionState.Result,
                OSSessionState.Result => to == OSSessionState.Boot,
                _ => false
            };
        }
    }
}
