using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class OSRunSummaryController : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSBodyDashController bodyDashController;
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private OSEncounterBalanceData encounterBalance;
        [SerializeField] private OSWaveScheduleData waveSchedule;

        private bool _subscribed;
        private int _totalKills;
        private int _eliteKills;
        private int _dashUseCount;
        private int _maxBodyCount;
        private int _acquiredBodyCount;
        private int _cutBodyCount;
        private int _dashConvergedBodyCount;
        private float _receivedHeadDamage;
        private OSRoleCountSnapshot _maxRoleCounts;

        public event Action<OSSessionSummary> SummaryBuilt;
        public event Action<int> EnemyDefeated;

        public bool HasSummary { get; private set; }
        public OSSessionSummary Summary { get; private set; }
        public int TotalKills => _totalKills;
        public int EliteKills => _eliteKills;
        public int DashUseCount => _dashUseCount;

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            ResetRun();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSGameSessionController session,
            OSPlayerHealth health,
            OSBodyChain chain,
            OSBodyRoleRegistry roles,
            OSBodyDashController bodyDash,
            OSLevelUpController level,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves)
        {
            Unsubscribe();
            sessionController = session;
            playerHealth = health;
            bodyChain = chain;
            roleRegistry = roles;
            bodyDashController = bodyDash;
            levelUpController = level;
            encounterBalance = encounter;
            waveSchedule = waves;
            ResetRun();
            Subscribe();
        }

        public void RecordEnemyDefeated(OSEnemyController enemy)
        {
            if (enemy == null || HasSummary)
            {
                return;
            }

            _totalKills++;
            if (enemy.Archetype == OSEnemyArchetype.EliteAccelerator)
            {
                _eliteKills++;
            }

            EnemyDefeated?.Invoke(_totalKills);
        }

        internal void BuildSummaryForTesting()
        {
            BuildSummary();
        }

        private void ResetRun()
        {
            HasSummary = false;
            Summary = default;
            _totalKills = 0;
            _eliteKills = 0;
            _dashUseCount = 0;
            _maxBodyCount = bodyChain != null ? bodyChain.ActiveCount : 0;
            _acquiredBodyCount = 0;
            _cutBodyCount = 0;
            _dashConvergedBodyCount = 0;
            _receivedHeadDamage = 0f;
            _maxRoleCounts = CaptureRoleCounts();
        }

        private void BuildSummary()
        {
            if (HasSummary || sessionController == null)
            {
                return;
            }

            var finalRoles = CaptureRoleCounts();
            Summary = new OSSessionSummary(
                sessionController.State,
                sessionController.ResultKind,
                sessionController.SessionElapsedTime,
                _totalKills,
                _eliteKills,
                _dashUseCount,
                _maxBodyCount,
                bodyChain != null ? bodyChain.ActiveCount : 0,
                _acquiredBodyCount,
                _cutBodyCount,
                _dashConvergedBodyCount,
                _receivedHeadDamage,
                _maxRoleCounts,
                finalRoles,
                levelUpController != null ? levelUpController.Level : 1,
                levelUpController != null ? levelUpController.AppliedUpgradeCount : 0,
                levelUpController != null ? levelUpController.RunSeed : 0,
                BuildDataVersion(),
                levelUpController != null ? levelUpController.GetAppliedUpgradeSummary() : "NONE");
            HasSummary = true;
            SummaryBuilt?.Invoke(Summary);
        }

        private OSRoleCountSnapshot CaptureRoleCounts()
        {
            return roleRegistry == null || !Application.isPlaying
                ? default
                : new OSRoleCountSnapshot(
                    roleRegistry.GetCount(OSBodyRoleType.Shield),
                    roleRegistry.GetCount(OSBodyRoleType.Attack),
                    roleRegistry.GetCount(OSBodyRoleType.Laser),
                    roleRegistry.GetCount(OSBodyRoleType.Control));
        }

        private void UpdateRoleMaximums()
        {
            var current = CaptureRoleCounts();
            _maxRoleCounts = new OSRoleCountSnapshot(
                Mathf.Max(_maxRoleCounts.Shield, current.Shield),
                Mathf.Max(_maxRoleCounts.Attack, current.Attack),
                Mathf.Max(_maxRoleCounts.Laser, current.Laser),
                Mathf.Max(_maxRoleCounts.Control, current.Control));
        }

        private string BuildDataVersion()
        {
            var encounter = encounterBalance != null ? encounterBalance.DataVersion : "missing";
            var waves = waveSchedule != null ? waveSchedule.DataVersion : "missing";
            return $"ENCOUNTER {encounter} | WAVE {waves}";
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot ||
                previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                ResetRun();
                return;
            }

            if (current is OSSessionState.Dead or OSSessionState.Cleared)
            {
                BuildSummary();
            }
        }

        private void HandleSegmentAppended(int stableId, int chainIndex, OSBodyRoleType role)
        {
            if (HasSummary)
            {
                return;
            }

            _acquiredBodyCount++;
            _maxBodyCount = Mathf.Max(_maxBodyCount, bodyChain != null ? bodyChain.ActiveCount : 0);
            UpdateRoleMaximums();
        }

        private void HandleSegmentsRemoved(OSBodyRemovalEvent removal)
        {
            if (HasSummary)
            {
                return;
            }

            if (removal.Cause == OSBodyRemovalCause.Cut)
            {
                _cutBodyCount += removal.RemovedCount;
            }
            UpdateRoleMaximums();
        }

        private void HandleRoleListsChanged()
        {
            if (!HasSummary)
            {
                UpdateRoleMaximums();
            }
        }

        private void HandleHeadDamaged(OSDamageEvent damageEvent, float remainingHealth)
        {
            if (!HasSummary && playerHealth != null)
            {
                _receivedHeadDamage += playerHealth.LastAppliedDamage;
            }
        }

        private void HandleBodyDashCompleted(OSBodyDashResolution resolution)
        {
            if (!HasSummary && !resolution.WasCancelled)
            {
                _dashUseCount++;
                _dashConvergedBodyCount += Mathf.Max(0, resolution.ConvergedBodyCount);
            }
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleSessionStateChanged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentAppended += HandleSegmentAppended;
                bodyChain.SegmentsRemoved += HandleSegmentsRemoved;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged += HandleRoleListsChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HeadDamaged += HandleHeadDamaged;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashCompleted += HandleBodyDashCompleted;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentAppended -= HandleSegmentAppended;
                bodyChain.SegmentsRemoved -= HandleSegmentsRemoved;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged -= HandleRoleListsChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HeadDamaged -= HandleHeadDamaged;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashCompleted -= HandleBodyDashCompleted;
            }

            _subscribed = false;
        }
    }
}
