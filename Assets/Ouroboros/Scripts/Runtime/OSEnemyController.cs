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
        private const int MaxContactTargets = 65;

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
        private readonly Transform[] _contactTransforms = new Transform[MaxContactTargets];
        private readonly int[] _contactRuntimeIds = new int[MaxContactTargets];
        private readonly OSTargetKind[] _contactTargetKinds = new OSTargetKind[MaxContactTargets];
        private int _contactCount;
        private float _attackCooldown;
        private float _movementControlRemaining;
        private float _attackControlRemaining;
        private bool _deathConfirmed;
        private bool _definitionResolved;
        private OSEnemyArchetype _archetype = OSEnemyArchetype.Chaser;

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
        public bool HasContactTarget => _contactCount > 0;
        public int ContactTargetCount => _contactCount;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public OSEnemyArchetype Archetype => _archetype;
        public bool IsEliteOrBoss => _archetype is OSEnemyArchetype.EliteAccelerator or
            OSEnemyArchetype.BossSwarmCore;
        public float MovementControlRemaining => _movementControlRemaining;
        public float AttackControlRemaining => _attackControlRemaining;
        public bool ControlAffectsMovement => controlAffectsMovement;
        public bool ControlAffectsAttack => controlAffectsAttack;

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

            for (var index = 0; index < _contactCount; index++)
            {
                if (_contactRuntimeIds[index] != targetRuntimeId ||
                    _contactTargetKinds[index] != targetKind)
                {
                    continue;
                }

                _contactTransforms[index] = contactTransform;
                return;
            }

            if (_contactCount >= MaxContactTargets)
            {
                return;
            }

            _contactRuntimeIds[_contactCount] = targetRuntimeId;
            _contactTargetKinds[_contactCount] = targetKind;
            _contactTransforms[_contactCount] = contactTransform;
            _contactCount++;
        }

        public void EndContact()
        {
            Array.Clear(_contactRuntimeIds, 0, _contactCount);
            Array.Clear(_contactTargetKinds, 0, _contactCount);
            Array.Clear(_contactTransforms, 0, _contactCount);
            _contactCount = 0;
        }

        public void EndContact(int targetRuntimeId, OSTargetKind targetKind)
        {
            for (var index = 0; index < _contactCount; index++)
            {
                if (_contactRuntimeIds[index] != targetRuntimeId ||
                    _contactTargetKinds[index] != targetKind)
                {
                    continue;
                }

                var last = --_contactCount;
                _contactRuntimeIds[index] = _contactRuntimeIds[last];
                _contactTargetKinds[index] = _contactTargetKinds[last];
                _contactTransforms[index] = _contactTransforms[last];
                _contactRuntimeIds[last] = 0;
                _contactTargetKinds[last] = default;
                _contactTransforms[last] = null;
                return;
            }
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
            float interval = 1f,
            OSEnemyArchetype archetype = OSEnemyArchetype.Chaser,
            bool controlMovement = true,
            bool controlAttack = false)
        {
            ConfigureRuntime(registry, session, target);
            maxHealth = Mathf.Max(0.01f, health);
            moveSpeed = Mathf.Max(0f, speed);
            contactDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.01f, interval);
            fragmentDropAmount = 0;
            fragmentDropChance = 0f;
            controlAffectsMovement = controlMovement;
            controlAffectsAttack = controlAttack;
            _archetype = archetype;
            encounterBalance = null;
            _definitionResolved = true;
            if (IsRented)
            {
                CurrentHealth = maxHealth;
            }
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
            if (_contactCount <= 0 || _attackCooldown > 0f || contactDamage <= 0f)
            {
                return;
            }

            var idResult = PoolOwner?.NextAttackEventId();
            if (!idResult.HasValue || !idResult.Value.IsAccepted)
            {
                return;
            }

            for (var index = 0; index < _contactCount; index++)
            {
                var hitPosition = _contactTransforms[index] != null
                    ? (Vector2)_contactTransforms[index].position
                    : Position;
                ContactAttackRequested?.Invoke(new OSDamageEvent(
                    idResult.Value.Payload,
                    Time.frameCount,
                    RuntimeId,
                    _contactRuntimeIds[index],
                    _contactTargetKinds[index],
                    contactDamage,
                    hitPosition));
            }

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

                ApplyDefinition(definition);
                return;
            }

            if (TryApplySpecialDefinition(encounterBalance.EliteDefinition) ||
                TryApplySpecialDefinition(encounterBalance.BossDefinition))
            {
                return;
            }

            Debug.LogError($"[OUROBOROS][ENEMY] Definition '{definitionId}' was not found.", this);
        }

        private bool TryApplySpecialDefinition(OSEnemyDefinition definition)
        {
            if (definition == null ||
                !string.Equals(definition.Id, definitionId, StringComparison.Ordinal))
            {
                return false;
            }

            ApplyDefinition(definition);
            return true;
        }

        private void ApplyDefinition(OSEnemyDefinition definition)
        {
            _archetype = definition.Archetype;
            maxHealth = definition.MaxHealth;
            moveSpeed = definition.MoveSpeed;
            contactDamage = definition.ContactDamage;
            attackInterval = definition.AttackInterval;
            fragmentDropAmount = definition.DropTable.FragmentAmount;
            fragmentDropChance = definition.DropTable.FragmentChance;
            controlAffectsMovement = definition.ControlAffectsMovement;
            controlAffectsAttack = definition.ControlAffectsAttack;
            _definitionResolved = true;
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
