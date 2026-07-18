using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSPlayerHeadVisual : MonoBehaviour
    {
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform coreVisual;
        [SerializeField] private Transform directionIndicator;
        [SerializeField, Min(0f)] private float pulseAmount = 0.035f;
        [SerializeField, Min(0f)] private float pulseFrequency = 3f;
        [SerializeField, Min(0f)] private float indicatorRadius = 0.72f;

        private Vector3 _coreBaseScale = Vector3.one;
        private float _pulseTime;

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

            if (directionIndicator == null || playerController == null)
            {
                return;
            }

            var direction = playerController.LastDirection;
            directionIndicator.localPosition = direction * indicatorRadius;
            directionIndicator.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Assigns the head core and direction marker used by the Step 04 placeholder visual.
        /// </summary>
        public void Configure(
            OSPlayerController controller,
            OSGameSessionController session,
            Transform core,
            Transform indicator)
        {
            playerController = controller;
            sessionController = session;
            coreVisual = core;
            directionIndicator = indicator;
            CaptureBaseScale();
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
