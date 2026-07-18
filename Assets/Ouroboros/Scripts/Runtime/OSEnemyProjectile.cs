using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class OSEnemyProjectile : OSPoolableBehaviour
    {
        private const float DirectionEpsilon = 0.000001f;

        [SerializeField] private Rigidbody2D body;
        [SerializeField, Min(0.01f)] private float moveSpeed = 6f;

        private OSGameSessionController _session;
        private OSPlayerCombatResolver _combatResolver;
        private Vector2 _direction;
        private float _damage;
        private float _maximumDistance;
        private float _travelledDistance;
        private int _attackEventId;
        private int _sourceRuntimeId;

        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public float TravelledDistance => _travelledDistance;
        public int AttackEventId => _attackEventId;

        private void Awake()
        {
            ResolveComponents();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public void ConfigureRuntime(
            OSGameSessionController session,
            OSPlayerCombatResolver combatResolver)
        {
            _session = session;
            _combatResolver = combatResolver;
        }

        public OSRuleResult<int> Launch(
            int attackEventId,
            int sourceRuntimeId,
            Vector2 direction,
            float damage,
            float maximumDistance)
        {
            if (!IsRented || attackEventId <= 0 || sourceRuntimeId <= 0 ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                direction.sqrMagnitude <= DirectionEpsilon ||
                !float.IsFinite(damage) || damage <= 0f ||
                !float.IsFinite(maximumDistance) || maximumDistance <= 0f)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "enemy_projectile.launch.invalid_payload");
            }

            _attackEventId = attackEventId;
            _sourceRuntimeId = sourceRuntimeId;
            _direction = direction.normalized;
            _damage = damage;
            _maximumDistance = maximumDistance;
            transform.right = _direction;
            return OSRuleResult<int>.Accepted(RuntimeId, "enemy_projectile.launch.accepted");
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _attackEventId <= 0 || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            var remaining = Mathf.Max(0f, _maximumDistance - _travelledDistance);
            var distance = Mathf.Min(moveSpeed * deltaTime, remaining);
            if (distance > 0f)
            {
                body.MovePosition(Position + (_direction * distance));
                _travelledDistance += distance;
            }

            if (_travelledDistance >= _maximumDistance - 0.0001f)
            {
                ReturnToPool();
            }
        }

        internal bool TryHitPlayer(OSCombatTargetIdentity target)
        {
            if (!IsRented || target == null || _attackEventId <= 0 ||
                target.TargetKind is not OSTargetKind.PlayerHead and not OSTargetKind.PlayerBody)
            {
                return false;
            }

            var damageEvent = new OSDamageEvent(
                _attackEventId,
                Time.frameCount,
                _sourceRuntimeId,
                target.RuntimeId,
                target.TargetKind,
                _damage,
                target.transform.position);
            var result = _combatResolver?.EnqueueDamage(damageEvent);
            if (!result.HasValue || !result.Value.IsAccepted)
            {
                return false;
            }

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
            var target = other.GetComponentInParent<OSCombatTargetIdentity>();
            if (target != null)
            {
                TryHitPlayer(target);
            }
        }

        private void ResetPayload()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            _direction = Vector2.zero;
            _damage = 0f;
            _maximumDistance = 0f;
            _travelledDistance = 0f;
            _attackEventId = 0;
            _sourceRuntimeId = 0;
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
