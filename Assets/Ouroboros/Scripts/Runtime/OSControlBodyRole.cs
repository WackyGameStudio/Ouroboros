using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSControlFireFeedback
    {
        public OSControlFireFeedback(int sourceStableId, int targetRuntimeId, int projectileRuntimeId)
        {
            SourceStableId = sourceStableId;
            TargetRuntimeId = targetRuntimeId;
            ProjectileRuntimeId = projectileRuntimeId;
        }

        public int SourceStableId { get; }
        public int TargetRuntimeId { get; }
        public int ProjectileRuntimeId { get; }
    }

    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class OSControlBodyRole : MonoBehaviour, IOSControlProjectileFeedbackSink
    {
        private const int Capacity = 64;
        private const float DefaultRange = 6f;
        private const float DefaultInterval = 4f;
        private const float DefaultNormalDuration = 1f;
        private const float DefaultEliteDuration = 0.5f;

        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private string projectilePoolKey = "body_control_projectile";

        private int[] _stableIds = new int[Capacity];
        private float[] _cooldowns = new float[Capacity];
        private int[] _targetRuntimeIds = new int[Capacity];
        private int[] _nextStableIds = new int[Capacity];
        private float[] _nextCooldowns = new float[Capacity];
        private int[] _nextTargetRuntimeIds = new int[Capacity];
        private int _stateCount;
        private bool _subscribed;

        private float _testRange = -1f;
        private float _testInterval = -1f;
        private float _testNormalDuration = -1f;
        private float _testEliteDuration = -1f;
        private float _cooldownMultiplier = 1f;

        public event Action<OSControlFireFeedback> Fired;
        public event Action<OSDamageEvent> ControlApplied;

        public int ActiveSegmentCount => _stateCount;
        public int ShotsFired { get; private set; }
        public int ControlsApplied { get; private set; }

        private void OnEnable()
        {
            Subscribe();
            SynchronizeStates();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        internal void ConfigureForTesting(
            OSBodyRoleRegistry roles,
            OSEnemyRegistry enemies,
            OSPoolRegistry pool,
            OSGameSessionController session,
            float range = DefaultRange,
            float interval = DefaultInterval,
            float normalDuration = DefaultNormalDuration,
            float eliteDuration = DefaultEliteDuration,
            string poolKey = "body_control_projectile")
        {
            Unsubscribe();
            roleRegistry = roles;
            enemyRegistry = enemies;
            poolRegistry = pool;
            sessionController = session;
            bodyBalance = null;
            projectilePoolKey = poolKey;
            _testRange = Mathf.Max(0.01f, range);
            _testInterval = Mathf.Max(0.01f, interval);
            _testNormalDuration = Mathf.Max(0.01f, normalDuration);
            _testEliteDuration = Mathf.Max(0.01f, eliteDuration);
            ResetState();
            Subscribe();
            SynchronizeStates();
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f ||
                sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            for (var index = 0; index < _stateCount; index++)
            {
                _cooldowns[index] = Mathf.Max(0f, _cooldowns[index] - deltaTime);
                if (_cooldowns[index] <= 0f)
                {
                    TryFire(index);
                }
            }
        }

        public void OnControlHitConfirmed(OSDamageEvent controlEvent, float appliedDuration)
        {
            ControlsApplied++;
            ControlApplied?.Invoke(controlEvent);
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _cooldownMultiplier = Mathf.Clamp(modifiers.RoleCooldownMultiplier, 0.5f, 1f);
            for (var index = 0; index < _stateCount; index++)
            {
                _cooldowns[index] = Mathf.Min(_cooldowns[index], EffectiveInterval);
            }
        }

        private float EffectiveRange => _testRange >= 0f
            ? _testRange
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Control)?.Range ?? DefaultRange;
        private float EffectiveInterval => _testInterval >= 0f
            ? _testInterval
            : Mathf.Max(
                0.15f,
                (bodyBalance?.GetRoleDefinition(OSBodyRoleType.Control)?.Interval ?? DefaultInterval) *
                _cooldownMultiplier);
        private float EffectiveNormalDuration => _testNormalDuration >= 0f
            ? _testNormalDuration
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Control)?.NormalControlDuration ??
              DefaultNormalDuration;
        private float EffectiveEliteDuration => _testEliteDuration >= 0f
            ? _testEliteDuration
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Control)?.EliteControlDuration ??
              DefaultEliteDuration;

        private bool TryFire(int index)
        {
            var segment = roleRegistry?.FindByStableId(OSBodyRoleType.Control, _stableIds[index]);
            if (segment?.View == null || enemyRegistry == null || poolRegistry == null)
            {
                return false;
            }

            var origin = (Vector2)segment.View.transform.position;
            var target = enemyRegistry.FindNearestTarget(
                origin,
                EffectiveRange,
                _targetRuntimeIds[index]);
            if (target == null)
            {
                _targetRuntimeIds[index] = 0;
                return false;
            }

            var direction = target.Position - origin;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.right;
            }

            var rent = poolRegistry.Rent(projectilePoolKey, origin, Quaternion.identity);
            if (!rent.IsAccepted || rent.Payload is not OSControlProjectile projectile)
            {
                return false;
            }

            var eventId = poolRegistry.NextAttackEventId();
            if (!eventId.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            var launch = projectile.Launch(
                eventId.Payload,
                segment.StableId,
                direction,
                0f,
                EffectiveRange,
                EffectiveNormalDuration,
                EffectiveEliteDuration,
                this);
            if (!launch.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            _targetRuntimeIds[index] = target.RuntimeId;
            _cooldowns[index] = EffectiveInterval;
            ShotsFired++;
            Fired?.Invoke(new OSControlFireFeedback(
                segment.StableId,
                target.RuntimeId,
                projectile.RuntimeId));
            return true;
        }

        private void SynchronizeStates()
        {
            Array.Clear(_nextStableIds, 0, Capacity);
            Array.Clear(_nextCooldowns, 0, Capacity);
            Array.Clear(_nextTargetRuntimeIds, 0, Capacity);

            var nextCount = Mathf.Min(roleRegistry?.GetCount(OSBodyRoleType.Control) ?? 0, Capacity);
            for (var index = 0; index < nextCount; index++)
            {
                var segment = roleRegistry.GetSegment(OSBodyRoleType.Control, index);
                if (segment == null)
                {
                    continue;
                }

                _nextStableIds[index] = segment.StableId;
                var previousIndex = FindStateIndex(segment.StableId);
                if (previousIndex >= 0)
                {
                    _nextCooldowns[index] = _cooldowns[previousIndex];
                    _nextTargetRuntimeIds[index] = _targetRuntimeIds[previousIndex];
                }
            }

            Swap(ref _stableIds, ref _nextStableIds);
            Swap(ref _cooldowns, ref _nextCooldowns);
            Swap(ref _targetRuntimeIds, ref _nextTargetRuntimeIds);
            _stateCount = nextCount;
        }

        private int FindStateIndex(int stableId)
        {
            for (var index = 0; index < _stateCount; index++)
            {
                if (_stableIds[index] == stableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ResetState()
        {
            Array.Clear(_stableIds, 0, Capacity);
            Array.Clear(_cooldowns, 0, Capacity);
            Array.Clear(_targetRuntimeIds, 0, Capacity);
            _stateCount = 0;
            ShotsFired = 0;
            ControlsApplied = 0;
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged += SynchronizeStates;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleStateChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged -= SynchronizeStates;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleStateChanged;
            }

            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                poolRegistry?.ReturnAll(projectilePoolKey);
                ResetState();
                SynchronizeStates();
            }
        }

        private static void Swap<T>(ref T[] left, ref T[] right)
        {
            (left, right) = (right, left);
        }
    }
}
