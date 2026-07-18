using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSBodyAttackFireFeedback
    {
        public OSBodyAttackFireFeedback(
            int sourceStableId,
            int targetRuntimeId,
            int projectileRuntimeId,
            Vector2 origin,
            Vector2 direction)
        {
            SourceStableId = sourceStableId;
            TargetRuntimeId = targetRuntimeId;
            ProjectileRuntimeId = projectileRuntimeId;
            Origin = origin;
            Direction = direction;
        }

        public int SourceStableId { get; }
        public int TargetRuntimeId { get; }
        public int ProjectileRuntimeId { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
    }

    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class OSAttackBodyRole : MonoBehaviour, IOSProjectileFeedbackSink
    {
        private const int Capacity = 64;
        private const float DefaultRange = 6f;
        private const float DefaultDamage = 6f;
        private const float DefaultInterval = 1f;

        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private string projectilePoolKey = "head_projectile";

        private int[] _stableIds = new int[Capacity];
        private float[] _cooldowns = new float[Capacity];
        private int[] _targetRuntimeIds = new int[Capacity];
        private int[] _nextStableIds = new int[Capacity];
        private float[] _nextCooldowns = new float[Capacity];
        private int[] _nextTargetRuntimeIds = new int[Capacity];
        private int _stateCount;
        private bool _subscribed;

        private float _testRange = -1f;
        private float _testDamage = -1f;
        private float _testInterval = -1f;

        public event Action<OSBodyAttackFireFeedback> Fired;
        public event Action<OSDamageEvent> HitConfirmed;

        public int ActiveSegmentCount => _stateCount;
        public int ShotsFired { get; private set; }
        public int HitsConfirmed { get; private set; }
        public float Range => EffectiveRange;
        public float Damage => EffectiveDamage;
        public float Interval => EffectiveInterval;

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
            float damage = DefaultDamage,
            float interval = DefaultInterval,
            string poolKey = "head_projectile")
        {
            Unsubscribe();
            roleRegistry = roles;
            enemyRegistry = enemies;
            poolRegistry = pool;
            sessionController = session;
            bodyBalance = null;
            projectilePoolKey = poolKey;
            _testRange = Mathf.Max(0.01f, range);
            _testDamage = Mathf.Max(0.01f, damage);
            _testInterval = Mathf.Max(0.01f, interval);
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
                if (_cooldowns[index] > 0f)
                {
                    continue;
                }

                TryFire(index);
            }
        }

        internal float GetCooldownForTesting(int stableId)
        {
            var index = FindStateIndex(stableId);
            return index >= 0 ? _cooldowns[index] : -1f;
        }

        public void OnProjectileHitConfirmed(OSDamageEvent damageEvent, bool targetDefeated)
        {
            HitsConfirmed++;
            HitConfirmed?.Invoke(damageEvent);
        }

        private float EffectiveRange => _testRange >= 0f
            ? _testRange
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Attack)?.Range ?? DefaultRange;
        private float EffectiveDamage => _testDamage >= 0f
            ? _testDamage
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Attack)?.Damage ?? DefaultDamage;
        private float EffectiveInterval => _testInterval >= 0f
            ? _testInterval
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Attack)?.Interval ?? DefaultInterval;

        private bool TryFire(int stateIndex)
        {
            if (roleRegistry == null || enemyRegistry == null || poolRegistry == null)
            {
                return false;
            }

            var segment = roleRegistry.FindByStableId(OSBodyRoleType.Attack, _stableIds[stateIndex]);
            if (segment?.View == null)
            {
                return false;
            }

            var origin = (Vector2)segment.View.transform.position;
            var target = enemyRegistry.FindNearestTarget(
                origin,
                EffectiveRange,
                _targetRuntimeIds[stateIndex]);
            if (target == null)
            {
                _targetRuntimeIds[stateIndex] = 0;
                return false;
            }

            _targetRuntimeIds[stateIndex] = target.RuntimeId;
            var direction = target.Position - origin;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.right;
            }

            var rentResult = poolRegistry.Rent(projectilePoolKey, origin, Quaternion.identity);
            if (!rentResult.IsAccepted || rentResult.Payload is not OSProjectile projectile)
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
                EffectiveDamage,
                EffectiveRange,
                0,
                this);
            if (!launch.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            _cooldowns[stateIndex] = EffectiveInterval;
            ShotsFired++;
            Fired?.Invoke(new OSBodyAttackFireFeedback(
                segment.StableId,
                target.RuntimeId,
                projectile.RuntimeId,
                origin,
                direction.normalized));
            return true;
        }

        private void SynchronizeStates()
        {
            Array.Clear(_nextStableIds, 0, _nextStableIds.Length);
            Array.Clear(_nextCooldowns, 0, _nextCooldowns.Length);
            Array.Clear(_nextTargetRuntimeIds, 0, _nextTargetRuntimeIds.Length);

            var nextCount = Mathf.Min(roleRegistry?.GetCount(OSBodyRoleType.Attack) ?? 0, Capacity);
            for (var index = 0; index < nextCount; index++)
            {
                var segment = roleRegistry.GetSegment(OSBodyRoleType.Attack, index);
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
            Array.Clear(_stableIds, 0, _stableIds.Length);
            Array.Clear(_cooldowns, 0, _cooldowns.Length);
            Array.Clear(_targetRuntimeIds, 0, _targetRuntimeIds.Length);
            _stateCount = 0;
            ShotsFired = 0;
            HitsConfirmed = 0;
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
