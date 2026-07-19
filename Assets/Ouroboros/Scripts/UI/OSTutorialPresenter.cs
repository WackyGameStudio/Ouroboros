using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSTutorialPresenter : MonoBehaviour
    {
        private static bool s_tutorialStartedThisApplication;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyDashController bodyDashController;
        [SerializeField] private OSRunSummaryController runSummary;
        [SerializeField] private TMP_Text tutorialLabel;

        private readonly OSTutorialProgress _progress = new();
        private bool _subscribed;
        private OSTutorialStage _displayedStage = (OSTutorialStage)(-1);

        public OSTutorialProgress Progress => _progress;
        public OSTutorialStage CurrentStage => _progress.Stage;
        public string CurrentMessage => tutorialLabel != null ? tutorialLabel.text : string.Empty;
        public bool IsTutorialVisible => tutorialLabel != null && tutorialLabel.gameObject.activeSelf;

        public static void ResetApplicationStateForTesting()
        {
            s_tutorialStartedThisApplication = false;
        }

        private void OnEnable()
        {
            Subscribe();
            if (sessionController != null && sessionController.State == OSSessionState.Combat)
            {
                TryBeginTutorial();
            }

            Refresh();
        }

        private void Update()
        {
            _progress.Advance(
                Mathf.Max(0f, Time.unscaledDeltaTime),
                playerController != null && playerController.MoveInput.sqrMagnitude > 0.01f);
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSGameSessionController session,
            OSPlayerController player,
            OSBodyGrowthController growth,
            OSBodyChain chain,
            OSBodyDashController bodyDash,
            OSRunSummaryController summary,
            TMP_Text label)
        {
            Unsubscribe();
            sessionController = session;
            playerController = player;
            bodyGrowth = growth;
            bodyChain = chain;
            bodyDashController = bodyDash;
            runSummary = summary;
            tutorialLabel = label;
            Subscribe();
            Refresh();
        }

        public void AdvanceForTesting(float unscaledDeltaTime, bool isMoving)
        {
            _progress.Advance(unscaledDeltaTime, isMoving);
            Refresh();
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Combat)
            {
                TryBeginTutorial();
            }

            Refresh();
        }

        private void TryBeginTutorial()
        {
            if (!s_tutorialStartedThisApplication && _progress.BeginFirstSession())
            {
                s_tutorialStartedThisApplication = true;
            }
        }

        private void HandleEnemyDefeated(int totalKills)
        {
            _progress.NotifyEnemyDefeated();
            Refresh();
        }

        private void HandleFragmentProgressChanged(int progress, int required)
        {
            if (progress > 0)
            {
                _progress.NotifyFragmentCollected();
            }

            Refresh();
        }

        private void HandleRoleConfirmed(OSBodyRoleType role, int stableId)
        {
            _progress.NotifyRoleConfirmed();
            Refresh();
        }

        private void HandleBodyCountChanged(int count)
        {
            _progress.NotifyBodyCount(count);
            Refresh();
        }

        private void HandleBodyDashResolved(OSBodyDashResolution resolution)
        {
            if (!resolution.WasCancelled)
            {
                _progress.NotifyBodyDashResolved();
            }

            Refresh();
        }

        private void HandleBodyCut(OSBodyRemovalEvent removal)
        {
            _progress.NotifyBodyCut();
            Refresh();
        }

        private void Refresh()
        {
            if (tutorialLabel == null || _displayedStage == _progress.Stage)
            {
                return;
            }

            _displayedStage = _progress.Stage;
            tutorialLabel.text = _progress.Stage switch
            {
                OSTutorialStage.Movement => "MOVE  [WASD / ARROWS]  |  Keep moving for 1 second",
                OSTutorialStage.AutoAttack => "AUTO ATTACK  [NO BUTTON]  |  Enter range and defeat one enemy",
                OSTutorialStage.BodyGrowth => "BODY FRAGMENTS  12 -> +1 ROLE SEGMENT  |  Choose a tail role",
                OSTutorialStage.BodyDash => "BODY DASH  [SPACE]  |  Tail converges while the head surges forward",
                OSTutorialStage.CutDifference => "CORE HIT = HP LOSS  |  BODY HIT = CUT FROM IMPACT TO TAIL",
                _ => string.Empty
            };
            tutorialLabel.gameObject.SetActive(_progress.IsVisible);
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleStateChanged;
            }

            if (runSummary != null)
            {
                runSummary.EnemyDefeated += HandleEnemyDefeated;
            }

            if (bodyGrowth != null)
            {
                bodyGrowth.FragmentProgressChanged += HandleFragmentProgressChanged;
                bodyGrowth.RoleConfirmed += HandleRoleConfirmed;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged += HandleBodyCountChanged;
                bodyChain.SegmentsCut += HandleBodyCut;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashCompleted += HandleBodyDashResolved;
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
                sessionController.StateChanged -= HandleStateChanged;
            }

            if (runSummary != null)
            {
                runSummary.EnemyDefeated -= HandleEnemyDefeated;
            }

            if (bodyGrowth != null)
            {
                bodyGrowth.FragmentProgressChanged -= HandleFragmentProgressChanged;
                bodyGrowth.RoleConfirmed -= HandleRoleConfirmed;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged -= HandleBodyCountChanged;
                bodyChain.SegmentsCut -= HandleBodyCut;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashCompleted -= HandleBodyDashResolved;
            }

            _subscribed = false;
        }
    }
}
