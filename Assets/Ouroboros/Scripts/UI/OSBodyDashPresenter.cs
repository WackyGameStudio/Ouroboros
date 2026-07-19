using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSBodyDashPresenter : MonoBehaviour
    {
        [SerializeField] private OSBodyDashController bodyDashController;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        private float _feedbackRemaining;
        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
            RefreshStatus();
        }

        private void Update()
        {
            RefreshStatus();
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

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSBodyDashController controller,
            OSBodyChain chain,
            TMP_Text status,
            TMP_Text feedback)
        {
            Unsubscribe();
            bodyDashController = controller;
            bodyChain = chain;
            statusLabel = status;
            feedbackLabel = feedback;
            Subscribe();
            RefreshStatus();
        }

        private void HandleDashStarted(OSBodyDashSnapshot snapshot)
        {
            ShowFeedback($"BODY CONVERGE  |  DASH {snapshot.Distance:0.0}u", 1f);
            RefreshStatus();
        }

        private void HandleDashCompleted(OSBodyDashResolution resolution)
        {
            ShowFeedback(
                resolution.WasCancelled
                    ? "DASH CANCELLED"
                    : $"DASH COMPLETE  |  {resolution.TravelledDistance:0.0}u",
                1.2f);
            RefreshStatus();
        }

        private void HandleRejected(OSResultCode code, string reasonKey)
        {
            ShowFeedback(
                reasonKey == "body_dash.request.cooldown"
                    ? $"DASH COOLDOWN  {bodyDashController?.CooldownRemaining ?? 0f:0.0}s"
                    : "DASH INPUT IGNORED",
                0.8f);
        }

        private void RefreshStatus()
        {
            if (statusLabel == null || bodyDashController == null)
            {
                return;
            }

            if (bodyDashController.IsDashActive)
            {
                statusLabel.text =
                    $"DASHING  {bodyDashController.DashRemaining:0.0}s  |  BODY {bodyChain?.ActiveCount ?? 0} CONVERGING";
            }
            else if (bodyDashController.CooldownRemaining > 0f)
            {
                statusLabel.text = $"DASH COOLDOWN  {bodyDashController.CooldownRemaining:0.0}s";
            }
            else
            {
                statusLabel.text =
                    $"DASH READY [SPACE]  |  {bodyDashController.Distance:0.0}u / {bodyDashController.Duration:0.0}s";
            }
        }

        private void ShowFeedback(string message, float duration)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = message;
            _feedbackRemaining = Mathf.Max(0f, duration);
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled || bodyDashController == null)
            {
                return;
            }

            bodyDashController.DashStarted += HandleDashStarted;
            bodyDashController.DashCompleted += HandleDashCompleted;
            bodyDashController.RequestRejected += HandleRejected;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashStarted -= HandleDashStarted;
                bodyDashController.DashCompleted -= HandleDashCompleted;
                bodyDashController.RequestRejected -= HandleRejected;
            }

            _subscribed = false;
        }
    }
}
