using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSExplosionPresenter : MonoBehaviour
    {
        private const int CircleSegments = 48;

        [SerializeField] private OSExplosionController explosionController;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private LineRenderer[] reservationCircles = new LineRenderer[20];

        private bool _subscribed;
        private float _feedbackRemaining;
        private int _lastBodyCount = -1;
        private int _lastReservedCount = -1;
        private int _lastExpectedRemaining = -1;
        private int _lastRemainingTenths = -1;
        private bool _lastTelegraphActive;

        public int VisibleCircleCount { get; private set; }

        private void Awake()
        {
            ConfigureCircles();
            HideCircles();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshStatus();
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

            RefreshStatus();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideCircles();
        }

        private void HandleTelegraph(OSExplosionSnapshot snapshot)
        {
            ShowSnapshot(snapshot);
            if (feedbackLabel != null)
            {
                feedbackLabel.text = "BLAST TELEGRAPH  |  MOVE AND ATTACK ACTIVE";
            }
        }

        private void HandleReservationChanged(OSExplosionSnapshot snapshot)
        {
            ShowSnapshot(snapshot);
            if (feedbackLabel != null)
            {
                feedbackLabel.text = snapshot.ConsumeCount > 0
                    ? $"RESERVATION CUT  |  BLAST x{snapshot.ConsumeCount}"
                    : "RESERVATION LOST  |  BLAST WILL CANCEL";
            }
        }

        private void HandleResolved(OSExplosionResolution resolution)
        {
            HideCircles();
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = resolution.WasCancelled
                ? "BLAST CANCELLED  |  NO RESERVED TAIL"
                : $"BLAST x{resolution.ConsumedCount}  |  DMG {resolution.DamagePerEnemy:0}  |  HIT {resolution.HitCount}";
            _feedbackRemaining = 1.5f;
        }

        private void HandleRejected(OSResultCode code, string reasonKey)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = code == OSResultCode.RejectedRequirement
                ? "BLAST REQUIRES 4 BODY"
                : "BLAST INPUT IGNORED";
            _feedbackRemaining = 1.0f;
        }

        private void RefreshStatus()
        {
            if (statusLabel == null || explosionController == null || bodyChain == null)
            {
                return;
            }

            var telegraphActive = explosionController.IsTelegraphActive;
            var bodyCount = bodyChain.ActiveCount;
            var reservedCount = explosionController.ReservedCount;
            var expectedRemaining = explosionController.ExpectedRemainingBodyCount;
            var remainingTenths = telegraphActive
                ? Mathf.CeilToInt(explosionController.TelegraphRemaining * 10f)
                : 0;
            if (_lastTelegraphActive == telegraphActive && _lastBodyCount == bodyCount &&
                _lastReservedCount == reservedCount && _lastExpectedRemaining == expectedRemaining &&
                _lastRemainingTenths == remainingTenths)
            {
                return;
            }

            _lastTelegraphActive = telegraphActive;
            _lastBodyCount = bodyCount;
            _lastReservedCount = reservedCount;
            _lastExpectedRemaining = expectedRemaining;
            _lastRemainingTenths = remainingTenths;

            if (telegraphActive)
            {
                statusLabel.text =
                    $"BLAST x{reservedCount}  |  AFTER {expectedRemaining}  |  {remainingTenths / 10f:0.0}s";
                return;
            }

            statusLabel.text = bodyCount >= 4
                ? $"BLAST READY [SPACE]  |  BODY {bodyCount}"
                : $"BLAST [SPACE]  |  BODY {bodyCount}/4";
        }

        private void ShowSnapshot(OSExplosionSnapshot snapshot)
        {
            VisibleCircleCount = Mathf.Min(snapshot.Centers.Count, reservationCircles?.Length ?? 0);
            for (var index = 0; index < (reservationCircles?.Length ?? 0); index++)
            {
                var circle = reservationCircles[index];
                if (circle == null)
                {
                    continue;
                }

                var visible = index < VisibleCircleCount;
                circle.enabled = visible;
                if (visible)
                {
                    circle.transform.position = snapshot.Centers[index];
                }
            }
        }

        private void ConfigureCircles()
        {
            if (reservationCircles == null || explosionController == null)
            {
                return;
            }

            var radius = explosionController.Radius;
            for (var rendererIndex = 0; rendererIndex < reservationCircles.Length; rendererIndex++)
            {
                var circle = reservationCircles[rendererIndex];
                if (circle == null)
                {
                    continue;
                }

                circle.useWorldSpace = false;
                circle.loop = true;
                circle.positionCount = CircleSegments;
                circle.startWidth = 0.08f;
                circle.endWidth = 0.08f;
                circle.startColor = new Color32(255, 188, 74, 235);
                circle.endColor = new Color32(255, 90, 66, 235);
                circle.numCapVertices = 2;
                circle.numCornerVertices = 2;
                for (var pointIndex = 0; pointIndex < CircleSegments; pointIndex++)
                {
                    var angle = pointIndex * Mathf.PI * 2f / CircleSegments;
                    circle.SetPosition(
                        pointIndex,
                        new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }
            }
        }

        private void HideCircles()
        {
            VisibleCircleCount = 0;
            if (reservationCircles == null)
            {
                return;
            }

            for (var index = 0; index < reservationCircles.Length; index++)
            {
                if (reservationCircles[index] != null)
                {
                    reservationCircles[index].enabled = false;
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed || explosionController == null || !isActiveAndEnabled)
            {
                return;
            }

            explosionController.TelegraphStarted += HandleTelegraph;
            explosionController.ReservationChanged += HandleReservationChanged;
            explosionController.ExplosionResolved += HandleResolved;
            explosionController.RequestRejected += HandleRejected;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || explosionController == null)
            {
                _subscribed = false;
                return;
            }

            explosionController.TelegraphStarted -= HandleTelegraph;
            explosionController.ReservationChanged -= HandleReservationChanged;
            explosionController.ExplosionResolved -= HandleResolved;
            explosionController.RequestRejected -= HandleRejected;
            _subscribed = false;
        }
    }
}
