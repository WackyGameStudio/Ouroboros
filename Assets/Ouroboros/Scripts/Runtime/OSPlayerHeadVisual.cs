using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSPlayerHeadVisual : MonoBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform coreVisual;
        [SerializeField] private Transform directionIndicator;
        [SerializeField, Min(0f)] private float pulseAmount = 0.035f;
        [SerializeField, Min(0f)] private float pulseFrequency = 3f;
        [SerializeField, Min(0f)] private float indicatorRadius = 0.72f;
        [SerializeField] private float spriteForwardDegrees = 90f;

        private Vector3 _coreBaseScale = Vector3.one;
        private float _pulseTime;

        public float SpriteForwardDegrees => spriteForwardDegrees;

        private void Awake()
        {
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            CaptureBaseScale();
        }

        private void Update()
        {
            if (coreVisual != null)
            {
                if (sessionController != null && sessionController.IsSimulationRunning)
                {
                    _pulseTime += Mathf.Max(0f, Time.deltaTime);
                    var pulse = 1f + (Mathf.Sin(_pulseTime * Mathf.PI * 2f * pulseFrequency) * pulseAmount);
                    coreVisual.localScale = _coreBaseScale * pulse;
                }
                else
                {
                    coreVisual.localScale = _coreBaseScale;
                }
            }

            RefreshFacing();
        }

        internal void RefreshFacing()
        {
            if (playerController == null)
            {
                return;
            }

            var direction = playerController.LastDirection;
            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                return;
            }

            direction.Normalize();
            var targetDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (coreVisual != null)
            {
                coreVisual.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    targetDegrees - spriteForwardDegrees);
            }

            if (directionIndicator != null)
            {
                directionIndicator.localPosition = direction * indicatorRadius;
                directionIndicator.localRotation = Quaternion.Euler(0f, 0f, targetDegrees);
            }
        }

        /// <summary>
        /// Assigns the head core and direction marker used by the Step 04 placeholder visual.
        /// </summary>
        public void Configure(
            OSPlayerController controller,
            OSGameSessionController session,
            Transform core,
            Transform indicator,
            float sourceSpriteForwardDegrees = 90f)
        {
            playerController = controller;
            sessionController = session;
            coreVisual = core;
            directionIndicator = indicator;
            spriteForwardDegrees = sourceSpriteForwardDegrees;
            CaptureBaseScale();
            RefreshFacing();
        }

        private void CaptureBaseScale()
        {
            if (coreVisual != null)
            {
                _coreBaseScale = coreVisual.localScale;
            }
        }
    }
}
