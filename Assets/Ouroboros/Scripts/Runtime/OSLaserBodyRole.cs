using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSLaserSnapshot
    {
        public OSLaserSnapshot(
            int sourceStableId,
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            float remaining)
        {
            SourceStableId = sourceStableId;
            Origin = origin;
            Direction = direction;
            Length = length;
            Width = width;
            Remaining = remaining;
        }

        public int SourceStableId { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public Vector2 End => Origin + (Direction * Length);
        public float Length { get; }
        public float Width { get; }
        public float Remaining { get; }
    }

    public readonly struct OSLaserResolution
    {
        public OSLaserResolution(int sourceStableId, int hitCount, int killCount, bool cancelled)
        {
            SourceStableId = sourceStableId;
            HitCount = hitCount;
            KillCount = killCount;
            Cancelled = cancelled;
        }

        public int SourceStableId { get; }
        public int HitCount { get; }
        public int KillCount { get; }
        public bool Cancelled { get; }
    }

    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class OSLaserBodyRole : MonoBehaviour
    {
        private const int Capacity = 64;
        private const int BeamOverlapCapacity = 256;
        private const float DefaultLength = 7f;
        private const float DefaultDamage = 12f;
        private const float DefaultInterval = 2.5f;
        private const float DefaultWidth = 0.35f;
        private const float DefaultTelegraph = 0.2f;

        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private LayerMask enemyHurtboxMask;
        [SerializeField] private LineRenderer[] telegraphViews = new LineRenderer[Capacity];

        private readonly Collider2D[] _beamColliderHits = new Collider2D[BeamOverlapCapacity];
        private readonly OSEnemyController[] _beamEnemyHits = new OSEnemyController[BeamOverlapCapacity];
        private readonly int[] _beamEnemyRuntimeIds = new int[BeamOverlapCapacity];
        private ContactFilter2D _enemyHurtboxFilter;

        private int[] _stableIds = new int[Capacity];
        private float[] _cooldowns = new float[Capacity];
        private float[] _telegraphRemaining = new float[Capacity];
        private bool[] _telegraphActive = new bool[Capacity];
        private Vector2[] _origins = new Vector2[Capacity];
        private Vector2[] _directions = new Vector2[Capacity];
        private int[] _targetRuntimeIds = new int[Capacity];

        private int[] _nextStableIds = new int[Capacity];
        private float[] _nextCooldowns = new float[Capacity];
        private float[] _nextTelegraphRemaining = new float[Capacity];
        private bool[] _nextTelegraphActive = new bool[Capacity];
        private Vector2[] _nextOrigins = new Vector2[Capacity];
        private Vector2[] _nextDirections = new Vector2[Capacity];
        private int[] _nextTargetRuntimeIds = new int[Capacity];

        private int _stateCount;
        private bool _subscribed;
        private float _testLength = -1f;
        private float _testDamage = -1f;
        private float _testInterval = -1f;
        private float _testWidth = -1f;
        private float _testTelegraph = -1f;
        private float _cooldownMultiplier = 1f;

        public event Action<OSLaserSnapshot> TelegraphStarted;
        public event Action<OSLaserResolution> LaserResolved;

        public int ActiveSegmentCount => _stateCount;
        public int ActiveTelegraphCount { get; private set; }
        public int BeamsFired { get; private set; }
        public int HitsConfirmed { get; private set; }
        public float Length => EffectiveLength;
        public float Damage => EffectiveDamage;
        public float Interval => EffectiveInterval;
        public float Width => EffectiveWidth;
        public float TelegraphDuration => EffectiveTelegraph;

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
            HideAllViews();
        }

        internal void ConfigureForTesting(
            OSBodyRoleRegistry roles,
            OSEnemyRegistry enemies,
            OSGameSessionController session,
            LineRenderer[] views = null,
            float length = DefaultLength,
            float damage = DefaultDamage,
            float interval = DefaultInterval,
            float width = DefaultWidth,
            float telegraph = DefaultTelegraph,
            int hurtboxMask = Physics2D.AllLayers)
        {
            Unsubscribe();
            roleRegistry = roles;
            enemyRegistry = enemies;
            sessionController = session;
            bodyBalance = null;
            telegraphViews = views ?? Array.Empty<LineRenderer>();
            _testLength = Mathf.Max(0.01f, length);
            _testDamage = Mathf.Max(0.01f, damage);
            _testInterval = Mathf.Max(0.01f, interval);
            _testWidth = Mathf.Max(0.01f, width);
            _testTelegraph = Mathf.Max(0f, telegraph);
            enemyHurtboxMask = hurtboxMask;
            RefreshEnemyHurtboxFilter();
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
                if (_telegraphActive[index])
                {
                    _telegraphRemaining[index] = Mathf.Max(0f, _telegraphRemaining[index] - deltaTime);
                    RefreshView(index);
                    if (_telegraphRemaining[index] <= 0f)
                    {
                        ResolveLaser(index);
                    }

                    continue;
                }

                _cooldowns[index] = Mathf.Max(0f, _cooldowns[index] - deltaTime);
                if (_cooldowns[index] <= 0f)
                {
                    TryBeginTelegraph(index);
                }
            }
        }

        internal bool TryGetSnapshotForTesting(int stableId, out OSLaserSnapshot snapshot)
        {
            var index = FindStateIndex(stableId);
            if (index < 0 || !_telegraphActive[index])
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateSnapshot(index);
            return true;
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _cooldownMultiplier = Mathf.Clamp(modifiers.RoleCooldownMultiplier, 0.5f, 1f);
            for (var index = 0; index < _stateCount; index++)
            {
                _cooldowns[index] = Mathf.Min(_cooldowns[index], EffectiveInterval);
            }
        }

        private float EffectiveLength => _testLength >= 0f
            ? _testLength
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Laser)?.Range ?? DefaultLength;
        private float EffectiveDamage => _testDamage >= 0f
            ? _testDamage
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Laser)?.Damage ?? DefaultDamage;
        private float EffectiveInterval => _testInterval >= 0f
            ? _testInterval
            : Mathf.Max(
                0.15f,
                (bodyBalance?.GetRoleDefinition(OSBodyRoleType.Laser)?.Interval ?? DefaultInterval) *
                _cooldownMultiplier);
        private float EffectiveWidth => _testWidth >= 0f
            ? _testWidth
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Laser)?.BeamWidth ?? DefaultWidth;
        private float EffectiveTelegraph => _testTelegraph >= 0f
            ? _testTelegraph
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Laser)?.TelegraphDuration ?? DefaultTelegraph;

        private bool TryBeginTelegraph(int index)
        {
            var segment = roleRegistry?.FindByStableId(OSBodyRoleType.Laser, _stableIds[index]);
            if (segment?.View == null || enemyRegistry == null)
            {
                return false;
            }

            var origin = (Vector2)segment.View.transform.position;
            var target = enemyRegistry.FindNearestTarget(
                origin,
                EffectiveLength,
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

            _targetRuntimeIds[index] = target.RuntimeId;
            _origins[index] = origin;
            _directions[index] = direction.normalized;
            _telegraphRemaining[index] = EffectiveTelegraph;
            _telegraphActive[index] = true;
            ActiveTelegraphCount++;
            RefreshView(index);
            TelegraphStarted?.Invoke(CreateSnapshot(index));

            if (_telegraphRemaining[index] <= 0f)
            {
                ResolveLaser(index);
            }

            return true;
        }

        private void ResolveLaser(int index)
        {
            var stableId = _stableIds[index];
            if (roleRegistry?.FindByStableId(OSBodyRoleType.Laser, stableId) == null)
            {
                CancelTelegraph(index);
                return;
            }

            var hitCount = 0;
            var killCount = 0;
            var uniqueEnemyCount = CollectBeamTargets(index);
            for (var enemyIndex = 0; enemyIndex < uniqueEnemyCount; enemyIndex++)
            {
                var enemy = _beamEnemyHits[enemyIndex];
                if (enemy == null || !enemy.IsRented || enemy.CurrentHealth <= 0f)
                {
                    continue;
                }

                var result = enemy.TryApplyDamage(EffectiveDamage);
                if (result.IsAccepted)
                {
                    hitCount++;
                    HitsConfirmed++;
                    if (!enemy.IsRented || result.Payload <= 0f)
                    {
                        killCount++;
                    }
                }
            }

            Array.Clear(_beamEnemyHits, 0, uniqueEnemyCount);
            Array.Clear(_beamEnemyRuntimeIds, 0, uniqueEnemyCount);

            _telegraphActive[index] = false;
            _telegraphRemaining[index] = 0f;
            _cooldowns[index] = EffectiveInterval;
            ActiveTelegraphCount = Mathf.Max(0, ActiveTelegraphCount - 1);
            SetViewVisible(index, false);
            BeamsFired++;
            LaserResolved?.Invoke(new OSLaserResolution(stableId, hitCount, killCount, false));
        }

        private int CollectBeamTargets(int index)
        {
            if (enemyRegistry == null || enemyHurtboxMask.value == 0)
            {
                return 0;
            }

            var length = EffectiveLength;
            var direction = _directions[index];
            var center = _origins[index] + (direction * (length * 0.5f));
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var colliderCount = Physics2D.OverlapBox(
                center,
                new Vector2(length, EffectiveWidth),
                angle,
                _enemyHurtboxFilter,
                _beamColliderHits);
            var uniqueEnemyCount = 0;
            for (var colliderIndex = 0; colliderIndex < colliderCount; colliderIndex++)
            {
                var collider = _beamColliderHits[colliderIndex];
                var enemy = collider?.attachedRigidbody != null
                    ? collider.attachedRigidbody.GetComponent<OSEnemyController>()
                    : collider?.GetComponentInParent<OSEnemyController>();
                if (enemy == null || !enemy.IsRented || enemy.CurrentHealth <= 0f ||
                    enemy.RegistryIndex < 0 || enemy.RegistryIndex >= enemyRegistry.Count ||
                    enemyRegistry.GetAt(enemy.RegistryIndex) != enemy ||
                    ContainsRuntimeId(enemy.RuntimeId, uniqueEnemyCount))
                {
                    continue;
                }

                _beamEnemyRuntimeIds[uniqueEnemyCount] = enemy.RuntimeId;
                _beamEnemyHits[uniqueEnemyCount] = enemy;
                uniqueEnemyCount++;
            }

            Array.Clear(_beamColliderHits, 0, colliderCount);
            return uniqueEnemyCount;
        }

        private bool ContainsRuntimeId(int runtimeId, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (_beamEnemyRuntimeIds[index] == runtimeId)
                {
                    return true;
                }
            }

            return false;
        }

        private void CancelTelegraph(int index)
        {
            if (!_telegraphActive[index])
            {
                return;
            }

            var stableId = _stableIds[index];
            _telegraphActive[index] = false;
            _telegraphRemaining[index] = 0f;
            ActiveTelegraphCount = Mathf.Max(0, ActiveTelegraphCount - 1);
            SetViewVisible(index, false);
            LaserResolved?.Invoke(new OSLaserResolution(stableId, 0, 0, true));
        }

        private OSLaserSnapshot CreateSnapshot(int index)
        {
            return new OSLaserSnapshot(
                _stableIds[index],
                _origins[index],
                _directions[index],
                EffectiveLength,
                EffectiveWidth,
                _telegraphRemaining[index]);
        }

        private void SynchronizeStates()
        {
            for (var oldIndex = 0; oldIndex < _stateCount; oldIndex++)
            {
                if (_telegraphActive[oldIndex] &&
                    roleRegistry?.FindByStableId(OSBodyRoleType.Laser, _stableIds[oldIndex]) == null)
                {
                    CancelTelegraph(oldIndex);
                }
            }

            ClearNextBuffers();
            var nextCount = Mathf.Min(roleRegistry?.GetCount(OSBodyRoleType.Laser) ?? 0, Capacity);
            for (var index = 0; index < nextCount; index++)
            {
                var segment = roleRegistry.GetSegment(OSBodyRoleType.Laser, index);
                if (segment == null)
                {
                    continue;
                }

                _nextStableIds[index] = segment.StableId;
                var previousIndex = FindStateIndex(segment.StableId);
                if (previousIndex < 0)
                {
                    continue;
                }

                _nextCooldowns[index] = _cooldowns[previousIndex];
                _nextTelegraphRemaining[index] = _telegraphRemaining[previousIndex];
                _nextTelegraphActive[index] = _telegraphActive[previousIndex];
                _nextOrigins[index] = _origins[previousIndex];
                _nextDirections[index] = _directions[previousIndex];
                _nextTargetRuntimeIds[index] = _targetRuntimeIds[previousIndex];
            }

            Swap(ref _stableIds, ref _nextStableIds);
            Swap(ref _cooldowns, ref _nextCooldowns);
            Swap(ref _telegraphRemaining, ref _nextTelegraphRemaining);
            Swap(ref _telegraphActive, ref _nextTelegraphActive);
            Swap(ref _origins, ref _nextOrigins);
            Swap(ref _directions, ref _nextDirections);
            Swap(ref _targetRuntimeIds, ref _nextTargetRuntimeIds);
            _stateCount = nextCount;
            RefreshAllViews();
        }

        private void RefreshView(int index)
        {
            if (telegraphViews == null || index < 0 || index >= telegraphViews.Length ||
                telegraphViews[index] == null)
            {
                return;
            }

            var view = telegraphViews[index];
            view.enabled = _telegraphActive[index];
            if (!view.enabled)
            {
                return;
            }

            view.useWorldSpace = true;
            view.positionCount = 2;
            view.startWidth = EffectiveWidth;
            view.endWidth = EffectiveWidth;
            view.SetPosition(0, _origins[index]);
            view.SetPosition(1, _origins[index] + (_directions[index] * EffectiveLength));
        }

        private void RefreshAllViews()
        {
            HideAllViews();
            for (var index = 0; index < _stateCount; index++)
            {
                RefreshView(index);
            }
        }

        private void SetViewVisible(int index, bool visible)
        {
            if (telegraphViews != null && index >= 0 && index < telegraphViews.Length &&
                telegraphViews[index] != null)
            {
                telegraphViews[index].enabled = visible;
            }
        }

        private void HideAllViews()
        {
            if (telegraphViews == null)
            {
                return;
            }

            for (var index = 0; index < telegraphViews.Length; index++)
            {
                if (telegraphViews[index] != null)
                {
                    telegraphViews[index].enabled = false;
                }
            }
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

        private void ClearNextBuffers()
        {
            Array.Clear(_nextStableIds, 0, Capacity);
            Array.Clear(_nextCooldowns, 0, Capacity);
            Array.Clear(_nextTelegraphRemaining, 0, Capacity);
            Array.Clear(_nextTelegraphActive, 0, Capacity);
            Array.Clear(_nextOrigins, 0, Capacity);
            Array.Clear(_nextDirections, 0, Capacity);
            Array.Clear(_nextTargetRuntimeIds, 0, Capacity);
        }

        private void ResetState()
        {
            Array.Clear(_stableIds, 0, Capacity);
            Array.Clear(_cooldowns, 0, Capacity);
            Array.Clear(_telegraphRemaining, 0, Capacity);
            Array.Clear(_telegraphActive, 0, Capacity);
            Array.Clear(_origins, 0, Capacity);
            Array.Clear(_directions, 0, Capacity);
            Array.Clear(_targetRuntimeIds, 0, Capacity);
            _stateCount = 0;
            ActiveTelegraphCount = 0;
            BeamsFired = 0;
            HitsConfirmed = 0;
            Array.Clear(_beamColliderHits, 0, _beamColliderHits.Length);
            Array.Clear(_beamEnemyHits, 0, _beamEnemyHits.Length);
            Array.Clear(_beamEnemyRuntimeIds, 0, _beamEnemyRuntimeIds.Length);
            HideAllViews();
        }

        private void Awake()
        {
            RefreshEnemyHurtboxFilter();
        }

        private void OnValidate()
        {
            RefreshEnemyHurtboxFilter();
        }

        private void RefreshEnemyHurtboxFilter()
        {
            _enemyHurtboxFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = enemyHurtboxMask,
                useTriggers = true,
                useDepth = false,
                useNormalAngle = false
            };
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
            if (current is OSSessionState.Boot or OSSessionState.Dead or OSSessionState.Cleared)
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
