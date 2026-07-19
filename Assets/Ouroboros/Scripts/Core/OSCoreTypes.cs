using UnityEngine;

namespace Ouroboros.Core
{
    public enum OSSessionState
    {
        Boot,
        StartBodySelection,
        Combat,
        BodyDash,
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
        Heal,
        SeveredBody
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
        BodyDashCompleted,
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
        AddDashDistanceMultiplier,
        AddDashCooldownMultiplier,
        AddDashCooldownDelta,
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
        Dash,
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

    public readonly struct OSBodyDashSnapshot
    {
        public OSBodyDashSnapshot(
            int requestId,
            float duration,
            float distance,
            Vector2 direction,
            int bodyCount)
        {
            RequestId = requestId;
            Duration = duration;
            Distance = distance;
            Direction = direction;
            BodyCount = bodyCount;
        }

        public int RequestId { get; }
        public float Duration { get; }
        public float Distance { get; }
        public Vector2 Direction { get; }
        public int BodyCount { get; }
    }

    public readonly struct OSSessionSummary
    {
        public OSSessionSummary(
            OSSessionState resultState,
            OSSessionResultKind resultKind,
            float durationSeconds,
            int totalKills,
            int eliteKills,
            int dashUseCount,
            int maxBodyCount,
            int finalBodyCount,
            int acquiredBodyCount,
            int cutBodyCount,
            int dashConvergedBodyCount,
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
            DashUseCount = dashUseCount;
            MaxBodyCount = maxBodyCount;
            FinalBodyCount = finalBodyCount;
            AcquiredBodyCount = acquiredBodyCount;
            CutBodyCount = cutBodyCount;
            DashConvergedBodyCount = dashConvergedBodyCount;
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
        public int DashUseCount { get; }
        public int MaxBodyCount { get; }
        public int FinalBodyCount { get; }
        public int AcquiredBodyCount { get; }
        public int CutBodyCount { get; }
        public int DashConvergedBodyCount { get; }
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
