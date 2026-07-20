using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSLevelUpPanel : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button[] candidateButtons = new Button[3];
        [SerializeField] private TMP_Text[] candidateLabels = new TMP_Text[3];

        private bool _subscribed;
        private int _displayedRequestId;
        private int _lastCommitFrame = -1;

        public int DisplayedCandidateCount => OSUpgradeRunState.CandidateCount;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
            SelectFirstButton();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SelectCandidate0() => TryConfirm(0);
        public void SelectCandidate1() => TryConfirm(1);
        public void SelectCandidate2() => TryConfirm(2);

        private void TryConfirm(int index)
        {
            if (levelUpController == null || sessionController == null ||
                !sessionController.ActiveSelection.HasValue ||
                sessionController.ActiveSelection.Value.Kind != OSSelectionKind.LevelUp ||
                sessionController.ActiveSelection.Value.RequestId != _displayedRequestId ||
                _lastCommitFrame == Time.frameCount)
            {
                return;
            }

            _lastCommitFrame = Time.frameCount;
            var result = levelUpController.ConfirmCandidate(index);
            if (!result.IsAccepted)
            {
                _lastCommitFrame = -1;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || levelUpController == null || !isActiveAndEnabled)
            {
                return;
            }

            levelUpController.CandidatesChanged += HandleCandidatesChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (levelUpController != null)
            {
                levelUpController.CandidatesChanged -= HandleCandidatesChanged;
            }

            _subscribed = false;
        }

        private void HandleCandidatesChanged(int requestId, OSUpgradeCandidate[] candidates)
        {
            _displayedRequestId = requestId;
            _lastCommitFrame = -1;
            Refresh();
            SelectFirstButton();
        }

        private void Refresh()
        {
            _displayedRequestId = levelUpController != null
                ? levelUpController.CandidateRequestId
                : 0;

            if (titleLabel != null)
            {
                var level = levelUpController != null ? levelUpController.Level : 1;
                titleLabel.text = $"LEVEL {level}  |  CHOOSE ONE UPGRADE\nCombat is paused until a card is confirmed";
            }

            for (var index = 0; index < OSUpgradeRunState.CandidateCount; index++)
            {
                var label = candidateLabels != null && index < candidateLabels.Length
                    ? candidateLabels[index]
                    : null;
                var button = candidateButtons != null && index < candidateButtons.Length
                    ? candidateButtons[index]
                    : null;
                var candidate = levelUpController != null
                    ? levelUpController.GetCandidate(index)
                    : default;
                var valid = _displayedRequestId > 0 && !string.IsNullOrWhiteSpace(candidate.Id);
                if (button != null)
                {
                    button.interactable = valid;
                }

                if (label == null)
                {
                    continue;
                }

                label.text = valid
                    ? BuildCandidateText(candidate)
                    : "NO CANDIDATE";
            }
        }

        private string BuildCandidateText(OSUpgradeCandidate candidate)
        {
            return $"{levelUpController.GetCategoryName(candidate.Category)}\n" +
                   $"{levelUpController.GetDisplayName(candidate.Id)}\n\n" +
                   levelUpController.GetEffectDescription(candidate) + "\n\n" +
                   "CURRENT → AFTER\n" +
                   levelUpController.GetComparison(candidate) + "\n" +
                   $"LEVEL {candidate.CurrentLevel} → {candidate.NextLevel} / MAX {candidate.MaxLevel}\n\n" +
                   "[ SELECT ]";
        }

        private void SelectFirstButton()
        {
            if (!isActiveAndEnabled || candidateButtons == null || candidateButtons.Length == 0 ||
                candidateButtons[0] == null || !candidateButtons[0].interactable ||
                EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(candidateButtons[0].gameObject);
        }
    }
}
