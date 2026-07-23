using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSBombSnapshot
    {
        public OSBombSnapshot(
            int requestId,
            int bodyCountBefore,
            int consumedBodyCount,
            Vector2 start,
            Vector2 center,
            Vector2 forward,
            float radius,
            float damage,
            float cooldown,
            OSBombTurnSide turnSide)
        {
            RequestId = requestId;
            BodyCountBefore = bodyCountBefore;
            ConsumedBodyCount = consumedBodyCount;
            Start = start;
            Center = center;
            Forward = forward;
            Radius = radius;
            Damage = damage;
            Cooldown = cooldown;
            TurnSide = turnSide;
        }

        public int RequestId { get; }
        public int BodyCountBefore { get; }
        public int ConsumedBodyCount { get; }
        public Vector2 Start { get; }
        public Vector2 Center { get; }
        public Vector2 Forward { get; }
        public float Radius { get; }
        public float Damage { get; }
        public float Cooldown { get; }
        public OSBombTurnSide TurnSide { get; }
    }

    public readonly struct OSBombResolution
    {
        public OSBombResolution(
            int requestId,
            OSResultCode code,
            int consumedBodyCount,
            int hitCount,
            int killCount)
        {
            RequestId = requestId;
            Code = code;
            ConsumedBodyCount = consumedBodyCount;
            HitCount = hitCount;
            KillCount = killCount;
        }

        public int RequestId { get; }
        public OSResultCode Code { get; }
        public int ConsumedBodyCount { get; }
        public int HitCount { get; }
        public int KillCount { get; }
        public bool WasCancelled => Code != OSResultCode.Accepted;
    }

    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class OSBombController : MonoBehaviour
    {
        private const int HitCapacity = 256;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private OSBombView bombView;
        [SerializeField] private LayerMask enemyHurtboxMask;

        private readonly Collider2D[] _colliderHits = new Collider2D[HitCapacity];
        private readonly OSEnemyController[] _enemyHits = new OSEnemyController[HitCapacity];
        private readonly int[] _enemyRuntimeIds = new int[HitCapacity];
        private ContactFilter2D _enemyHurtboxFilter;
        private OSUpgradeModifiers _upgradeModifiers = OSUpgradeModifiers.Default;
        private bool _active;
        private bool _subscribed;
        private bool _orbitCompletionPending;
        private bool _orbitInterrupted;
        private bool _gatherCompletionPending;
        private int _requestId;
        private int _consumedBodyCount;
        private int _hitCount;
        private int _killCount;
        private float _cooldownRemaining;
        private float _gatherRemaining;
        private Vector2 _start;
        private Vector2 _center;
        private Vector2 _forward = Vector2.right;
        private float _radius;
        private OSBombTurnSide _turnSide;

        private int _testMinimumBodyCount;
        private float _testConsumeRate = -1f;
        private float _testDrawDuration = -1f;
        private float _testGatherDuration = -1f;
        private float _testDamage = -1f;
        private float _testCooldown = -1f;

        public event Action<OSBombSnapshot> BombStarted;
        public event Action<OSBombResolution> BombExploded;
        public event Action<OSBombResolution> BombCompleted;
        public event Action<OSResultCode, string> RequestRejected;

        public OSBombPhase Phase { get; private set; }
        public bool IsActive => _active;
        public bool IsReady => !_active && _cooldownRemaining <= 0f &&
                               sessionController != null &&
                               sessionController.State == OSSessionState.Combat &&
                               bodyChain != null && bodyChain.ActiveCount >= MinimumBodyCount;
        public float CooldownRemaining => _cooldownRemaining;
        public float Cooldown => EffectiveCooldown;
        public float Damage => EffectiveDamage;
        public float Radius => _radius;
        public float DrawRemaining => Phase == OSBombPhase.DrawingCircle
            ? DrawDuration * (1f - (playerController?.BombOrbitProgress ?? 0f))
            : 0f;
        public float GatherRemaining => Phase == OSBombPhase.Gathering ? _gatherRemaining : 0f;
        public int MinimumBodyCount => _testMinimumBodyCount > 0
            ? _testMinimumBodyCount
            : bodyBalance != null ? bodyBalance.Bomb.MinimumBodyCount : OSBombMath.DefaultMinimumBodyCount;
        public float ConsumeRate => _testConsumeRate >= 0f
            ? _testConsumeRate
            : bodyBalance != null ? bodyBalance.Bomb.ConsumeRate : OSBombMath.DefaultConsumeRate;
        public float DrawDuration => _testDrawDuration >= 0f
            ? _testDrawDuration
            : bodyBalance != null ? bodyBalance.Bomb.DrawDuration : OSBombMath.DefaultDrawDuration;
        public float GatherDuration => _testGatherDuration >= 0f
            ? _testGatherDuration
            : bodyBalance != null ? bodyBalance.Bomb.GatherDuration : OSBombMath.DefaultGatherDuration;

        private float EffectiveDamage => OSBombMath.CalculateDamage(
            _testDamage >= 0f
                ? _testDamage
                : bodyBalance != null ? bodyBalance.Bomb.Damage : OSBombMath.DefaultDamage,
            _upgradeModifiers.BombDamageMultiplier);
        private float EffectiveCooldown => OSBombMath.CalculateCooldown(
            _testCooldown >= 0f
                ? _testCooldown
                : bodyBalance != null ? bodyBalance.Bomb.Cooldown : OSBombMath.DefaultCooldown,
            _upgradeModifiers.BombCooldownDelta);

        private void Awake()
        {
            RebuildContactFilter();
            Subscribe();
        }

        private void OnEnable()
        {
            RebuildContactFilter();
            Subscribe();
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            var returnToCombat = sessionController != null &&
                                 sessionController.State == OSSessionState.Bomb;
            Unsubscribe();
            CancelActive(OSResultCode.RejectedState, false, returnToCombat);
        }

        public void Configure(
            OSGameSessionController session,
            OSPlayerController player,
            OSPlayerHealth health,
            OSBodyChain chain,
            OSBodyGrowthController growth,
            OSEnemyRegistry enemies,
            OSBodyBalanceData balance,
            OSBombView view,
            LayerMask hurtboxMask)
        {
            Unsubscribe();
            sessionController = session;
            playerController = player;
            playerHealth = health;
            bodyChain = chain;
            bodyGrowth = growth;
            enemyRegistry = enemies;
            bodyBalance = balance;
            bombView = view;
            enemyHurtboxMask = hurtboxMask;
            RebuildContactFilter();
            Subscribe();
        }

        public OSRuleResult<int> RequestBomb()
        {
            if (_active || sessionController == null ||
                sessionController.State != OSSessionState.Combat)
            {
                return Reject(OSResultCode.RejectedState, "bomb.request.invalid_state");
            }

            if (_cooldownRemaining > 0f)
            {
                return Reject(OSResultCode.RejectedRequirement, "bomb.request.cooldown");
            }

            if (playerController == null || playerHealth == null || bodyChain == null)
            {
                return Reject(OSResultCode.ConfigurationError, "bomb.request.missing_dependency");
            }

            var bodyCountBefore = bodyChain.ActiveCount;
            if (bodyCountBefore < MinimumBodyCount)
            {
                return Reject(OSResultCode.RejectedRequirement, "bomb.request.body_requirement");
            }

            var consumedBodyCount = OSBombMath.CalculateConsumeCount(bodyCountBefore, ConsumeRate);
            var remainingBodyCount = bodyCountBefore - consumedBodyCount;
            if (consumedBodyCount <= 0 || remainingBodyCount <= 0)
            {
                return Reject(OSResultCode.RejectedRequirement, "bomb.request.invalid_cost");
            }

            var radius = OSBombMath.CalculateRadius(
                remainingBodyCount,
                bodyChain.SegmentSpacing);
            var forward = OSBodyDashMath.ResolveDirection(
                playerController.MoveInput,
                playerController.LastDirection);
            var turnSide = ResolveTurnSide(forward);
            Physics2D.SyncTransforms();
            if (!playerController.CanTraceBombCircle(
                    forward,
                    radius,
                    DrawDuration,
                    turnSide,
                    out var center))
            {
                return Reject(OSResultCode.RejectedRange, "bomb.request.blocked_circle");
            }

            if (!playerController.TryStartBombOrbit(
                    forward,
                    radius,
                    DrawDuration,
                    turnSide))
            {
                return Reject(OSResultCode.ConfigurationError, "bomb.request.player_rejected");
            }

            var stateResult = sessionController.BeginBomb();
            if (!stateResult.IsAccepted)
            {
                playerController.CancelBombOrbit();
                return Reject(stateResult.Code, stateResult.ReasonKey);
            }

            var consumeResult = bodyChain.ConsumeBombTail(consumedBodyCount);
            if (!consumeResult.IsAccepted)
            {
                playerController.CancelBombOrbit();
                sessionController.CompleteBomb();
                return Reject(consumeResult.Code, consumeResult.ReasonKey);
            }

            _active = true;
            Phase = OSBombPhase.DrawingCircle;
            _requestId++;
            _consumedBodyCount = consumedBodyCount;
            _hitCount = 0;
            _killCount = 0;
            _cooldownRemaining = EffectiveCooldown;
            _gatherRemaining = 0f;
            _start = playerController.Position;
            _center = center;
            _forward = forward;
            _radius = radius;
            _turnSide = turnSide;
            playerHealth.SetAbilityInvulnerable(true);
            bombView?.BeginDrawing(_start, _forward, _radius, _turnSide);

            var snapshot = new OSBombSnapshot(
                _requestId,
                bodyCountBefore,
                consumedBodyCount,
                _start,
                _center,
                _forward,
                _radius,
                EffectiveDamage,
                EffectiveCooldown,
                _turnSide);
            BombStarted?.Invoke(snapshot);
            return OSRuleResult<int>.Accepted(consumedBodyCount, "bomb.request.accepted");
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _upgradeModifiers = modifiers;
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            OSPlayerController player,
            OSPlayerHealth health,
            OSBodyChain chain,
            OSEnemyRegistry enemies = null,
            LayerMask hurtboxMask = default,
            int minimumBodyCount = OSBombMath.DefaultMinimumBodyCount,
            float consumeRate = OSBombMath.DefaultConsumeRate,
            float drawDuration = OSBombMath.DefaultDrawDuration,
            float gatherDuration = OSBombMath.DefaultGatherDuration,
            float damage = OSBombMath.DefaultDamage,
            float cooldown = OSBombMath.DefaultCooldown)
        {
            Unsubscribe();
            sessionController = session;
            playerController = player;
            playerHealth = health;
            bodyChain = chain;
            bodyGrowth = null;
            enemyRegistry = enemies;
            bodyBalance = null;
            bombView = null;
            enemyHurtboxMask = hurtboxMask;
            _testMinimumBodyCount = Mathf.Max(1, minimumBodyCount);
            _testConsumeRate = Mathf.Clamp01(consumeRate);
            _testDrawDuration = Mathf.Max(0.01f, drawDuration);
            _testGatherDuration = Mathf.Max(0.01f, gatherDuration);
            _testDamage = Mathf.Max(0.01f, damage);
            _testCooldown = Mathf.Max(OSBombMath.MinimumCooldown, cooldown);
            _active = false;
            Phase = OSBombPhase.Inactive;
            _cooldownRemaining = 0f;
            RebuildContactFilter();
            Subscribe();
        }

        internal void SimulateForTesting(float deltaTime)
        {
            Simulate(deltaTime);
        }

        private void Simulate(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            if (sessionController != null && sessionController.IsSimulationRunning &&
                _cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            }

            if (!_active)
            {
                return;
            }

            if (Phase == OSBombPhase.DrawingCircle)
            {
                bombView?.SetDrawingProgress(
                    playerController != null && playerController.IsBombOrbitActive
                        ? playerController.BombOrbitProgress
                        : 1f);
            }
            else if (Phase == OSBombPhase.Gathering)
            {
                _gatherRemaining = Mathf.Max(0f, _gatherRemaining - deltaTime);
            }

            if (_orbitCompletionPending)
            {
                var interrupted = _orbitInterrupted;
                _orbitCompletionPending = false;
                _orbitInterrupted = false;
                if (interrupted)
                {
                    CancelActive(OSResultCode.CancelledMissingSource, true, true);
                    return;
                }

                ResolveExplosionAndBeginGather();
            }

            if (_gatherCompletionPending)
            {
                _gatherCompletionPending = false;
                CompleteBomb();
            }
        }

        private OSBombTurnSide ResolveTurnSide(Vector2 forward)
        {
            var leftCount = 0;
            var rightCount = 0;
            var signedLateralSum = 0f;
            var headPosition = playerController != null ? playerController.Position : Vector2.zero;
            for (var index = 0; index < bodyChain.ActiveCount; index++)
            {
                var segment = bodyChain.GetActiveSegment(index);
                if (segment?.View == null)
                {
                    continue;
                }

                var side = OSBombMath.ClassifySide(
                    forward,
                    (Vector2)segment.View.transform.position - headPosition,
                    out var signedLateral);
                signedLateralSum += signedLateral;
                if (side > 0)
                {
                    leftCount++;
                }
                else if (side < 0)
                {
                    rightCount++;
                }
            }

            return OSBombMath.ResolveTurnSide(leftCount, rightCount, signedLateralSum);
        }

        private void ResolveExplosionAndBeginGather()
        {
            _hitCount = 0;
            _killCount = 0;
            var uniqueEnemyCount = CollectExplosionTargets();
            for (var index = 0; index < uniqueEnemyCount; index++)
            {
                var enemy = _enemyHits[index];
                if (enemy == null || !enemy.IsRented || enemy.CurrentHealth <= 0f)
                {
                    continue;
                }

                var result = enemy.TryApplyDamage(EffectiveDamage);
                if (!result.IsAccepted)
                {
                    continue;
                }

                _hitCount++;
                if (!enemy.IsRented || result.Payload <= 0f)
                {
                    _killCount++;
                }
            }

            Array.Clear(_enemyHits, 0, uniqueEnemyCount);
            Array.Clear(_enemyRuntimeIds, 0, uniqueEnemyCount);
            bombView?.Explode(_center, _radius);
            var resolution = CurrentResolution(OSResultCode.Accepted);
            BombExploded?.Invoke(resolution);

            Phase = OSBombPhase.Gathering;
            _gatherRemaining = GatherDuration;
            bodyChain.BeginBombPathConvergence(GatherDuration);
        }

        private int CollectExplosionTargets()
        {
            if (enemyRegistry == null || enemyHurtboxMask.value == 0)
            {
                return 0;
            }

            var colliderCount = Physics2D.OverlapCircle(
                _center,
                _radius,
                _enemyHurtboxFilter,
                _colliderHits);
            var uniqueCount = 0;
            for (var index = 0; index < colliderCount && uniqueCount < HitCapacity; index++)
            {
                var collider = _colliderHits[index];
                var enemy = collider?.attachedRigidbody != null
                    ? collider.attachedRigidbody.GetComponent<OSEnemyController>()
                    : collider?.GetComponentInParent<OSEnemyController>();
                if (enemy == null || !enemy.IsRented || enemy.CurrentHealth <= 0f ||
                    enemy.RegistryIndex < 0 || enemy.RegistryIndex >= enemyRegistry.Count ||
                    enemyRegistry.GetAt(enemy.RegistryIndex) != enemy ||
                    ContainsRuntimeId(enemy.RuntimeId, uniqueCount))
                {
                    continue;
                }

                _enemyRuntimeIds[uniqueCount] = enemy.RuntimeId;
                _enemyHits[uniqueCount] = enemy;
                uniqueCount++;
            }

            Array.Clear(_colliderHits, 0, colliderCount);
            return uniqueCount;
        }

        private bool ContainsRuntimeId(int runtimeId, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (_enemyRuntimeIds[index] == runtimeId)
                {
                    return true;
                }
            }

            return false;
        }

        private void CompleteBomb()
        {
            var resolution = CurrentResolution(OSResultCode.Accepted);
            _active = false;
            Phase = OSBombPhase.Inactive;
            _gatherRemaining = 0f;
            playerHealth?.SetAbilityInvulnerable(false);
            bombView?.Complete();
            sessionController?.CompleteBomb();
            BombCompleted?.Invoke(resolution);

            if (sessionController != null && sessionController.State == OSSessionState.Combat)
            {
                bodyGrowth?.ResumeDeferredAfterCapacityChange();
                sessionController.ProcessPendingSelection();
            }
        }

        private void CancelActive(OSResultCode code, bool publish, bool returnToCombat)
        {
            if (!_active)
            {
                playerHealth?.SetAbilityInvulnerable(false);
                return;
            }

            var resolution = CurrentResolution(code);
            _active = false;
            Phase = OSBombPhase.Inactive;
            _orbitCompletionPending = false;
            _gatherCompletionPending = false;
            _gatherRemaining = 0f;
            playerController?.CancelBombOrbit();
            bodyChain?.CancelBombPathConvergence();
            playerHealth?.SetAbilityInvulnerable(false);
            bombView?.Cancel();
            if (returnToCombat && sessionController != null &&
                sessionController.State == OSSessionState.Bomb)
            {
                sessionController.CompleteBomb();
            }

            if (publish)
            {
                BombCompleted?.Invoke(resolution);
            }
        }

        private OSBombResolution CurrentResolution(OSResultCode code)
        {
            return new OSBombResolution(
                _requestId,
                code,
                _consumedBodyCount,
                _hitCount,
                _killCount);
        }

        private OSRuleResult<int> Reject(OSResultCode code, string reasonKey)
        {
            RequestRejected?.Invoke(code, reasonKey);
            return OSRuleResult<int>.Rejected(code, reasonKey, 0);
        }

        private void HandleBombOrbitCompleted(bool interrupted)
        {
            if (!_active || Phase != OSBombPhase.DrawingCircle)
            {
                return;
            }

            _orbitInterrupted = interrupted;
            _orbitCompletionPending = true;
        }

        private void HandleBombPathConvergenceCompleted()
        {
            if (_active && Phase == OSBombPhase.Gathering)
            {
                _gatherCompletionPending = true;
            }
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                _cooldownRemaining = 0f;
                CancelActive(OSResultCode.RejectedState, false, false);
            }
            else if (_active && current != OSSessionState.Bomb)
            {
                CancelActive(OSResultCode.RejectedState, true, false);
            }
        }

        private void HandleBombRequested()
        {
            RequestBomb();
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.BombOrbitCompleted += HandleBombOrbitCompleted;
            }

            if (bodyChain != null)
            {
                bodyChain.BombPathConvergenceCompleted += HandleBombPathConvergenceCompleted;
            }

            if (sessionController != null)
            {
                sessionController.BombRequested += HandleBombRequested;
                sessionController.StateChanged += HandleSessionStateChanged;
            }

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
                playerController.BombOrbitCompleted -= HandleBombOrbitCompleted;
            }

            if (bodyChain != null)
            {
                bodyChain.BombPathConvergenceCompleted -= HandleBombPathConvergenceCompleted;
            }

            if (sessionController != null)
            {
                sessionController.BombRequested -= HandleBombRequested;
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void RebuildContactFilter()
        {
            _enemyHurtboxFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = enemyHurtboxMask,
                useTriggers = true
            };
        }

        private void OnValidate()
        {
            RebuildContactFilter();
        }
    }
}
