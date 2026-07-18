using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class OSEnemyController : OSPoolableBehaviour
    {
        private const float MinimumDistanceSquared = 0.000001f;

        [Header("Definition")]
        [SerializeField] private OSEncounterBalanceData encounterBalance;
        [SerializeField] private string definitionId = "enemy_chaser";

        [Header("References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Header("World safety")]
        [SerializeField, Min(1f)] private float reclaimDistance = 32f;
        [SerializeField, Min(0f)] private float separationRadius = 0.72f;
        [SerializeField, Min(0f)] private float separationStrength = 0.55f;

        [Header("Fallback definition")]
        [SerializeField, Min(0.01f)] private float maxHealth = 18f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.1f;
        [SerializeField, Min(0f)] private float contactDamage = 8f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1f;
        [SerializeField, Min(0)] private int fragmentDropAmount = 1;
        [SerializeField, Range(0f, 1f)] private float fragmentDropChance = 0.15f;
        [SerializeField] private bool controlAffectsMovement = true;
        [SerializeField] private bool controlAffectsAttack;

        private OSEnemyRegistry _registry;
        private OSGameSessionController _session;
        private Transform _target;
        private Transform _contactTransform;
        private int _contactRuntimeId;
        private OSTargetKind _contactTargetKind;
        private float _attackCooldown;
        private float _movementControlRemaining;
        private float _attackControlRemaining;
        private bool _hasContact;
        private bool _deathConfirmed;
        private bool _definitionResolved;

        public event Action<OSEnemyController> Died;
        public event Action<OSDamageEvent> ContactAttackRequested;

        public int RegistryIndex { get; internal set; } = -1;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public float AttackInterval => attackInterval;
        public int FragmentDropAmount => fragmentDropAmount;
        public float FragmentDropChance => fragmentDropChance;
        public float AttackCooldown => _attackCooldown;
        public bool IsDeathConfirmed => _deathConfirmed;
        public bool HasContactTarget => _hasContact;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private void Awake()
        {
            ResolveComponents();
            ResolveDefinition();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public void ConfigureRuntime(
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform target)
        {
            _registry = registry;
            _session = session;
            _target = target;
        }

        public void BeginContact(int targetRuntimeId, OSTargetKind targetKind, Transform contactTransform)
        {
            if (!IsRented || targetRuntimeId <= 0)
            {
                return;
            }

            _contactRuntimeId = targetRuntimeId;
            _contactTargetKind = targetKind;
            _contactTransform = contactTransform;
            _hasContact = true;
        }

        public void EndContact()
        {
            _contactRuntimeId = 0;
            _contactTransform = null;
            _hasContact = false;
        }

        public OSRuleResult<float> TryApplyDamage(float damage)
        {
            if (!IsRented || _deathConfirmed)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.Duplicate,
                    "enemy.damage.already_dead",
                    CurrentHealth);
            }

            if (!float.IsFinite(damage) || damage <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "enemy.damage.invalid_amount",
                    CurrentHealth);
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (CurrentHealth <= 0f)
            {
                ConfirmDeath();
            }

            return OSRuleResult<float>.Accepted(CurrentHealth, "enemy.damage.accepted");
        }

        public OSRuleResult<float> ApplyControl(float duration)
        {
            if (!IsRented || !float.IsFinite(duration) || duration <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "enemy.control.invalid_duration");
            }

            if (controlAffectsMovement)
            {
                _movementControlRemaining = Mathf.Max(_movementControlRemaining, duration);
            }

            if (controlAffectsAttack)
            {
                _attackControlRemaining = Mathf.Max(_attackControlRemaining, duration);
            }

            return OSRuleResult<float>.Accepted(duration, "enemy.control.accepted");
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _deathConfirmed || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            _movementControlRemaining = Mathf.Max(0f, _movementControlRemaining - deltaTime);
            _attackControlRemaining = Mathf.Max(0f, _attackControlRemaining - deltaTime);

            if (_target != null)
            {
                var targetOffset = (Vector2)_target.position - Position;
                if (targetOffset.sqrMagnitude > reclaimDistance * reclaimDistance)
                {
                    ReturnToPool();
                    return;
                }

                if (_movementControlRemaining <= 0f)
                {
                    MoveTowardTarget(targetOffset, deltaTime);
                }
            }

            SimulateContactAttack(deltaTime);
        }

        internal void ConfigureForTesting(
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform target,
            float health = 18f,
            float speed = 2.1f,
            float damage = 8f,
            float interval = 1f)
        {
            ConfigureRuntime(registry, session, target);
            maxHealth = Mathf.Max(0.01f, health);
            moveSpeed = Mathf.Max(0f, speed);
            contactDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.01f, interval);
            fragmentDropAmount = 0;
            fragmentDropChance = 0f;
            encounterBalance = null;
            _definitionResolved = true;
        }

        internal void ConfigureDropForTesting(int amount, float chance)
        {
            fragmentDropAmount = Mathf.Max(0, amount);
            fragmentDropChance = Mathf.Clamp01(chance);
        }

        protected override void OnRented()
        {
            ResolveComponents();
            ResolveDefinition();
            CurrentHealth = maxHealth;
            _attackCooldown = 0f;
            _movementControlRemaining = 0f;
            _attackControlRemaining = 0f;
            _deathConfirmed = false;
            EndContact();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            RegistryIndex = -1;

            var registration = _registry?.Register(this);
            if (registration.HasValue && !registration.Value.IsAccepted)
            {
                Debug.LogError(
                    $"[OUROBOROS][ENEMY] Registry rejected '{name}': {registration.Value.ReasonKey}",
                    this);
            }
        }

        protected override void OnReturning()
        {
            if (RegistryIndex >= 0)
            {
                _registry?.Unregister(this);
            }

            EndContact();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            CurrentHealth = 0f;
            _attackCooldown = 0f;
            _movementControlRemaining = 0f;
            _attackControlRemaining = 0f;
            _deathConfirmed = false;
            RegistryIndex = -1;
            Died = null;
            ContactAttackRequested = null;
        }

        private void MoveTowardTarget(Vector2 targetOffset, float deltaTime)
        {
            if (targetOffset.sqrMagnitude <= MinimumDistanceSquared || moveSpeed <= 0f)
            {
                return;
            }

            var direction = targetOffset.normalized;
            if (_registry != null && separationRadius > 0f && separationStrength > 0f)
            {
                var separation = _registry.CalculateSeparation(this, Position, separationRadius);
                direction = Vector2.ClampMagnitude(direction + separation * separationStrength, 1f);
            }

            body.MovePosition(Position + direction * (moveSpeed * deltaTime));
            if (bodyRenderer != null && Mathf.Abs(direction.x) > 0.001f)
            {
                bodyRenderer.flipX = direction.x < 0f;
            }
        }

        private void SimulateContactAttack(float deltaTime)
        {
            if (_attackControlRemaining > 0f)
            {
                return;
            }

            _attackCooldown = Mathf.Max(0f, _attackCooldown - deltaTime);
            if (!_hasContact || _contactRuntimeId <= 0 || _attackCooldown > 0f || contactDamage <= 0f)
            {
                return;
            }

            var idResult = PoolOwner?.NextAttackEventId();
            if (!idResult.HasValue || !idResult.Value.IsAccepted)
            {
                return;
            }

            var hitPosition = _contactTransform != null
                ? (Vector2)_contactTransform.position
                : Position;
            ContactAttackRequested?.Invoke(new OSDamageEvent(
                idResult.Value.Payload,
                Time.frameCount,
                RuntimeId,
                _contactRuntimeId,
                _contactTargetKind,
                contactDamage,
                hitPosition));
            _attackCooldown = attackInterval;
        }

        private void ConfirmDeath()
        {
            if (_deathConfirmed)
            {
                return;
            }

            _deathConfirmed = true;
            Died?.Invoke(this);
            ReturnToPool();
        }

        private void ResolveComponents()
        {
            body ??= GetComponent<Rigidbody2D>();
            bodyRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void ResolveDefinition()
        {
            if (_definitionResolved || encounterBalance == null)
            {
                return;
            }

            var definitions = encounterBalance.EnemyDefinitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || !string.Equals(definition.Id, definitionId, StringComparison.Ordinal))
                {
                    continue;
                }

                maxHealth = definition.MaxHealth;
                moveSpeed = definition.MoveSpeed;
                contactDamage = definition.ContactDamage;
                attackInterval = definition.AttackInterval;
                fragmentDropAmount = definition.DropTable.FragmentAmount;
                fragmentDropChance = definition.DropTable.FragmentChance;
                controlAffectsMovement = definition.ControlAffectsMovement;
                controlAffectsAttack = definition.ControlAffectsAttack;
                _definitionResolved = true;
                return;
            }

            Debug.LogError($"[OUROBOROS][ENEMY] Definition '{definitionId}' was not found.", this);
        }

        private void OnValidate()
        {
            reclaimDistance = Mathf.Max(1f, reclaimDistance);
            separationRadius = Mathf.Max(0f, separationRadius);
            separationStrength = Mathf.Max(0f, separationStrength);
            maxHealth = Mathf.Max(0.01f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            contactDamage = Mathf.Max(0f, contactDamage);
            attackInterval = Mathf.Max(0.01f, attackInterval);
            fragmentDropAmount = Mathf.Max(0, fragmentDropAmount);
            fragmentDropChance = Mathf.Clamp01(fragmentDropChance);
        }
    }
}
