using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class OSCameraFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField, Range(0.08f, 0.15f)] private float smoothTime = 0.1f;
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [SerializeField] private Vector2 worldMin = new(-24f, -15f);
        [SerializeField] private Vector2 worldMax = new(24f, 15f);

        private Vector3 _velocity;
        private float _depth;
        private Quaternion _fixedRotation;
        private bool _subscribed;

        public Transform Target => target;
        public Vector2 WorldMin => worldMin;
        public Vector2 WorldMax => worldMax;
        public float EdgePadding => edgePadding;
        public float SmoothTime => smoothTime;

        private void Awake()
        {
            targetCamera ??= GetComponent<Camera>();
            _depth = transform.position.z;
            _fixedRotation = transform.rotation;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (target == null || targetCamera == null)
            {
                return;
            }

            var desired = CalculateDesiredPosition();
            var deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (smoothTime <= 0f || deltaTime <= 0f)
            {
                if (smoothTime <= 0f)
                {
                    transform.position = desired;
                }
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desired,
                    ref _velocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }

            transform.rotation = _fixedRotation;
        }

        /// <summary>
        /// Assigns a fixed-zoom camera target and its clamped arena bounds.
        /// </summary>
        public void Configure(
            Transform followTarget,
            Camera cameraComponent,
            OSPlayerController controller,
            Vector2 minimum,
            Vector2 maximum,
            float followSmoothTime,
            float viewportPadding)
        {
            Unsubscribe();
            target = followTarget;
            targetCamera = cameraComponent != null ? cameraComponent : GetComponent<Camera>();
            playerController = controller;
            worldMin = Vector2.Min(minimum, maximum);
            worldMax = Vector2.Max(minimum, maximum);
            smoothTime = Mathf.Clamp(followSmoothTime, 0.08f, 0.15f);
            edgePadding = Mathf.Max(0f, viewportPadding);
            _depth = transform.position.z;
            _fixedRotation = transform.rotation;
            _velocity = Vector3.zero;
            Subscribe();
        }

        /// <summary>
        /// Immediately aligns the camera with the target while preserving depth, rotation, and zoom.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null || targetCamera == null)
            {
                return;
            }

            _velocity = Vector3.zero;
            transform.position = CalculateDesiredPosition();
            transform.rotation = _fixedRotation;
        }

        private Vector3 CalculateDesiredPosition()
        {
            var desired = new Vector2(target.position.x, target.position.y);
            if (targetCamera.orthographic)
            {
                var verticalExtent = targetCamera.orthographicSize + edgePadding;
                var horizontalExtent = (targetCamera.orthographicSize * targetCamera.aspect) + edgePadding;
                desired.x = ClampAxis(desired.x, worldMin.x + horizontalExtent, worldMax.x - horizontalExtent);
                desired.y = ClampAxis(desired.y, worldMin.y + verticalExtent, worldMax.y - verticalExtent);
            }

            return new Vector3(desired.x, desired.y, _depth);
        }

        private static float ClampAxis(float value, float minimum, float maximum)
        {
            return minimum <= maximum ? Mathf.Clamp(value, minimum, maximum) : (minimum + maximum) * 0.5f;
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled || playerController == null)
            {
                return;
            }

            playerController.PositionReset += SnapToTarget;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.PositionReset -= SnapToTarget;
            }

            _subscribed = false;
        }

        private void OnValidate()
        {
            smoothTime = Mathf.Clamp(smoothTime, 0.08f, 0.15f);
            edgePadding = Mathf.Max(0f, edgePadding);
            var minimum = Vector2.Min(worldMin, worldMax);
            var maximum = Vector2.Max(worldMin, worldMax);
            worldMin = minimum;
            worldMax = maximum;
        }
    }
}
