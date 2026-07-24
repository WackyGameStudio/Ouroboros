using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-3000)]
    [DisallowMultipleComponent]
    public sealed class OSWaveDirector : MonoBehaviour
    {
        private const string SplitterSpawnId = "enemy_splitter_spawn";
        private const string EliteId = "enemy_elite_accelerator";
        private const string EnemyProjectileKey = "enemy_projectile";
        private const int SpawnAttempts = 8;
        private const float MinimumSpawnDistance = 7f;
        private const float MinimumSpawnRadius = 7.5f;
        private const float MaximumSpawnRadius = 12.5f;

        [Header("Data")]
        [SerializeField] private OSWaveScheduleData waveSchedule;
        [SerializeField] private OSEncounterBalanceData encounterBalance;
        [SerializeField] private int runSeed = 13013;

        [Header("Runtime references")]
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private LayerMask worldBlockerMask;
        [SerializeField] private OSBossEncounterController bossEncounter;

        private OSWaveScheduleRuntime _runtimeSchedule;
        private OSRunRandom _random;
        private float _elapsedSeconds;
        private float _spawnBudget;
        private int _pendingEliteSpawns;
        private bool _subscribed;

        public event Action<OSWaveSpecialEvent> SpecialEventTriggered;
        public event Action<OSEnemyController> EnemySpawned;

        public float ElapsedSeconds => _elapsedSeconds;
        public int ActiveEnemyCount => enemyRegistry != null ? enemyRegistry.NormalEnemyCount : 0;
        public int ActiveEnemyLimit => encounterBalance != null ? encounterBalance.ActiveEnemyLimit : 180;
        public int CurrentTargetActiveEnemies { get; private set; }
        public string CurrentWaveEnemyId { get; private set; } = string.Empty;
        public int EliteSpawnCount { get; private set; }
        public int BossWarningCount { get; private set; }
        public int DeferredBossEventCount { get; private set; }
        public int BossSpawnCount => bossEncounter != null ? bossEncounter.BossSpawnCount : 0;
        public int RejectedSpawnCandidateCount { get; private set; }
        public int DeferredSpawnTicketCount => Mathf.Max(0, Mathf.FloorToInt(_spawnBudget));
        public Vector2 LastSpawnPosition { get; private set; }

        private void Awake()
        {
            BuildRuntimeCopy();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            Advance(Time.deltaTime, true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void HandleEnemyDied(OSEnemyController enemy)
        {
            if (enemy == null || enemy.Archetype != OSEnemyArchetype.Splitter ||
                poolRegistry == null || enemyRegistry == null)
            {
                return;
            }

            var projectedCount = Mathf.Max(0, enemyRegistry.NormalEnemyCount - 1);
            for (var childIndex = 0; childIndex < 2 && projectedCount < ActiveEnemyLimit; childIndex++)
            {
                var offset = childIndex == 0 ? Vector2.left * 0.32f : Vector2.right * 0.32f;
                if (!TrySpawnAt(SplitterSpawnId, enemy.Position + offset, out _))
                {
                    break;
                }

                projectedCount++;
            }
        }

        public bool IsValidSpawnPosition(Vector2 candidate)
        {
            if (playerTarget == null || !float.IsFinite(candidate.x) || !float.IsFinite(candidate.y))
            {
                return false;
            }

            var playerPosition = (Vector2)playerTarget.position;
            if ((candidate - playerPosition).sqrMagnitude < MinimumSpawnDistance * MinimumSpawnDistance)
            {
                return false;
            }

            if (playerController != null)
            {
                const float margin = 0.65f;
                var minimum = playerController.WorldMin + Vector2.one * margin;
                var maximum = playerController.WorldMax - Vector2.one * margin;
                if (candidate.x < minimum.x || candidate.y < minimum.y ||
                    candidate.x > maximum.x || candidate.y > maximum.y)
                {
                    return false;
                }
            }

            if (gameplayCamera != null)
            {
                var viewport = gameplayCamera.WorldToViewportPoint(candidate);
                if (viewport.z > 0f && viewport.x >= -0.02f && viewport.x <= 1.02f &&
                    viewport.y >= -0.02f && viewport.y <= 1.02f)
                {
                    return false;
                }
            }

            return worldBlockerMask.value == 0 ||
                   Physics2D.OverlapCircle(candidate, 0.48f, worldBlockerMask) == null;
        }

        internal void ConfigureForTesting(
            OSWaveScheduleRuntime runtimeSchedule,
            OSEncounterBalanceData balance,
            OSGameSessionController session,
            OSPoolRegistry pools,
            OSEnemyRegistry registry,
            Transform target,
            OSPlayerController player = null,
            Camera camera = null,
            int seed = 13013,
            OSBossEncounterController boss = null)
        {
            Unsubscribe();
            _runtimeSchedule = runtimeSchedule;
            encounterBalance = balance;
            sessionController = session;
            poolRegistry = pools;
            enemyRegistry = registry;
            playerTarget = target;
            playerController = player;
            gameplayCamera = camera;
            bossEncounter = boss;
            runSeed = seed;
            ResetRun();
            Subscribe();
        }

        internal void AdvanceForTesting(float deltaTime, bool spawnNormalEnemies = false)
        {
            Advance(deltaTime, spawnNormalEnemies);
        }

        internal bool TrySpawnForTesting(string enemyId, Vector2 position, out OSEnemyController enemy)
        {
            return TrySpawnAt(enemyId, position, out enemy);
        }

        public bool TrySpawnBossSummon(string enemyId, out OSEnemyController enemy)
        {
            enemy = null;
            return ActiveEnemyCount < ActiveEnemyLimit &&
                   TryFindSpawnPosition(out var position) &&
                   TrySpawnAt(enemyId, position, out enemy);
        }

        private void Advance(float deltaTime, bool spawnNormalEnemies)
        {
            if (_runtimeSchedule == null || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            var previous = _elapsedSeconds;
            _elapsedSeconds += deltaTime;
            DispatchCrossedSpecialEvents(previous, _elapsedSeconds);

            var entryIndex = _runtimeSchedule.FindEntryIndex(_elapsedSeconds);
            if (entryIndex < 0)
            {
                CurrentTargetActiveEnemies = 0;
                return;
            }

            var entry = _runtimeSchedule.GetEntry(entryIndex);
            CurrentTargetActiveEnemies = entry.TargetActiveEnemies;

            TrySpawnPendingElite();
            if (!spawnNormalEnemies || !entry.HasNormalSpawns)
            {
                return;
            }

            _spawnBudget += entry.SpawnRate *
                            _runtimeSchedule.SpawnDensityMultiplier *
                            OSWaveScheduleRuntime.CalculateSpawnRateMultiplier(_elapsedSeconds) *
                            deltaTime;
            SpawnAvailableTickets(entryIndex, entry.TargetActiveEnemies);
        }

        private void SpawnAvailableTickets(int entryIndex, int targetActiveEnemies)
        {
            var safety = ActiveEnemyLimit;
            while (_spawnBudget >= 1f && safety-- > 0)
            {
                if (!OSWaveScheduleRuntime.CanSpawn(
                        ActiveEnemyCount,
                        ActiveEnemyLimit,
                        targetActiveEnemies))
                {
                    return;
                }

                var enemyId = _runtimeSchedule.SelectEnemyId(entryIndex, _random);
                if (string.IsNullOrWhiteSpace(enemyId) || !TryFindSpawnPosition(out var position) ||
                    !TrySpawnAt(enemyId, position, out _))
                {
                    return;
                }

                CurrentWaveEnemyId = enemyId;
                _spawnBudget -= 1f;
            }
        }

        private bool TryFindSpawnPosition(out Vector2 position)
        {
            position = default;
            if (playerTarget == null || _random == null)
            {
                return false;
            }

            var center = (Vector2)playerTarget.position;
            for (var attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                var angle = (float)(_random.NextDouble() * Math.PI * 2d);
                var radius = Mathf.Lerp(
                    MinimumSpawnRadius,
                    MaximumSpawnRadius,
                    (float)_random.NextDouble());
                var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (IsValidSpawnPosition(candidate))
                {
                    position = candidate;
                    return true;
                }

                RejectedSpawnCandidateCount++;
            }

            return false;
        }

        private bool TrySpawnAt(string enemyId, Vector2 position, out OSEnemyController enemy)
        {
            enemy = null;
            if (poolRegistry == null || enemyRegistry == null ||
                enemyRegistry.NormalEnemyCount >= ActiveEnemyLimit)
            {
                return false;
            }

            var rent = poolRegistry.Rent(enemyId, position, Quaternion.identity);
            if (!rent.IsAccepted || rent.Payload is not OSEnemyController rentedEnemy)
            {
                return false;
            }

            var multiplier = OSWaveScheduleRuntime.CalculateHealthMultiplier(_elapsedSeconds);
            if (!rentedEnemy.ApplyWaveHealthMultiplier(multiplier).IsAccepted)
            {
                rentedEnemy.ReturnToPool();
                return false;
            }

            enemy = rentedEnemy;
            LastSpawnPosition = position;
            EnemySpawned?.Invoke(enemy);
            return true;
        }

        private void DispatchCrossedSpecialEvents(float previous, float current)
        {
            for (var index = 0; index < _runtimeSchedule.Count; index++)
            {
                var entry = _runtimeSchedule.GetEntry(index);
                if (entry.SpecialEvent == OSWaveSpecialEvent.None ||
                    !OSWaveScheduleRuntime.Crossed(previous, current, entry.StartSeconds))
                {
                    continue;
                }

                switch (entry.SpecialEvent)
                {
                    case OSWaveSpecialEvent.EliteAccelerator:
                        _pendingEliteSpawns++;
                        break;
                    case OSWaveSpecialEvent.BossWarning:
                        BossWarningCount++;
                        SpecialEventTriggered?.Invoke(OSWaveSpecialEvent.BossWarning);
                        break;
                    case OSWaveSpecialEvent.BossSwarmCore:
                        var spawn = bossEncounter?.RequestBossSpawn();
                        if (!spawn.HasValue || !spawn.Value.IsAccepted)
                        {
                            DeferredBossEventCount++;
                        }
                        SpecialEventTriggered?.Invoke(OSWaveSpecialEvent.BossSwarmCore);
                        break;
                }
            }
        }

        private void TrySpawnPendingElite()
        {
            while (_pendingEliteSpawns > 0 && ActiveEnemyCount < ActiveEnemyLimit)
            {
                if (!TryFindSpawnPosition(out var position) ||
                    !TrySpawnAt(EliteId, position, out _))
                {
                    return;
                }

                _pendingEliteSpawns--;
                EliteSpawnCount++;
                SpecialEventTriggered?.Invoke(OSWaveSpecialEvent.EliteAccelerator);
            }
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.StartBodySelection &&
                previous is OSSessionState.Boot or OSSessionState.Result)
            {
                ResetRun();
            }
        }

        private void ResetRun()
        {
            enemyRegistry?.ReturnAll();
            poolRegistry?.ReturnAll(EnemyProjectileKey);
            _random = new OSRunRandom(runSeed);
            _elapsedSeconds = 0f;
            _spawnBudget = 0f;
            _pendingEliteSpawns = 0;
            EliteSpawnCount = 0;
            BossWarningCount = 0;
            DeferredBossEventCount = 0;
            RejectedSpawnCandidateCount = 0;
            CurrentTargetActiveEnemies = 0;
            CurrentWaveEnemyId = string.Empty;
            LastSpawnPosition = default;
        }

        private void BuildRuntimeCopy()
        {
            _runtimeSchedule = waveSchedule != null
                ? new OSWaveScheduleRuntime(waveSchedule)
                : null;
            _random = new OSRunRandom(runSeed);
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleSessionStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || sessionController == null)
            {
                _subscribed = false;
                return;
            }

            sessionController.StateChanged -= HandleSessionStateChanged;
            _subscribed = false;
        }

        private void OnValidate()
        {
            runSeed = runSeed == 0 ? 13013 : runSeed;
        }
    }
}
