using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(9500)]
    [DisallowMultipleComponent]
    public sealed class OSBossEncounterController : MonoBehaviour
    {
        private const string BossPoolKey = "boss_swarm_core";
        private const float DefaultTimeLimit = 90f;

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField, Min(1f)] private float timeLimitSeconds = DefaultTimeLimit;
        [SerializeField] private Vector2 spawnPosition = new(0f, 6f);

        private OSEnemyController _bossEnemy;
        private OSBossController _bossController;
        private bool _pendingBossDefeat;
        private bool _subscribed;

        public event Action EncounterStateChanged;
        public event Action BossSpawned;
        public event Action BossDefeated;
        public event Action BossTimedOut;

        public bool IsBossActive => _bossEnemy != null && _bossEnemy.IsRented &&
                                    !_bossEnemy.IsDeathConfirmed;
        public OSEnemyController BossEnemy => IsBossActive ? _bossEnemy : null;
        public OSBossController BossController => IsBossActive ? _bossController : null;
        public float BossHealth => IsBossActive ? _bossEnemy.CurrentHealth : 0f;
        public float BossMaxHealth => _bossEnemy != null ? _bossEnemy.MaxHealth : 6000f;
        public float ShieldHealth => IsBossActive && _bossController != null
            ? _bossController.ShieldHealth
            : 0f;
        public float ShieldMaxHealth => _bossController != null
            ? _bossController.ShieldMaxHealth
            : 600f;
        public OSBossPhase Phase => _bossController != null
            ? _bossController.Phase
            : OSBossPhase.PhaseOne;
        public OSBossPattern ActivePattern => _bossController != null
            ? _bossController.ActivePattern
            : OSBossPattern.None;
        public float TelegraphRemaining => _bossController != null
            ? _bossController.TelegraphRemaining
            : 0f;
        public float EncounterElapsedSeconds { get; private set; }
        public float TimeRemaining => OSBossMath.GetRemainingTime(
            timeLimitSeconds,
            EncounterElapsedSeconds);
        public int BossSpawnCount { get; private set; }
        public int BossDefeatCount { get; private set; }
        public int BossTimeoutCount { get; private set; }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            ResolvePendingBossDefeat();
            if (!IsBossActive || sessionController == null ||
                !sessionController.IsSimulationRunning)
            {
                return;
            }

            EncounterElapsedSeconds += Mathf.Max(0f, Time.fixedDeltaTime);
            if (EncounterElapsedSeconds >= timeLimitSeconds)
            {
                ResolveTimeout();
            }

            EncounterStateChanged?.Invoke();
        }

        private void LateUpdate()
        {
            ResolvePendingBossDefeat();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSGameSessionController session,
            OSPoolRegistry pools,
            OSPlayerController player,
            float limitSeconds = DefaultTimeLimit)
        {
            Unsubscribe();
            sessionController = session;
            poolRegistry = pools;
            playerController = player;
            timeLimitSeconds = Mathf.Max(1f, limitSeconds);
            Subscribe();
        }

        public OSRuleResult<OSEnemyController> RequestBossSpawn()
        {
            if (sessionController == null || !sessionController.IsSimulationRunning ||
                poolRegistry == null || IsBossActive || _pendingBossDefeat)
            {
                return OSRuleResult<OSEnemyController>.Rejected(
                    OSResultCode.RejectedState,
                    "boss.spawn.invalid_state",
                    _bossEnemy);
            }

            var position = ClampSpawnPosition(spawnPosition);
            var rent = poolRegistry.Rent(BossPoolKey, position, Quaternion.identity);
            if (!rent.IsAccepted || rent.Payload is not OSEnemyController enemy)
            {
                return OSRuleResult<OSEnemyController>.Rejected(
                    rent.Code,
                    rent.ReasonKey,
                    _bossEnemy);
            }

            var boss = enemy.GetComponent<OSBossController>();
            if (boss == null)
            {
                enemy.ReturnToPool();
                return OSRuleResult<OSEnemyController>.Rejected(
                    OSResultCode.ConfigurationError,
                    "boss.spawn.controller_missing");
            }

            _bossEnemy = enemy;
            _bossController = boss;
            _bossController.StateChanged -= HandleBossStateChanged;
            _bossController.StateChanged += HandleBossStateChanged;
            EncounterElapsedSeconds = 0f;
            BossSpawnCount++;
            BossSpawned?.Invoke();
            EncounterStateChanged?.Invoke();
            return OSRuleResult<OSEnemyController>.Accepted(enemy, "boss.spawn.accepted");
        }

        public void HandleBossDied(OSEnemyController enemy)
        {
            if (!_pendingBossDefeat && enemy != null && enemy == _bossEnemy &&
                enemy.Archetype == OSEnemyArchetype.BossSwarmCore)
            {
                _pendingBossDefeat = true;
                EncounterStateChanged?.Invoke();
            }
        }

        internal void AdvanceForTesting(float deltaTime)
        {
            ResolvePendingBossDefeat();
            if (!IsBossActive || sessionController == null ||
                !sessionController.IsSimulationRunning || !float.IsFinite(deltaTime) ||
                deltaTime <= 0f)
            {
                return;
            }

            EncounterElapsedSeconds += deltaTime;
            _bossController?.SimulateStep(deltaTime);
            if (EncounterElapsedSeconds >= timeLimitSeconds)
            {
                ResolveTimeout();
            }

            EncounterStateChanged?.Invoke();
        }

        private void ResolvePendingBossDefeat()
        {
            if (!_pendingBossDefeat)
            {
                return;
            }

            if (sessionController == null ||
                sessionController.State is OSSessionState.Dead or OSSessionState.Result)
            {
                _pendingBossDefeat = false;
                DetachBoss();
                return;
            }

            if (!sessionController.IsSimulationRunning)
            {
                return;
            }

            _pendingBossDefeat = false;
            BossDefeatCount++;
            BossDefeated?.Invoke();
            DetachBoss();
            sessionController.RequestClear();
            EncounterStateChanged?.Invoke();
        }

        private void ResolveTimeout()
        {
            if (!IsBossActive || sessionController == null ||
                !sessionController.IsSimulationRunning)
            {
                return;
            }

            BossTimeoutCount++;
            BossTimedOut?.Invoke();
            sessionController.RequestBossTimeout();
            ReturnBossWithoutDefeat();
            EncounterStateChanged?.Invoke();
        }

        private void ReturnBossWithoutDefeat()
        {
            var enemy = _bossEnemy;
            DetachBoss();
            if (enemy != null && enemy.IsRented)
            {
                enemy.ReturnToPool();
            }
        }

        private void DetachBoss()
        {
            if (_bossController != null)
            {
                _bossController.StateChanged -= HandleBossStateChanged;
            }

            _bossEnemy = null;
            _bossController = null;
        }

        private Vector2 ClampSpawnPosition(Vector2 requested)
        {
            if (playerController == null)
            {
                return requested;
            }

            const float margin = 1.5f;
            return new Vector2(
                Mathf.Clamp(requested.x, playerController.WorldMin.x + margin,
                    playerController.WorldMax.x - margin),
                Mathf.Clamp(requested.y, playerController.WorldMin.y + margin,
                    playerController.WorldMax.y - margin));
        }

        private void HandleBossStateChanged()
        {
            EncounterStateChanged?.Invoke();
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                ReturnBossWithoutDefeat();
                _pendingBossDefeat = false;
                EncounterElapsedSeconds = 0f;
                BossSpawnCount = 0;
                BossDefeatCount = 0;
                BossTimeoutCount = 0;
                EncounterStateChanged?.Invoke();
                return;
            }

            if (current == OSSessionState.Dead && !_pendingBossDefeat)
            {
                ReturnBossWithoutDefeat();
                EncounterStateChanged?.Invoke();
            }
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
            if (_subscribed && sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void OnValidate()
        {
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
        }
    }
}
