using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSPlayerHealthPresenter : MonoBehaviour
    {
        private const float FeedbackDuration = 1.5f;

        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private Image healthFill;

        private float _feedbackRemaining;
        private bool _subscribed;

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
            if (_feedbackRemaining > 0f)
            {
                _feedbackRemaining = Mathf.Max(0f, _feedbackRemaining - Time.unscaledDeltaTime);
                if (_feedbackRemaining <= 0f && feedbackLabel != null)
                {
                    feedbackLabel.text = string.Empty;
                }
            }

            Refresh();
        }

        public void Configure(
            OSPlayerHealth health,
            OSBodyChain chain,
            TMP_Text hpLabel,
            TMP_Text cutFeedbackLabel,
            Image fill)
        {
            Unsubscribe();
            playerHealth = health;
            bodyChain = chain;
            healthLabel = hpLabel;
            feedbackLabel = cutFeedbackLabel;
            healthFill = fill;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += HandleHealthChanged;
                playerHealth.HeadDamaged += HandleHeadDamaged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsCut += HandleSegmentsCut;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
                playerHealth.HeadDamaged -= HandleHeadDamaged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsCut -= HandleSegmentsCut;
            }

            _subscribed = false;
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            Refresh();
        }

        private void HandleHeadDamaged(OSDamageEvent damageEvent, float remainingHealth)
        {
            if (feedbackLabel != null)
            {
                feedbackLabel.text = $"CORE HIT  -{damageEvent.Damage:0}  |  HP {remainingHealth:0}";
            }

            _feedbackRemaining = FeedbackDuration;
        }

        private void HandleSegmentsCut(OSBodyRemovalEvent removal)
        {
            if (feedbackLabel != null)
            {
                feedbackLabel.text =
                    $"BODY CUT  {removal.StartIndex + 1} → TAIL  |  LOST {removal.RemovedCount}";
            }

            _feedbackRemaining = FeedbackDuration;
        }

        private void Refresh()
        {
            if (playerHealth == null)
            {
                return;
            }

            if (healthLabel != null)
            {
                var invulnerability = playerHealth.IsInvulnerable
                    ? $"  |  INVULN {Mathf.Max(playerHealth.HitInvulnerabilityRemaining, playerHealth.ExplosionInvulnerabilityRemaining):0.0}s"
                    : string.Empty;
                healthLabel.text = $"CORE HP  {playerHealth.CurrentHealth:0} / {playerHealth.MaxHealth:0}{invulnerability}";
            }

            if (healthFill != null)
            {
                healthFill.fillAmount = playerHealth.MaxHealth > 0f
                    ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth)
                    : 0f;
            }
        }
    }
}
