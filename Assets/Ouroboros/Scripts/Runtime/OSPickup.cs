using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// One pooled pickup with amount preservation, magnet movement, and explicit collector validation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public sealed class OSPickup : OSPoolableBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField, Min(0.01f)] private float magnetSpeed = 8f;

        private OSPickupSpawner _spawner;
        private OSGameSessionController _session;
        private Transform _target;
        private float _magnetRadius;

        public OSPickupType PickupType { get; private set; }
        public int Amount { get; private set; }
        public int RegistryIndex { get; internal set; } = -1;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private void Awake()
        {
            ResolveComponents();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public OSRuleResult<int> AddAmount(int amount)
        {
            if (!IsRented || amount <= 0 || Amount > int.MaxValue - amount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.amount.invalid",
                    Amount);
            }

            Amount += amount;
            return OSRuleResult<int>.Accepted(Amount, "pickup.amount.merged");
        }

        public OSRuleResult<int> TryCollect(OSPickupCollector collector)
        {
            if (!IsRented || collector == null || _spawner == null ||
                _session != null && !_session.IsSimulationRunning)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "pickup.collect.invalid_state",
                    Amount);
            }

            return _spawner.Collect(this, collector);
        }

        internal void ConfigurePickup(
            OSPickupSpawner spawner,
            OSGameSessionController session,
            Transform target,
            OSPickupType pickupType,
            int amount,
            float magnetRadius)
        {
            _spawner = spawner;
            _session = session;
            _target = target;
            PickupType = pickupType;
            Amount = Mathf.Max(1, amount);
            _magnetRadius = Mathf.Max(0f, magnetRadius);
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _target == null || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            var offset = (Vector2)_target.position - Position;
            if (offset.sqrMagnitude > _magnetRadius * _magnetRadius || offset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.MovePosition(Vector2.MoveTowards(Position, _target.position, magnetSpeed * deltaTime));
        }

        protected override void OnRented()
        {
            ResolveComponents();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            RegistryIndex = -1;
            Amount = 0;
        }

        protected override void OnReturning()
        {
            _spawner?.Unregister(this);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            _spawner = null;
            _session = null;
            _target = null;
            PickupType = default;
            Amount = 0;
            _magnetRadius = 0f;
            RegistryIndex = -1;
        }

        private void ResolveComponents()
        {
            body ??= GetComponent<Rigidbody2D>();
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            magnetSpeed = Mathf.Max(0.01f, magnetSpeed);
        }
    }
}
