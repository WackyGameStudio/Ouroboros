using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public interface IOSControlProjectileFeedbackSink
    {
        void OnControlHitConfirmed(OSDamageEvent controlEvent, float appliedDuration);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class OSControlProjectile : OSPoolableBehaviour
    {
        private const float DirectionEpsilon = 0.000001f;

        [SerializeField] private Rigidbody2D body;
        [SerializeField, Min(0.01f)] private float moveSpeed = 9f;

        private OSGameSessionController _session;
        private IOSControlProjectileFeedbackSink _feedbackSink;
        private Vector2 _direction;
        private float _maximumDistance;
        private float _travelledDistance;
        private float _normalDuration;
        private float _eliteDuration;
        private int _attackEventId;
        private int _sourceStableId;

        public int AttackEventId => _attackEventId;
        public int SourceStableId => _sourceStableId;
        public float Damage => 0f;
        public float TravelledDistance => _travelledDistance;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private void Awake()
        {
            ResolveComponents();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public void ConfigureRuntime(OSGameSessionController session)
        {
            _session = session;
        }

        public OSRuleResult<int> Launch(
            int attackEventId,
            int sourceStableId,
            Vector2 direction,
            float damage,
            float maximumDistance,
            float normalDuration,
            float eliteDuration,
            IOSControlProjectileFeedbackSink feedbackSink)
        {
            if (!IsRented || attackEventId <= 0 || sourceStableId <= 0 ||
                !float.IsFinite(damage) || Mathf.Abs(damage) > 0.000001f ||
                !float.IsFinite(maximumDistance) || maximumDistance <= 0f ||
                !float.IsFinite(normalDuration) || normalDuration <= 0f ||
                !float.IsFinite(eliteDuration) || eliteDuration <= 0f ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                direction.sqrMagnitude <= DirectionEpsilon)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "control_projectile.launch.invalid_payload");
            }

            _attackEventId = attackEventId;
            _sourceStableId = sourceStableId;
            _direction = direction.normalized;
            _maximumDistance = maximumDistance;
            _normalDuration = normalDuration;
            _eliteDuration = eliteDuration;
            _feedbackSink = feedbackSink;
            transform.right = _direction;
            return OSRuleResult<int>.Accepted(RuntimeId, "control_projectile.launch.accepted");
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _attackEventId <= 0 || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            var remainingDistance = Mathf.Max(0f, _maximumDistance - _travelledDistance);
            var stepDistance = Mathf.Min(moveSpeed * deltaTime, remainingDistance);
            if (stepDistance > 0f)
            {
                body.MovePosition(Position + (_direction * stepDistance));
                _travelledDistance += stepDistance;
            }

            if (_travelledDistance >= _maximumDistance - 0.0001f)
            {
                ReturnToPool();
            }
        }

        internal bool TryHitEnemy(OSEnemyController enemy)
        {
            if (!IsRented || enemy == null || !enemy.IsRented || enemy.IsDeathConfirmed ||
                enemy.CurrentHealth <= 0f)
            {
                return false;
            }

            var duration = OSBodyRoleMath.SelectControlDuration(
                enemy.Archetype,
                _normalDuration,
                _eliteDuration);
            var result = enemy.ApplyControl(duration);
            if (!result.IsAccepted)
            {
                return false;
            }

            var controlEvent = new OSDamageEvent(
                _attackEventId,
                Time.frameCount,
                _sourceStableId,
                enemy.RuntimeId,
                enemy.Archetype == OSEnemyArchetype.BossSwarmCore
                    ? OSTargetKind.Boss
                    : enemy.Archetype == OSEnemyArchetype.EliteAccelerator
                        ? OSTargetKind.Elite
                        : OSTargetKind.Enemy,
                0f,
                enemy.Position,
                duration);
            _feedbackSink?.OnControlHitConfirmed(controlEvent, duration);
            ReturnToPool();
            return true;
        }

        protected override void OnRented()
        {
            ResolveComponents();
            ResetPayload();
        }

        protected override void OnReturning()
        {
            ResetPayload();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponentInParent<OSEnemyController>();
            if (enemy != null)
            {
                TryHitEnemy(enemy);
            }
        }

        private void ResetPayload()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            _feedbackSink = null;
            _direction = Vector2.zero;
            _maximumDistance = 0f;
            _travelledDistance = 0f;
            _normalDuration = 0f;
            _eliteDuration = 0f;
            _attackEventId = 0;
            _sourceStableId = 0;
        }

        private void ResolveComponents()
        {
            body ??= GetComponent<Rigidbody2D>();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
        }
    }
}
