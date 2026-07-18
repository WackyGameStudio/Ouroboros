using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSLevelProgressPresenter : MonoBehaviour
    {
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private TMP_Text resultLabel;

        private bool _subscribed;
        private float _feedbackRemaining;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_feedbackRemaining <= 0f)
            {
                return;
            }

            _feedbackRemaining = Mathf.Max(0f, _feedbackRemaining - Time.unscaledDeltaTime);
            if (_feedbackRemaining <= 0f && feedbackLabel != null)
            {
                feedbackLabel.text = string.Empty;
            }
        }

        public void Configure(
            OSLevelUpController controller,
            TMP_Text progress,
            TMP_Text feedback,
            TMP_Text result = null)
        {
            Unsubscribe();
            levelUpController = controller;
            progressLabel = progress;
            feedbackLabel = feedback;
            resultLabel = result;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (_subscribed || levelUpController == null || !isActiveAndEnabled)
            {
                return;
            }

            levelUpController.ExperienceChanged += HandleExperienceChanged;
            levelUpController.UpgradeApplied += HandleUpgradeApplied;
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
                levelUpController.ExperienceChanged -= HandleExperienceChanged;
                levelUpController.UpgradeApplied -= HandleUpgradeApplied;
            }

            _subscribed = false;
        }

        private void HandleExperienceChanged(int level, float current, int required)
        {
            Refresh();
        }

        private void HandleUpgradeApplied(OSUpgradeCandidate candidate, OSUpgradeModifiers modifiers)
        {
            Refresh();
            if (feedbackLabel != null)
            {
                feedbackLabel.text = $"UPGRADE APPLIED  |  {levelUpController.GetDisplayName(candidate.Id)} LV {candidate.NextLevel}";
                _feedbackRemaining = 1.5f;
            }
        }

        private void Refresh()
        {
            if (progressLabel == null || levelUpController == null)
            {
                return;
            }

            progressLabel.text =
                $"LEVEL {levelUpController.Level}  |  XP {levelUpController.CurrentExperience:0.0}/{levelUpController.RequiredExperience}  |  UPGRADES {levelUpController.AppliedUpgradeCount}";
            if (resultLabel != null)
            {
                resultLabel.text =
                    "SESSION RESULT\n" +
                    $"LEVEL {levelUpController.Level}  |  UPGRADES {levelUpController.AppliedUpgradeCount}\n" +
                    $"BUILD  {levelUpController.GetAppliedUpgradeSummary()}\n" +
                    $"RUN SEED {levelUpController.RunSeed}\n" +
                    "[ENTER] CONTINUE / RESTART";
            }
        }
    }
}
