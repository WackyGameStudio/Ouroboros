using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    public enum OSSessionState
    {
        Boot,
        StartBodySelection,
        Combat,
        ExplosionTelegraph,
        BodyRoleSelection,
        LevelUpSelection,
        Dead,
        Cleared,
        Result
    }

    public enum OSSessionResultKind
    {
        None,
        PlayerDefeated,
        BossDefeated,
        BossTimeout
    }

    public enum OSBodyRoleType
    {
        Shield,
        Attack,
        Laser,
        Control
    }

    public enum OSSelectionKind
    {
        StartBody,
        BodyRole,
        LevelUp
    }

    public enum OSPickupType
    {
        BodyFragment,
        Experience,
        Heal
    }

    public enum OSTargetKind
    {
        PlayerHead,
        PlayerBody,
        Enemy,
        Elite,
        Boss
    }

    public enum OSCombatEventType
    {
        Damage,
        Pickup,
        ExplosionCompleted,
        EnemyDefeated,
        BossDefeated
    }

    public enum OSUpgradeOperation
    {
        AddHeadDamageMultiplier,
        AddHeadRateMultiplier,
        AddHeadPierce,
        AddFragmentRequirementMultiplier,
        AddBodyDamageRate,
        AddRoleCooldownMultiplier,
        AddExplosionRadiusMultiplier,
        AddExplosionDamageMultiplier,
        AddExplosionConsumeRate,
        AddMaxHealth,
        AddMoveSpeedMultiplier,
        AddHealMultiplier,
        AddMagnetMultiplier,
        AddExperienceMultiplier,
        EnableElitePriority
    }

    public enum OSUpgradeCategory
    {
        Firepower,
        Body,
        Explosion,
        Survival,
        Utility
    }

    public enum OSEnemyArchetype
    {
        Chaser,
        Charger,
        Shooter,
        Splitter,
        SplitterSpawn,
        EliteAccelerator,
        BossSwarmCore
    }

    public enum OSWaveSpecialEvent
    {
        None,
        EliteAccelerator,
        BossWarning,
        BossSwarmCore
    }

    public enum OSEnemyBehaviorState
    {
        Pursuit,
        Telegraph,
        Charge,
        Recovery,
        RangedHold
    }

    public enum OSBossPhase
    {
        PhaseOne,
        PhaseTwo,
        PhaseThree
    }

    public enum OSBossPattern
    {
        None,
        FanProjectiles,
        SwarmSummon,
        AttractionPulse,
        Shield
    }

    public readonly struct OSRoleCountSnapshot
    {
        public OSRoleCountSnapshot(int shield, int attack, int laser, int control)
        {
            Shield = Mathf.Max(0, shield);
            Attack = Mathf.Max(0, attack);
            Laser = Mathf.Max(0, laser);
            Control = Mathf.Max(0, control);
        }

        public int Shield { get; }
        public int Attack { get; }
        public int Laser { get; }
        public int Control { get; }

        public int Get(OSBodyRoleType role)
        {
            return role switch
            {
                OSBodyRoleType.Shield => Shield,
                OSBodyRoleType.Attack => Attack,
                OSBodyRoleType.Laser => Laser,
                OSBodyRoleType.Control => Control,
                _ => 0
            };
        }
    }

    public readonly struct OSDamageEvent
    {
        public OSDamageEvent(
            int attackEventId,
            int combatTick,
            int sourceRuntimeId,
            int targetRuntimeId,
            OSTargetKind targetKind,
            float damage,
            Vector2 hitPosition,
            float controlDuration = 0f)
        {
            AttackEventId = attackEventId;
            CombatTick = combatTick;
            SourceRuntimeId = sourceRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            TargetKind = targetKind;
            Damage = damage;
            HitPosition = hitPosition;
            ControlDuration = controlDuration;
        }

        public int AttackEventId { get; }
        public int CombatTick { get; }
        public int SourceRuntimeId { get; }
        public int TargetRuntimeId { get; }
        public OSTargetKind TargetKind { get; }
        public float Damage { get; }
        public Vector2 HitPosition { get; }
        public float ControlDuration { get; }
    }

    public readonly struct OSPickupEvent
    {
        public OSPickupEvent(int pickupRuntimeId, int combatTick, OSPickupType pickupType, int amount)
        {
            PickupRuntimeId = pickupRuntimeId;
            CombatTick = combatTick;
            PickupType = pickupType;
            Amount = amount;
        }

        public int PickupRuntimeId { get; }
        public int CombatTick { get; }
        public OSPickupType PickupType { get; }
        public int Amount { get; }
    }

    public readonly struct OSSelectionRequest
    {
        public OSSelectionRequest(int requestId, OSSelectionKind kind, int createdTick)
        {
            RequestId = requestId;
            Kind = kind;
            CreatedTick = createdTick;
        }

        public int RequestId { get; }
        public OSSelectionKind Kind { get; }
        public int CreatedTick { get; }
    }

    public readonly struct OSExplosionSnapshot
    {
        public OSExplosionSnapshot(
            int requestId,
            int consumeCount,
            IReadOnlyList<int> reservedSegmentIds,
            IReadOnlyList<Vector2> centers)
        {
            RequestId = requestId;
            ConsumeCount = consumeCount;
            ReservedSegmentIds = reservedSegmentIds;
            Centers = centers;
        }

        public int RequestId { get; }
        public int ConsumeCount { get; }
        public IReadOnlyList<int> ReservedSegmentIds { get; }
        public IReadOnlyList<Vector2> Centers { get; }
    }

    public readonly struct OSSessionSummary
    {
        public OSSessionSummary(
            OSSessionState resultState,
            OSSessionResultKind resultKind,
            float durationSeconds,
            int totalKills,
            int eliteKills,
            int explosionKills,
            int maxBodyCount,
            int finalBodyCount,
            int acquiredBodyCount,
            int cutBodyCount,
            int explosionConsumedBodyCount,
            float receivedHeadDamage,
            OSRoleCountSnapshot maxRoleCounts,
            OSRoleCountSnapshot finalRoleCounts,
            int level,
            int appliedUpgradeCount,
            int runSeed,
            string dataVersion,
            string upgradeSummary)
        {
            ResultState = resultState;
            ResultKind = resultKind;
            DurationSeconds = durationSeconds;
            TotalKills = totalKills;
            EliteKills = eliteKills;
            ExplosionKills = explosionKills;
            MaxBodyCount = maxBodyCount;
            FinalBodyCount = finalBodyCount;
            AcquiredBodyCount = acquiredBodyCount;
            CutBodyCount = cutBodyCount;
            ExplosionConsumedBodyCount = explosionConsumedBodyCount;
            ReceivedHeadDamage = receivedHeadDamage;
            MaxRoleCounts = maxRoleCounts;
            FinalRoleCounts = finalRoleCounts;
            Level = Mathf.Max(1, level);
            AppliedUpgradeCount = Mathf.Max(0, appliedUpgradeCount);
            RunSeed = runSeed;
            DataVersion = dataVersion ?? string.Empty;
            UpgradeSummary = upgradeSummary ?? string.Empty;
        }

        public OSSessionState ResultState { get; }
        public OSSessionResultKind ResultKind { get; }
        public float DurationSeconds { get; }
        public int TotalKills { get; }
        public int EliteKills { get; }
        public int ExplosionKills { get; }
        public int MaxBodyCount { get; }
        public int FinalBodyCount { get; }
        public int AcquiredBodyCount { get; }
        public int CutBodyCount { get; }
        public int ExplosionConsumedBodyCount { get; }
        public float ReceivedHeadDamage { get; }
        public OSRoleCountSnapshot MaxRoleCounts { get; }
        public OSRoleCountSnapshot FinalRoleCounts { get; }
        public int Level { get; }
        public int AppliedUpgradeCount { get; }
        public int RunSeed { get; }
        public string DataVersion { get; }
        public string UpgradeSummary { get; }
    }
}
