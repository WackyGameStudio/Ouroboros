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
        private const float ChargerTelegraphSeconds = 0.65f;
        private const float ChargerChargeSeconds = 0.55f;
        private const float ChargerRecoverySeconds = 0.55f;
        private const float ChargerSpeedMultiplier = 4.5f;
        private const float ShooterTelegraphSeconds = 0.45f;
        private const float ShooterMinimumRange = 5f;
        private const float ShooterMaximumRange = 7f;
        private const float ShooterProjectileRange = 13f;
        private const string EnemyProjectilePoolKey = "enemy_projectile";

        [Header("Definition")]
        [SerializeField] private OSEncounterBalanceData encounterBalance;
        [SerializeField] private string definitionId = "enemy_chaser";

        [Header("References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CircleCollider2D bodyCollider;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private LineRenderer telegraphLine;
        [SerializeField] private LineRenderer controlStatusRing;

        [Header("World safety")]
        [SerializeField] private LayerMask worldBlockerMask;
        [SerializeField, Min(1f)] private float reclaimDistance = 60f;
        [SerializeField, Min(0f)] private float separationRadius = 0.72f;
        [SerializeField, Min(0f)] private float separationStrength = 0.55f;

        [Header("Fallback definition")]
        [SerializeField, Min(0.01f)] private float maxHealth = 18f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.1f;
        [SerializeField, Min(0f)] private float contactDamage = 8f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1f;
        [SerializeField, Min(0)] private int fragmentDropAmount = 1;
        [SerializeField, Range(0f, 1f)] private float fragmentDropChance = 0.15f;
        [SerializeField, Min(0)] private int experienceDropAmount = 1;
        [SerializeField, Min(0)] private int healDropAmount = 10;
        [SerializeField, Range(0f, 1f)] private float healDropChance = 0.02f;
        [SerializeField] private bool controlAffectsMovement = true;
        [SerializeField] private bool controlAffectsAttack;

        private OSEnemyRegistry _registry;
        private OSGameSessionController _session;
        private Transform _target;
        private readonly Transform[] _contactTransforms = new Transform[MaxContactTargets];
        private readonly int[] _contactRuntimeIds = new int[MaxContactTargets];
        private readonly OSTargetKind[] _contactTargetKinds = new OSTargetKind[MaxContactTargets];
        private readonly RaycastHit2D[] _worldBlockerHits = new RaycastHit2D[4];
        private ContactFilter2D _worldBlockerFilter;
        private int _contactCount;
        private float _attackCooldown;
        private float _movementControlRemaining;
        private float _attackControlRemaining;
        private bool _deathConfirmed;
        private bool _definitionResolved;
        private OSEnemyArchetype _archetype = OSEnemyArchetype.Chaser;
        private OSEnemyBehaviorState _behaviorState = OSEnemyBehaviorState.Pursuit;
        private Vector2 _behaviorDirection = Vector2.right;
        private float _behaviorTimer;
        private float _specialAttackCooldown;
        private float _baseMaxHealth;
        private Color _baseColor = Color.white;
        private OSBossController _bossController;
        private float _controlVisualDuration;

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
        public int ExperienceDropAmount => experienceDropAmount;
        public int HealDropAmount => healDropAmount;
        public float HealDropChance => healDropChance;
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
        public string DefinitionId => definitionId;
        public OSEnemyBehaviorState BehaviorState => _behaviorState;
        public Vector2 TelegraphDirection => _behaviorDirection;
        public float BehaviorTimer => _behaviorTimer;
        public bool IsAuraAccelerated { get; private set; }
        public float EffectiveMoveSpeed => moveSpeed * (IsAuraAccelerated ? 1.2f : 1f);
        public float EffectiveAttackInterval => attackInterval * (IsAuraAccelerated ? 0.9f : 1f);
        public bool ControlStatusVisible => controlStatusRing != null && controlStatusRing.enabled;

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

        public OSRuleResult<float> ApplyWaveHealthMultiplier(float multiplier)
        {
            if (!IsRented || !float.IsFinite(multiplier) || multiplier < 1f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "enemy.wave_health.invalid_multiplier",
                    CurrentHealth);
            }

            maxHealth = Mathf.Max(0.01f, _baseMaxHealth * multiplier);
            CurrentHealth = maxHealth;
            return OSRuleResult<float>.Accepted(CurrentHealth, "enemy.wave_health.applied");
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

            var healthDamage = _bossController != null
                ? _bossController.AbsorbIncomingDamage(damage)
                : damage;
            if (healthDamage <= 0f)
            {
                return OSRuleResult<float>.Accepted(
                    CurrentHealth,
                    "enemy.damage.absorbed_by_shield");
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - healthDamage);
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

            _controlVisualDuration = Mathf.Max(_controlVisualDuration, duration);
            UpdateControlVisual();

            return OSRuleResult<float>.Accepted(duration, "enemy.control.accepted");
        }

        public void ConfigureReadability(LineRenderer controlRing)
        {
            controlStatusRing = controlRing;
            UpdateControlVisual();
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _deathConfirmed || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            var behaviorWasControlled = _movementControlRemaining > 0f;
            _movementControlRemaining = Mathf.Max(0f, _movementControlRemaining - deltaTime);
            _attackControlRemaining = Mathf.Max(0f, _attackControlRemaining - deltaTime);

            IsAuraAccelerated = _registry != null && _registry.IsInsideEliteAccelerationAura(this);
            UpdateAuraVisual();
            UpdateControlVisual();

            if (behaviorWasControlled)
            {
                return;
            }

            if (_target != null)
            {
                var targetOffset = (Vector2)_target.position - Position;
                if (targetOffset.sqrMagnitude > reclaimDistance * reclaimDistance)
                {
                    ReturnToPool();
                    return;
                }

                SimulateBehavior(targetOffset, deltaTime);
            }

            if (_archetype != OSEnemyArchetype.Shooter &&
                (_archetype != OSEnemyArchetype.Charger || _behaviorState == OSEnemyBehaviorState.Charge))
            {
                SimulateContactAttack(deltaTime);
            }
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
            experienceDropAmount = 0;
            healDropAmount = 0;
            healDropChance = 0f;
            controlAffectsMovement = controlMovement;
            controlAffectsAttack = controlAttack;
            _archetype = archetype;
            _baseMaxHealth = maxHealth;
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

        internal void ConfigureAllDropsForTesting(
            int experienceAmount,
            int fragmentAmount,
            float fragmentChance,
            int healAmount,
            float healChance)
        {
            experienceDropAmount = Mathf.Max(0, experienceAmount);
            fragmentDropAmount = Mathf.Max(0, fragmentAmount);
            fragmentDropChance = Mathf.Clamp01(fragmentChance);
            healDropAmount = Mathf.Max(0, healAmount);
            healDropChance = Mathf.Clamp01(healChance);
        }

        protected override void OnRented()
        {
            ResolveComponents();
            ResolveDefinition();
            _baseMaxHealth = Mathf.Max(0.01f, _baseMaxHealth > 0f ? _baseMaxHealth : maxHealth);
            maxHealth = _baseMaxHealth;
            CurrentHealth = maxHealth;
            _attackCooldown = 0f;
            _movementControlRemaining = 0f;
            _attackControlRemaining = 0f;
            _controlVisualDuration = 0f;
            _deathConfirmed = false;
            _behaviorState = _archetype == OSEnemyArchetype.Shooter
                ? OSEnemyBehaviorState.RangedHold
                : OSEnemyBehaviorState.Pursuit;
            _behaviorDirection = Vector2.right;
            _behaviorTimer = 0f;
            _specialAttackCooldown = _archetype == OSEnemyArchetype.Charger ? 0.8f : 0.35f;
            IsAuraAccelerated = false;
            EndContact();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            RegistryIndex = -1;
            if (bodyRenderer != null)
            {
                _baseColor = bodyRenderer.color;
            }
            SetTelegraphVisible(false);
            UpdateControlVisual();
            _bossController?.HandleRented();

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
            _controlVisualDuration = 0f;
            _deathConfirmed = false;
            _behaviorState = OSEnemyBehaviorState.Pursuit;
            _behaviorTimer = 0f;
            _specialAttackCooldown = 0f;
            IsAuraAccelerated = false;
            SetTelegraphVisible(false);
            UpdateControlVisual();
            if (bodyRenderer != null)
            {
                bodyRenderer.color = _baseColor;
            }
            _bossController?.HandleReturning();
            RegistryIndex = -1;
            Died = null;
            ContactAttackRequested = null;
        }

        private void MoveTowardTarget(Vector2 targetOffset, float deltaTime)
        {
            MoveInDirection(targetOffset, EffectiveMoveSpeed, deltaTime, true);
        }

        private void MoveInDirection(
            Vector2 desiredDirection,
            float speed,
            float deltaTime,
            bool applySeparation)
        {
            if (desiredDirection.sqrMagnitude <= MinimumDistanceSquared || speed <= 0f)
            {
                return;
            }

            var direction = desiredDirection.normalized;
            if (applySeparation && _registry != null && separationRadius > 0f && separationStrength > 0f)
            {
                var separation = _registry.CalculateSeparation(this, Position, separationRadius);
                direction = Vector2.ClampMagnitude(direction + separation * separationStrength, 1f);
            }

            MoveWithWorldBlockers(direction * (speed * deltaTime));
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

            _attackCooldown = EffectiveAttackInterval;
        }

        private void SimulateBehavior(Vector2 targetOffset, float deltaTime)
        {
            switch (_archetype)
            {
                case OSEnemyArchetype.Charger:
                    SimulateCharger(targetOffset, deltaTime);
                    break;
                case OSEnemyArchetype.Shooter:
                    SimulateShooter(targetOffset, deltaTime);
                    break;
                default:
                    _behaviorState = OSEnemyBehaviorState.Pursuit;
                    MoveTowardTarget(targetOffset, deltaTime);
                    break;
            }
        }

        private void SimulateCharger(Vector2 targetOffset, float deltaTime)
        {
            if (_behaviorState == OSEnemyBehaviorState.Telegraph)
            {
                _behaviorTimer = Mathf.Max(0f, _behaviorTimer - deltaTime);
                UpdateTelegraphLine();
                if (_behaviorTimer <= 0f)
                {
                    _behaviorState = OSEnemyBehaviorState.Charge;
                    _behaviorTimer = ChargerChargeSeconds;
                    SetTelegraphVisible(false);
                }

                return;
            }

            if (_behaviorState == OSEnemyBehaviorState.Charge)
            {
                MoveInDirection(_behaviorDirection, moveSpeed * ChargerSpeedMultiplier, deltaTime, false);
                _behaviorTimer = Mathf.Max(0f, _behaviorTimer - deltaTime);
                if (_behaviorTimer <= 0f)
                {
                    _behaviorState = OSEnemyBehaviorState.Recovery;
                    _behaviorTimer = ChargerRecoverySeconds;
                }

                return;
            }

            if (_behaviorState == OSEnemyBehaviorState.Recovery)
            {
                _behaviorTimer = Mathf.Max(0f, _behaviorTimer - deltaTime);
                if (_behaviorTimer <= 0f)
                {
                    _behaviorState = OSEnemyBehaviorState.Pursuit;
                    _specialAttackCooldown = EffectiveAttackInterval;
                }

                return;
            }

            _behaviorState = OSEnemyBehaviorState.Pursuit;
            _specialAttackCooldown = Mathf.Max(0f, _specialAttackCooldown - deltaTime);
            if (_attackControlRemaining <= 0f && _specialAttackCooldown <= 0f &&
                targetOffset.sqrMagnitude <= 8f * 8f &&
                targetOffset.sqrMagnitude > MinimumDistanceSquared)
            {
                _behaviorDirection = targetOffset.normalized;
                _behaviorState = OSEnemyBehaviorState.Telegraph;
                _behaviorTimer = ChargerTelegraphSeconds;
                SetTelegraphVisible(true);
                UpdateTelegraphLine();
                return;
            }

            MoveTowardTarget(targetOffset, deltaTime);
        }

        private void SimulateShooter(Vector2 targetOffset, float deltaTime)
        {
            if (_behaviorState == OSEnemyBehaviorState.Telegraph)
            {
                _behaviorTimer = Mathf.Max(0f, _behaviorTimer - deltaTime);
                UpdateTelegraphLine();
                if (_behaviorTimer <= 0f)
                {
                    var launched = TryLaunchEnemyProjectile();
                    _behaviorState = OSEnemyBehaviorState.RangedHold;
                    SetTelegraphVisible(false);
                    if (launched)
                    {
                        _specialAttackCooldown = EffectiveAttackInterval;
                    }
                }

                return;
            }

            _behaviorState = OSEnemyBehaviorState.RangedHold;
            var distance = targetOffset.magnitude;
            if (distance > ShooterMaximumRange)
            {
                MoveTowardTarget(targetOffset, deltaTime);
            }
            else if (distance < ShooterMinimumRange)
            {
                MoveInDirection(-targetOffset, EffectiveMoveSpeed, deltaTime, true);
            }
            else if (targetOffset.sqrMagnitude > MinimumDistanceSquared)
            {
                var tangent = new Vector2(-targetOffset.y, targetOffset.x);
                MoveInDirection(tangent, EffectiveMoveSpeed * 0.35f, deltaTime, true);
            }

            if (_attackControlRemaining > 0f)
            {
                return;
            }

            _specialAttackCooldown = Mathf.Max(0f, _specialAttackCooldown - deltaTime);
            if (_specialAttackCooldown <= 0f && distance <= ShooterProjectileRange &&
                targetOffset.sqrMagnitude > MinimumDistanceSquared)
            {
                _behaviorDirection = targetOffset.normalized;
                _behaviorState = OSEnemyBehaviorState.Telegraph;
                _behaviorTimer = ShooterTelegraphSeconds;
                SetTelegraphVisible(true);
                UpdateTelegraphLine();
            }
        }

        private bool TryLaunchEnemyProjectile()
        {
            var projectileLimit = encounterBalance != null ? encounterBalance.ProjectileLimit : 120;
            if (PoolOwner == null ||
                PoolOwner.GetActiveCount("head_projectile") +
                PoolOwner.GetActiveCount("body_control_projectile") +
                PoolOwner.GetActiveCount(EnemyProjectilePoolKey) >= projectileLimit)
            {
                return false;
            }

            var idResult = PoolOwner.NextAttackEventId();
            if (!idResult.IsAccepted)
            {
                return false;
            }

            var rent = PoolOwner.Rent(EnemyProjectilePoolKey, Position, Quaternion.identity);
            if (!rent.IsAccepted || rent.Payload is not OSEnemyProjectile projectile)
            {
                return false;
            }

            var launch = projectile.Launch(
                idResult.Payload,
                RuntimeId,
                _behaviorDirection,
                contactDamage,
                ShooterProjectileRange);
            if (launch.IsAccepted)
            {
                return true;
            }

            projectile.ReturnToPool();
            return false;
        }

        private void SetTelegraphVisible(bool visible)
        {
            if (telegraphLine != null)
            {
                telegraphLine.enabled = visible;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = visible ? Color.white : _baseColor;
            }
        }

        private void UpdateAuraVisual()
        {
            if (bodyRenderer == null || _behaviorState == OSEnemyBehaviorState.Telegraph)
            {
                return;
            }

            bodyRenderer.color = IsAuraAccelerated
                ? Color.Lerp(_baseColor, new Color(1f, 0.78f, 0.12f, 1f), 0.38f)
                : Mathf.Max(_movementControlRemaining, _attackControlRemaining) > 0f
                    ? Color.Lerp(_baseColor, new Color(0.12f, 0.95f, 1f, 1f), 0.42f)
                    : _baseColor;
        }

        private void UpdateControlVisual()
        {
            if (controlStatusRing == null)
            {
                return;
            }

            var remaining = Mathf.Max(_movementControlRemaining, _attackControlRemaining);
            if (remaining <= 0f || _controlVisualDuration <= 0f)
            {
                controlStatusRing.enabled = false;
                return;
            }

            const int maxSegments = 25;
            var normalized = Mathf.Clamp01(remaining / _controlVisualDuration);
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(normalized * (maxSegments - 1)) + 1, 2, maxSegments);
            controlStatusRing.positionCount = segmentCount;
            for (var index = 0; index < segmentCount; index++)
            {
                var normalizedIndex = index / (float)(maxSegments - 1);
                var angle = (Mathf.PI * 0.5f) - (normalizedIndex * Mathf.PI * 2f);
                controlStatusRing.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.72f);
            }

            controlStatusRing.enabled = true;
        }

        private void UpdateTelegraphLine()
        {
            if (telegraphLine == null || !telegraphLine.enabled)
            {
                return;
            }

            telegraphLine.positionCount = 2;
            telegraphLine.SetPosition(0, Position);
            telegraphLine.SetPosition(1, Position + (_behaviorDirection *
                (_archetype == OSEnemyArchetype.Charger ? 5.5f : ShooterProjectileRange)));
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
            bodyCollider ??= GetComponent<CircleCollider2D>();
            bodyRenderer ??= GetComponent<SpriteRenderer>();
            _bossController ??= GetComponent<OSBossController>();
            controlStatusRing ??= transform.Find("ControlStatusRing")?.GetComponent<LineRenderer>();
            _worldBlockerFilter = OSWorldBlockerMotion.CreateFilter(worldBlockerMask);
        }

        private void MoveWithWorldBlockers(Vector2 displacement)
        {
            var remaining = displacement;
            for (var iteration = 0; iteration < 2; iteration++)
            {
                var distance = remaining.magnitude;
                if (distance <= MinimumDistanceSquared)
                {
                    break;
                }

                var direction = remaining / distance;
                if (!OSWorldBlockerMotion.TryGetClosestHit(
                        bodyCollider,
                        direction,
                        distance,
                        _worldBlockerFilter,
                        _worldBlockerHits,
                        out var blockerHit))
                {
                    body.position += remaining;
                    break;
                }

                var safeDistance = Mathf.Clamp(
                    blockerHit.distance - OSWorldBlockerMotion.SkinWidth,
                    0f,
                    distance);
                var safeMove = direction * safeDistance;
                body.position += safeMove;

                var unresolved = remaining - safeMove;
                var intoSurface = Vector2.Dot(unresolved, blockerHit.normal);
                if (intoSurface < 0f)
                {
                    unresolved -= blockerHit.normal * intoSurface;
                }

                if (safeDistance <= MinimumDistanceSquared &&
                    unresolved.sqrMagnitude >= remaining.sqrMagnitude - MinimumDistanceSquared)
                {
                    break;
                }

                remaining = unresolved;
            }
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
            experienceDropAmount = definition.DropTable.ExperienceAmount;
            healDropAmount = definition.DropTable.HealAmount;
            healDropChance = definition.DropTable.HealChance;
            controlAffectsMovement = definition.ControlAffectsMovement;
            controlAffectsAttack = definition.ControlAffectsAttack;
            _baseMaxHealth = maxHealth;
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
            experienceDropAmount = Mathf.Max(0, experienceDropAmount);
            healDropAmount = Mathf.Max(0, healDropAmount);
            healDropChance = Mathf.Clamp01(healDropChance);
            ResolveComponents();
        }
    }
}
