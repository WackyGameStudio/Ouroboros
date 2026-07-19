using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class OSPlayerController : MonoBehaviour
    {
        private const float MinimumMoveInput = 0.0001f;
        private const float MinimumMoveDistance = 0.000001f;
        private const float DefaultMoveSpeed = 5.5f;

        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D solidCollider;
        [SerializeField] private OSInputRouter inputRouter;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerBalanceData playerBalance;
        [SerializeField] private LayerMask worldBlockerMask;
        [SerializeField] private Vector2 worldMin = new(-24f, -15f);
        [SerializeField] private Vector2 worldMax = new(24f, 15f);
        [SerializeField, Min(0.001f)] private float skinWidth = 0.02f;
        [SerializeField, Range(1, 5)] private int maxSlideIterations = 3;

        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[16];
        private ContactFilter2D _blockerFilter;
        private Vector2 _moveInput;
        private Vector2 _lastDirection = Vector2.right;
        private Vector2 _spawnPosition;
        private bool _spawnCaptured;
        private bool _subscribed;
        private float _moveSpeedMultiplier = 1f;
        private bool _bodyDashActive;
        private Vector2 _bodyDashDirection = Vector2.right;
        private float _bodyDashDuration;
        private float _bodyDashDistance;
        private float _bodyDashElapsed;
        private float _bodyDashTravelledDistance;

        public event Action PositionReset;
        public event Action<float> BodyDashCompleted;

        public Vector2 MoveInput => _moveInput;
        public Vector2 LastDirection => _lastDirection;
        public Vector2 WorldMin => worldMin;
        public Vector2 WorldMax => worldMax;
        public Vector2 Position => body != null ? body.position : transform.position;
        public float MoveSpeed => OSUpgradeMath.CalculateMoveSpeed(
            playerBalance != null ? playerBalance.MoveSpeed : DefaultMoveSpeed,
            _moveSpeedMultiplier);
        public bool IsBodyDashActive => _bodyDashActive;
        public float BodyDashRemaining => _bodyDashActive
            ? Mathf.Max(0f, _bodyDashDuration - _bodyDashElapsed)
            : 0f;
        public bool IsMovementAllowed => sessionController != null && sessionController.IsSimulationRunning &&
                                         inputRouter != null && inputRouter.CurrentMode == OSInputMode.Player;

        private void Awake()
        {
            ResolveComponents();
            CaptureSpawnPosition();
            RebuildContactFilter();
        }

        private void OnEnable()
        {
            ResolveComponents();
            Subscribe();
            _moveInput = inputRouter != null ? NormalizeMoveInput(inputRouter.MoveValue) : Vector2.zero;
        }

        private void OnDisable()
        {
            Unsubscribe();
            _moveInput = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (_bodyDashActive)
            {
                SimulateBodyDashStep(Time.fixedDeltaTime);
            }
            else
            {
                SimulateMovementStep(_moveInput, Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Assigns the session-owned input dependencies and immutable movement configuration.
        /// </summary>
        public void Configure(
            OSInputRouter router,
            OSGameSessionController session,
            OSPlayerBalanceData balance,
            LayerMask blockerMask,
            Vector2 minimum,
            Vector2 maximum)
        {
            Unsubscribe();
            ResolveComponents();
            inputRouter = router;
            sessionController = session;
            playerBalance = balance;
            worldBlockerMask = blockerMask;
            worldMin = Vector2.Min(minimum, maximum);
            worldMax = Vector2.Max(minimum, maximum);
            CaptureSpawnPosition(true);
            RebuildContactFilter();
            Subscribe();
        }

        /// <summary>
        /// Returns a vector whose magnitude never exceeds one, preserving analogue input below one.
        /// </summary>
        public static Vector2 NormalizeMoveInput(Vector2 rawInput)
        {
            if (!float.IsFinite(rawInput.x) || !float.IsFinite(rawInput.y))
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(rawInput, 1f);
        }

        /// <summary>
        /// Calculates frame displacement with identical straight and diagonal top speed.
        /// </summary>
        public static Vector2 CalculateDisplacement(Vector2 rawInput, float speed, float deltaTime)
        {
            if (!float.IsFinite(speed) || !float.IsFinite(deltaTime) || speed <= 0f || deltaTime <= 0f)
            {
                return Vector2.zero;
            }

            return NormalizeMoveInput(rawInput) * speed * deltaTime;
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _moveSpeedMultiplier = Mathf.Max(0.01f, modifiers.MoveSpeedMultiplier);
        }

        /// <summary>
        /// Restores the captured spawn position without changing session state or time ownership.
        /// </summary>
        public void ResetToSpawn()
        {
            ResolveComponents();
            if (body == null)
            {
                return;
            }

            body.position = ClampToWorld(_spawnPosition);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            CancelBodyDash();
            _moveInput = Vector2.zero;
            _lastDirection = Vector2.right;
            PositionReset?.Invoke();
        }

        public bool TryStartBodyDash(Vector2 direction, float duration, float distance)
        {
            if (_bodyDashActive || !IsMovementAllowed || body == null || solidCollider == null ||
                !float.IsFinite(duration) || !float.IsFinite(distance) ||
                duration < OSBodyDashMath.MinimumDuration || distance < OSBodyDashMath.MinimumDistance)
            {
                return false;
            }

            _bodyDashDirection = OSBodyDashMath.ResolveDirection(direction, _lastDirection);
            _lastDirection = _bodyDashDirection;
            _bodyDashDuration = duration;
            _bodyDashDistance = distance;
            _bodyDashElapsed = 0f;
            _bodyDashTravelledDistance = 0f;
            _bodyDashActive = true;
            return true;
        }

        public void CancelBodyDash()
        {
            _bodyDashActive = false;
            _bodyDashDuration = 0f;
            _bodyDashDistance = 0f;
            _bodyDashElapsed = 0f;
            _bodyDashTravelledDistance = 0f;
        }

        /// <summary>
        /// Applies boss-forced movement through the same kinematic cast and world bounds as player input.
        /// </summary>
        public bool ApplyExternalDisplacement(Vector2 displacement)
        {
            if (sessionController == null || !sessionController.IsSimulationRunning || body == null ||
                solidCollider == null || !float.IsFinite(displacement.x) ||
                !float.IsFinite(displacement.y) || displacement.sqrMagnitude <= MinimumMoveDistance)
            {
                return false;
            }

            MoveWithSlide(displacement);
            return true;
        }

        internal void SimulateBodyDashStep(float deltaTime)
        {
            if (!_bodyDashActive || !IsMovementAllowed || body == null || solidCollider == null ||
                !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            var previousElapsed = _bodyDashElapsed;
            var nextElapsed = Mathf.Min(_bodyDashDuration, previousElapsed + deltaTime);
            var stepDistance = OSBodyDashMath.CalculateStepDistance(
                _bodyDashDistance,
                _bodyDashDuration,
                previousElapsed,
                nextElapsed);
            _bodyDashTravelledDistance += MoveWithSlide(_bodyDashDirection * stepDistance);
            _bodyDashElapsed = nextElapsed;
            if (_bodyDashElapsed + 0.000001f < _bodyDashDuration)
            {
                return;
            }

            var travelledDistance = _bodyDashTravelledDistance;
            CancelBodyDash();
            BodyDashCompleted?.Invoke(travelledDistance);
        }

        internal void SimulateMovementStep(Vector2 rawInput, float deltaTime)
        {
            if (!IsMovementAllowed || body == null || solidCollider == null)
            {
                return;
            }

            var input = NormalizeMoveInput(rawInput);
            if (input.sqrMagnitude <= MinimumMoveInput)
            {
                return;
            }

            _lastDirection = input.normalized;
            var displacement = CalculateDisplacement(input, MoveSpeed, deltaTime);
            if (displacement.sqrMagnitude <= MinimumMoveDistance)
            {
                return;
            }

            MoveWithSlide(displacement);
        }

        private float MoveWithSlide(Vector2 displacement)
        {
            var startPosition = body.position;
            var remaining = displacement;
            for (var iteration = 0; iteration < maxSlideIterations; iteration++)
            {
                var distance = remaining.magnitude;
                if (distance <= MinimumMoveDistance)
                {
                    break;
                }

                var direction = remaining / distance;
                var hitCount = solidCollider.Cast(
                    direction,
                    _blockerFilter,
                    _castHits,
                    distance + skinWidth);

                if (!TryGetClosestHit(hitCount, direction, out var closestHit))
                {
                    body.position += remaining;
                    remaining = Vector2.zero;
                    break;
                }

                var safeDistance = Mathf.Clamp(closestHit.distance - skinWidth, 0f, distance);
                var safeMove = direction * safeDistance;
                body.position += safeMove;

                var unresolved = remaining - safeMove;
                var intoSurface = Vector2.Dot(unresolved, closestHit.normal);
                if (intoSurface < 0f)
                {
                    unresolved -= closestHit.normal * intoSurface;
                }

                if (safeDistance <= MinimumMoveDistance && unresolved.sqrMagnitude >= remaining.sqrMagnitude -
                    MinimumMoveDistance)
                {
                    break;
                }

                remaining = unresolved;
            }

            body.position = ClampToWorld(body.position);
            return Vector2.Distance(startPosition, body.position);
        }

        private bool TryGetClosestHit(int hitCount, Vector2 castDirection, out RaycastHit2D closestHit)
        {
            closestHit = default;
            var closestDistance = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = _castHits[i];
                if (hit.collider == null || hit.distance < 0f || hit.distance >= closestDistance ||
                    hit.distance <= MinimumMoveDistance && Vector2.Dot(castDirection, hit.normal) >= -0.0001f)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }

            return found;
        }

        private Vector2 ClampToWorld(Vector2 bodyPosition)
        {
            if (solidCollider == null)
            {
                return new Vector2(
                    Mathf.Clamp(bodyPosition.x, worldMin.x, worldMax.x),
                    Mathf.Clamp(bodyPosition.y, worldMin.y, worldMax.y));
            }

            var bounds = solidCollider.bounds;
            var currentBodyPosition = body != null ? body.position : bodyPosition;
            var centerOffset = (Vector2)bounds.center - currentBodyPosition;
            var extents = (Vector2)bounds.extents;
            var minimumCenter = worldMin + extents;
            var maximumCenter = worldMax - extents;
            var targetCenter = bodyPosition + centerOffset;

            if (minimumCenter.x > maximumCenter.x)
            {
                targetCenter.x = (worldMin.x + worldMax.x) * 0.5f;
            }
            else
            {
                targetCenter.x = Mathf.Clamp(targetCenter.x, minimumCenter.x, maximumCenter.x);
            }

            if (minimumCenter.y > maximumCenter.y)
            {
                targetCenter.y = (worldMin.y + worldMax.y) * 0.5f;
            }
            else
            {
                targetCenter.y = Mathf.Clamp(targetCenter.y, minimumCenter.y, maximumCenter.y);
            }

            return targetCenter - centerOffset;
        }

        private void ResolveComponents()
        {
            body ??= GetComponent<Rigidbody2D>();
            solidCollider ??= GetComponent<Collider2D>();
        }

        private void CaptureSpawnPosition(bool force = false)
        {
            if (_spawnCaptured && !force)
            {
                return;
            }

            ResolveComponents();
            _spawnPosition = body != null ? body.position : (Vector2)transform.position;
            _spawnCaptured = true;
        }

        private void RebuildContactFilter()
        {
            _blockerFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = worldBlockerMask,
                useTriggers = false
            };
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled || inputRouter == null || sessionController == null)
            {
                return;
            }

            inputRouter.MoveChanged += HandleMoveChanged;
            sessionController.StateChanged += HandleStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (inputRouter != null)
            {
                inputRouter.MoveChanged -= HandleMoveChanged;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleStateChanged;
            }

            _subscribed = false;
        }

        private void HandleMoveChanged(Vector2 value)
        {
            _moveInput = NormalizeMoveInput(value);
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current is not OSSessionState.Combat and not OSSessionState.BodyDash)
            {
                _moveInput = Vector2.zero;
                CancelBodyDash();
            }

            if (current == OSSessionState.Boot ||
                previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                ResetToSpawn();
            }
        }

        private void OnValidate()
        {
            skinWidth = Mathf.Max(0.001f, skinWidth);
            maxSlideIterations = Mathf.Clamp(maxSlideIterations, 1, 5);
            var minimum = Vector2.Min(worldMin, worldMax);
            var maximum = Vector2.Max(worldMin, worldMax);
            worldMin = minimum;
            worldMax = maximum;
            RebuildContactFilter();
        }
    }
}
