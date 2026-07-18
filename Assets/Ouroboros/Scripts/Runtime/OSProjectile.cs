using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public interface IOSProjectileFeedbackSink
    {
        void OnProjectileHitConfirmed(OSDamageEvent damageEvent, bool targetDefeated);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class OSProjectile : OSPoolableBehaviour
    {
        private const int MaximumRecordedTargets = 8;
        private const float MinimumDirectionSquared = 0.000001f;

        [SerializeField] private Rigidbody2D body;
        [SerializeField, Min(0.01f)] private float moveSpeed = 12f;

        private readonly int[] _hitRuntimeIds = new int[MaximumRecordedTargets];
        private OSGameSessionController _session;
        private IOSProjectileFeedbackSink _feedbackSink;
        private Vector2 _direction;
        private float _damage;
        private float _maximumDistance;
        private float _travelledDistance;
        private int _attackEventId;
        private int _sourceRuntimeId;
        private int _remainingPierce;
        private int _hitCount;

        public int AttackEventId => _attackEventId;
        public int SourceRuntimeId => _sourceRuntimeId;
        public int RemainingPierce => _remainingPierce;
        public int HitCount => _hitCount;
        public float TravelledDistance => _travelledDistance;
        public Vector2 Direction => _direction;
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
            int sourceRuntimeId,
            Vector2 direction,
            float damage,
            float maximumDistance,
            int pierce,
            IOSProjectileFeedbackSink feedbackSink)
        {
            if (!IsRented || attackEventId <= 0 || sourceRuntimeId <= 0 ||
                !float.IsFinite(damage) || damage <= 0f ||
                !float.IsFinite(maximumDistance) || maximumDistance <= 0f ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                direction.sqrMagnitude <= MinimumDirectionSquared)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "projectile.launch.invalid_payload");
            }

            _attackEventId = attackEventId;
            _sourceRuntimeId = sourceRuntimeId;
            _direction = direction.normalized;
            _damage = damage;
            _maximumDistance = maximumDistance;
            _remainingPierce = Mathf.Clamp(pierce, 0, MaximumRecordedTargets - 1);
            _feedbackSink = feedbackSink;
            transform.right = _direction;
            return OSRuleResult<int>.Accepted(RuntimeId, "projectile.launch.accepted");
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
                body.MovePosition(Position + _direction * stepDistance);
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
                enemy.CurrentHealth <= 0f || HasAlreadyHit(enemy.RuntimeId))
            {
                return false;
            }

            if (_hitCount >= _hitRuntimeIds.Length)
            {
                ReturnToPool();
                return false;
            }

            _hitRuntimeIds[_hitCount++] = enemy.RuntimeId;
            var targetRuntimeId = enemy.RuntimeId;
            var hitPosition = enemy.Position;
            var damageEvent = new OSDamageEvent(
                _attackEventId,
                Time.frameCount,
                _sourceRuntimeId,
                targetRuntimeId,
                OSTargetKind.Enemy,
                _damage,
                hitPosition);
            var result = enemy.TryApplyDamage(_damage);
            if (!result.IsAccepted)
            {
                return false;
            }

            var targetDefeated = !enemy.IsRented;
            _feedbackSink?.OnProjectileHitConfirmed(damageEvent, targetDefeated);
            if (_remainingPierce <= 0)
            {
                ReturnToPool();
            }
            else
            {
                _remainingPierce--;
            }

            return true;
        }

        protected override void OnRented()
        {
            ResolveComponents();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            _feedbackSink = null;
            _direction = Vector2.zero;
            _damage = 0f;
            _maximumDistance = 0f;
            _travelledDistance = 0f;
            _attackEventId = 0;
            _sourceRuntimeId = 0;
            _remainingPierce = 0;
            _hitCount = 0;
            Array.Clear(_hitRuntimeIds, 0, _hitRuntimeIds.Length);
        }

        protected override void OnReturning()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            _feedbackSink = null;
            _direction = Vector2.zero;
            _damage = 0f;
            _maximumDistance = 0f;
            _travelledDistance = 0f;
            _attackEventId = 0;
            _sourceRuntimeId = 0;
            _remainingPierce = 0;
            _hitCount = 0;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponentInParent<OSEnemyController>();
            if (enemy != null)
            {
                TryHitEnemy(enemy);
            }
        }

        private bool HasAlreadyHit(int runtimeId)
        {
            for (var index = 0; index < _hitCount; index++)
            {
                if (_hitRuntimeIds[index] == runtimeId)
                {
                    return true;
                }
            }

            return false;
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
