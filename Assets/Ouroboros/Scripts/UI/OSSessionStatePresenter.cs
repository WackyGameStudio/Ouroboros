using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSSessionStatePresenter : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private GameObject combatHud;
        [SerializeField] private GameObject bodyRoleSelectionPanel;
        [SerializeField] private GameObject levelUpPanel;
        [SerializeField] private GameObject resultPanel;

        private bool _isSubscribed;

        private void OnEnable()
        {
            Subscribe();
            RefreshState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (sessionController == null)
            {
                return;
            }

            if (timerLabel != null)
            {
                var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(sessionController.SessionElapsedTime));
                timerLabel.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }

            UpdateStateLabel();
        }

        /// <summary>
        /// Assigns scene references and refreshes the mutually exclusive panel state.
        /// </summary>
        public void Configure(
            OSGameSessionController controller,
            TMP_Text timer,
            TMP_Text state,
            GameObject combat,
            GameObject bodyPanel,
            GameObject levelPanel,
            GameObject result)
        {
            Unsubscribe();
            sessionController = controller;
            timerLabel = timer;
            stateLabel = state;
            combatHud = combat;
            bodyRoleSelectionPanel = bodyPanel;
            levelUpPanel = levelPanel;
            resultPanel = result;
            Subscribe();
            RefreshState();
        }

        public void CompleteSelection()
        {
            sessionController?.CompleteActiveSelection();
        }

        public void ConfirmResult()
        {
            sessionController?.ConfirmResult();
        }

        public void RestartSession()
        {
            sessionController?.RestartSession();
        }

        public void QueueBodySelection()
        {
            if (sessionController?.QueueSelection(OSSelectionKind.BodyRole).IsAccepted == true)
            {
                sessionController.ProcessPendingSelection();
            }
        }

        public void QueueLevelUpSelection()
        {
            if (sessionController?.QueueSelection(OSSelectionKind.LevelUp).IsAccepted == true)
            {
                sessionController.ProcessPendingSelection();
            }
        }

        public void DebugRequestDeath()
        {
            sessionController?.RequestDeath();
        }

        private void Subscribe()
        {
            if (_isSubscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleStateChanged;
            sessionController.ActiveSelectionChanged += HandleActiveSelectionChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || sessionController == null)
            {
                _isSubscribed = false;
                return;
            }

            sessionController.StateChanged -= HandleStateChanged;
            sessionController.ActiveSelectionChanged -= HandleActiveSelectionChanged;
            _isSubscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            RefreshState();
        }

        private void HandleActiveSelectionChanged(OSSelectionRequest? request)
        {
            RefreshState();
        }

        private void RefreshState()
        {
            if (sessionController == null)
            {
                return;
            }

            var state = sessionController.State;
            SetActive(combatHud, state is OSSessionState.Combat or OSSessionState.ExplosionTelegraph);
            SetActive(
                bodyRoleSelectionPanel,
                state is OSSessionState.StartBodySelection or OSSessionState.BodyRoleSelection);
            SetActive(levelUpPanel, state == OSSessionState.LevelUpSelection);
            SetActive(resultPanel, state is OSSessionState.Dead or OSSessionState.Cleared or OSSessionState.Result);
            UpdateStateLabel();
        }

        private void UpdateStateLabel()
        {
            if (stateLabel == null || sessionController == null)
            {
                return;
            }

            var request = sessionController.ActiveSelection;
            var requestText = request.HasValue
                ? $"  REQUEST {request.Value.RequestId}: {request.Value.Kind}"
                : string.Empty;
            stateLabel.text = $"STEP 03  |  {sessionController.State}{requestText}";
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
