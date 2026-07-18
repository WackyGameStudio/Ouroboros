using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSCombatFeedbackPresenter : MonoBehaviour
    {
        private const float DefaultFeedbackDuration = 1.5f;
        private const int BoundarySegments = 28;

        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSExplosionController explosionController;
        [SerializeField] private OSShieldBodyRole shieldRole;
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private LineRenderer cutBoundary;
        [SerializeField] private LineRenderer tailDirection;

        private bool _subscribed;
        private float _remaining;
        private int _messageFrame = -1;
        private int _messagePriority;

        public string LastMessage { get; private set; } = string.Empty;
        public int MessageRevision { get; private set; }
        public int VisibleWorldMarkerCount =>
            (cutBoundary != null && cutBoundary.enabled ? 1 : 0) +
            (tailDirection != null && tailDirection.enabled ? 1 : 0);

        private void Awake()
        {
            ConfigureWorldMarkers();
            HideWorldMarkers();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - Time.unscaledDeltaTime);
            if (_remaining > 0f)
            {
                return;
            }

            if (feedbackLabel != null)
            {
                feedbackLabel.text = string.Empty;
                feedbackLabel.gameObject.SetActive(false);
            }

            HideWorldMarkers();
            _messagePriority = 0;
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideWorldMarkers();
        }

        public void Configure(
            OSPlayerHealth health,
            OSBodyChain chain,
            OSExplosionController explosion,
            OSShieldBodyRole shield,
            OSLevelUpController level,
            TMP_Text label,
            LineRenderer boundary,
            LineRenderer direction)
        {
            Unsubscribe();
            playerHealth = health;
            bodyChain = chain;
            explosionController = explosion;
            shieldRole = shield;
            levelUpController = level;
            feedbackLabel = label;
            cutBoundary = boundary;
            tailDirection = direction;
            ConfigureWorldMarkers();
            HideWorldMarkers();
            Subscribe();
        }

        private void HandleHeadDamaged(OSDamageEvent damageEvent, float remainingHealth)
        {
            ShowFeedback(
                $"CORE HIT  -{playerHealth?.LastAppliedDamage ?? damageEvent.Damage:0} HP  |  CORE {remainingHealth:0}",
                100,
                DefaultFeedbackDuration);
        }

        private void HandleSegmentsRemoving(OSBodyRemovalEvent removal)
        {
            if (removal.Cause != OSBodyRemovalCause.Cut || bodyChain == null)
            {
                return;
            }

            var shield = 0;
            var attack = 0;
            var laser = 0;
            var control = 0;
            for (var index = removal.StartIndex; index < removal.PreviousCount; index++)
            {
                var segment = bodyChain.GetActiveSegment(index);
                switch (segment?.Role)
                {
                    case OSBodyRoleType.Shield:
                        shield++;
                        break;
                    case OSBodyRoleType.Attack:
                        attack++;
                        break;
                    case OSBodyRoleType.Laser:
                        laser++;
                        break;
                    case OSBodyRoleType.Control:
                        control++;
                        break;
                }
            }

            ShowCutMarkers(removal);
            ShowFeedback(
                $"BODY CUT  IMPACT -> TAIL  |  LOST {removal.RemovedCount}  " +
                $"[O]{shield} [>]{attack} [=]{laser} [+]{control}",
                90,
                DefaultFeedbackDuration);
        }

        private void HandleExplosionResolved(OSExplosionResolution resolution)
        {
            ShowFeedback(
                resolution.WasCancelled
                    ? "BLAST CANCELLED  |  RESERVED TAIL WAS LOST"
                    : $"BLAST COMPLETE  |  TAIL -{resolution.ConsumedCount}  HIT {resolution.HitCount}  KILL {resolution.KillCount}",
                80,
                DefaultFeedbackDuration);
        }

        private void HandleShieldConsumed(OSShieldChargeEvent chargeEvent)
        {
            ShowFeedback("SHIELD [O] BLOCKED THE HIT  |  RECHARGING", 85, 1.2f);
        }

        private void HandleShieldRecharged(OSShieldChargeEvent chargeEvent)
        {
            ShowFeedback("SHIELD [O] READY", 50, 1f);
        }

        private void HandleUpgradeApplied(OSUpgradeCandidate candidate, OSUpgradeModifiers modifiers)
        {
            var name = levelUpController != null
                ? levelUpController.GetDisplayName(candidate.Id)
                : candidate.Id;
            ShowFeedback($"UPGRADE APPLIED  |  {name} LV {candidate.NextLevel}", 40, 1.2f);
        }

        private void ShowFeedback(string message, int priority, float duration)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                _messageFrame == Time.frameCount && priority < _messagePriority)
            {
                return;
            }

            if (_messageFrame == Time.frameCount && LastMessage == message)
            {
                _remaining = Mathf.Max(_remaining, duration);
                return;
            }

            _messageFrame = Time.frameCount;
            _messagePriority = priority;
            _remaining = Mathf.Max(0.1f, duration);
            LastMessage = message;
            MessageRevision++;
            if (feedbackLabel != null)
            {
                feedbackLabel.text = message;
                feedbackLabel.gameObject.SetActive(true);
            }
        }

        private void ShowCutMarkers(OSBodyRemovalEvent removal)
        {
            if (cutBoundary != null)
            {
                cutBoundary.enabled = true;
                cutBoundary.positionCount = BoundarySegments;
                for (var index = 0; index < BoundarySegments; index++)
                {
                    var angle = index * Mathf.PI * 2f / BoundarySegments;
                    cutBoundary.SetPosition(
                        index,
                        removal.HitPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.72f);
                }
            }

            if (tailDirection == null)
            {
                return;
            }

            var tail = removal.PreviousCount > 0
                ? bodyChain.GetActiveSegment(removal.PreviousCount - 1)
                : null;
            tailDirection.enabled = tail?.View != null;
            if (!tailDirection.enabled)
            {
                return;
            }

            var hit = removal.HitPosition;
            var tailPosition = (Vector2)tail.View.transform.position;
            var direction = (tailPosition - hit).sqrMagnitude > 0.001f
                ? (tailPosition - hit).normalized
                : Vector2.left;
            var end = Vector2.Lerp(hit, tailPosition, 0.82f);
            var side = new Vector2(-direction.y, direction.x) * 0.22f;
            tailDirection.positionCount = 4;
            tailDirection.SetPosition(0, hit);
            tailDirection.SetPosition(1, end);
            tailDirection.SetPosition(2, end - direction * 0.35f + side);
            tailDirection.SetPosition(3, end);
        }

        private void ConfigureWorldMarkers()
        {
            if (cutBoundary != null)
            {
                cutBoundary.useWorldSpace = true;
                cutBoundary.loop = true;
                cutBoundary.startWidth = 0.11f;
                cutBoundary.endWidth = 0.11f;
                cutBoundary.startColor = new Color32(255, 80, 92, 245);
                cutBoundary.endColor = new Color32(255, 194, 74, 245);
                cutBoundary.sortingOrder = 185;
            }

            if (tailDirection != null)
            {
                tailDirection.useWorldSpace = true;
                tailDirection.loop = false;
                tailDirection.startWidth = 0.08f;
                tailDirection.endWidth = 0.08f;
                tailDirection.startColor = new Color32(255, 194, 74, 235);
                tailDirection.endColor = new Color32(255, 80, 92, 235);
                tailDirection.sortingOrder = 184;
            }
        }

        private void HideWorldMarkers()
        {
            if (cutBoundary != null)
            {
                cutBoundary.enabled = false;
            }

            if (tailDirection != null)
            {
                tailDirection.enabled = false;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.HeadDamaged += HandleHeadDamaged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving += HandleSegmentsRemoving;
            }

            if (explosionController != null)
            {
                explosionController.ExplosionResolved += HandleExplosionResolved;
            }

            if (shieldRole != null)
            {
                shieldRole.ShieldConsumed += HandleShieldConsumed;
                shieldRole.ShieldRecharged += HandleShieldRecharged;
            }

            if (levelUpController != null)
            {
                levelUpController.UpgradeApplied += HandleUpgradeApplied;
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
                playerHealth.HeadDamaged -= HandleHeadDamaged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentsRemoving -= HandleSegmentsRemoving;
            }

            if (explosionController != null)
            {
                explosionController.ExplosionResolved -= HandleExplosionResolved;
            }

            if (shieldRole != null)
            {
                shieldRole.ShieldConsumed -= HandleShieldConsumed;
                shieldRole.ShieldRecharged -= HandleShieldRecharged;
            }

            if (levelUpController != null)
            {
                levelUpController.UpgradeApplied -= HandleUpgradeApplied;
            }

            _subscribed = false;
        }
    }
}
