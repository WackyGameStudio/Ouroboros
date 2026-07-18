using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Ouroboros.Core
{
    public readonly struct OSBodyRoleRuntimeDefinition
    {
        public OSBodyRoleRuntimeDefinition(OSBodyRoleDefinition source)
        {
            Id = source.Id;
            RoleType = source.RoleType;
            Range = source.Range;
            Damage = source.Damage;
            Interval = source.Interval;
            Radius = source.Radius;
            Charges = source.Charges;
            RechargeDuration = source.RechargeDuration;
            BeamWidth = source.BeamWidth;
            TelegraphDuration = source.TelegraphDuration;
            NormalControlDuration = source.NormalControlDuration;
            EliteControlDuration = source.EliteControlDuration;
        }

        public string Id { get; }
        public OSBodyRoleType RoleType { get; }
        public float Range { get; }
        public float Damage { get; }
        public float Interval { get; }
        public float Radius { get; }
        public int Charges { get; }
        public float RechargeDuration { get; }
        public float BeamWidth { get; }
        public float TelegraphDuration { get; }
        public float NormalControlDuration { get; }
        public float EliteControlDuration { get; }
    }

    public readonly struct OSExplosionRuntimeSettings
    {
        public OSExplosionRuntimeSettings(OSExplosionSettings source)
        {
            MinimumSegments = source.MinimumSegments;
            ConsumeRate = source.ConsumeRate;
            TelegraphDuration = source.TelegraphDuration;
            Radius = source.Radius;
            DamagePerSegment = source.DamagePerSegment;
            HeadInvulnerability = source.HeadInvulnerability;
        }

        public int MinimumSegments { get; }
        public float ConsumeRate { get; }
        public float TelegraphDuration { get; }
        public float Radius { get; }
        public float DamagePerSegment { get; }
        public float HeadInvulnerability { get; }
    }

    public readonly struct OSDropRuntimeData
    {
        public OSDropRuntimeData(OSDropTable source)
        {
            ExperienceAmount = source.ExperienceAmount;
            FragmentAmount = source.FragmentAmount;
            FragmentChance = source.FragmentChance;
            HealAmount = source.HealAmount;
            HealChance = source.HealChance;
        }

        public int ExperienceAmount { get; }
        public int FragmentAmount { get; }
        public float FragmentChance { get; }
        public int HealAmount { get; }
        public float HealChance { get; }
    }

    public readonly struct OSEnemyRuntimeDefinition
    {
        public OSEnemyRuntimeDefinition(OSEnemyDefinition source)
        {
            Id = source.Id;
            Archetype = source.Archetype;
            Prefab = source.Prefab;
            MaxHealth = source.MaxHealth;
            MoveSpeed = source.MoveSpeed;
            ContactDamage = source.ContactDamage;
            AttackInterval = source.AttackInterval;
            DropTable = new OSDropRuntimeData(source.DropTable);
            ControlAffectsMovement = source.ControlAffectsMovement;
            ControlAffectsAttack = source.ControlAffectsAttack;
            PoolCapacity = source.PoolCapacity;
        }

        public string Id { get; }
        public OSEnemyArchetype Archetype { get; }
        public GameObject Prefab { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float ContactDamage { get; }
        public float AttackInterval { get; }
        public OSDropRuntimeData DropTable { get; }
        public bool ControlAffectsMovement { get; }
        public bool ControlAffectsAttack { get; }
        public int PoolCapacity { get; }
    }

    public readonly struct OSWeightedEnemyRuntimeEntry
    {
        public OSWeightedEnemyRuntimeEntry(OSWeightedEnemyEntry source)
        {
            EnemyId = source.EnemyId;
            Weight = source.Weight;
        }

        public string EnemyId { get; }
        public float Weight { get; }
    }

    public sealed class OSWaveRuntimeEntry
    {
        public OSWaveRuntimeEntry(OSWaveEntry source)
        {
            StartSeconds = source.StartSeconds;
            EndSeconds = source.EndSeconds;
            SpawnRate = source.SpawnRate;
            SpecialEvent = source.SpecialEvent;
            TargetActiveEnemies = source.TargetActiveEnemies;

            var weights = new OSWeightedEnemyRuntimeEntry[source.EnemyWeights.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = new OSWeightedEnemyRuntimeEntry(source.EnemyWeights[i]);
            }

            EnemyWeights = Array.AsReadOnly(weights);
        }

        public float StartSeconds { get; }
        public float EndSeconds { get; }
        public float SpawnRate { get; }
        public OSWaveSpecialEvent SpecialEvent { get; }
        public int TargetActiveEnemies { get; }
        public IReadOnlyList<OSWeightedEnemyRuntimeEntry> EnemyWeights { get; }
    }

    public readonly struct OSUpgradeRuntimeDefinition
    {
        public OSUpgradeRuntimeDefinition(OSUpgradeDefinition source)
        {
            Id = source.Id;
            Category = source.Category;
            MaxLevel = source.MaxLevel;
            Operation = source.Operation;
            PerLevelValue = source.PerLevelValue;
            ClampMinimum = source.ClampMinimum;
            ClampMaximum = source.ClampMaximum;
            CandidateWeight = source.CandidateWeight;
        }

        public string Id { get; }
        public OSUpgradeCategory Category { get; }
        public int MaxLevel { get; }
        public OSUpgradeOperation Operation { get; }
        public float PerLevelValue { get; }
        public float ClampMinimum { get; }
        public float ClampMaximum { get; }
        public float CandidateWeight { get; }
    }

    public readonly struct OSRoleVisualRuntimeDefinition
    {
        public OSRoleVisualRuntimeDefinition(OSRoleVisualDefinition source)
        {
            Id = source.Id;
            RoleType = source.RoleType;
            Sprite = source.Sprite;
            Color = source.Color;
            PatternKey = source.PatternKey;
        }

        public string Id { get; }
        public OSBodyRoleType RoleType { get; }
        public Sprite Sprite { get; }
        public Color Color { get; }
        public string PatternKey { get; }
    }

    public sealed class OSSessionRuntimeState
    {
        private OSSessionRuntimeState(
            OSPlayerBalanceData player,
            OSBodyBalanceData body,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSUpgradeCatalog upgrades,
            OSFeedbackCatalog feedback)
        {
            DataVersion = string.Join(
                "|",
                player.DataVersion,
                body.DataVersion,
                encounter.DataVersion,
                waves.DataVersion,
                upgrades.DataVersion,
                feedback.DataVersion);

            MaxHealth = player.MaxHealth;
            CurrentHealth = player.MaxHealth;
            MoveSpeed = player.MoveSpeed;
            HitInvulnerability = player.HitInvulnerability;
            HeadDamage = player.HeadDamage;
            HeadFireInterval = player.HeadFireInterval;
            HeadRange = player.HeadRange;
            MagnetRadius = player.MagnetRadius;

            FragmentRequirement = body.FragmentRequirement;
            TechnicalGuard = body.TechnicalGuard;
            SegmentSpacing = body.SegmentSpacing;
            PathSampleInterval = body.PathSampleInterval;
            PathReserveDistance = body.PathReserveDistance;
            BodyDamageRate = body.BodyDamageRate;
            CutGuardDuration = body.CutGuardDuration;
            Explosion = new OSExplosionRuntimeSettings(body.Explosion);
            BodyRoles = CopyBodyRoles(body.RoleDefinitions);

            ActiveEnemyLimit = encounter.ActiveEnemyLimit;
            ProjectileLimit = encounter.ProjectileLimit;
            PickupLimit = encounter.PickupLimit;
            VfxLimit = encounter.VfxLimit;
            EnemyDefinitions = CopyEnemies(encounter);
            WaveEntries = CopyWaves(waves.Entries);
            UpgradeDefinitions = CopyUpgrades(upgrades.Entries);
            UpgradeLevels = CreateUpgradeLevels(UpgradeDefinitions);
            RoleVisuals = CopyRoleVisuals(feedback.RoleVisuals);
            AttackVfxKeys = CopyStrings(feedback.AttackVfxKeys);
            TelegraphKeys = CopyStrings(feedback.TelegraphKeys);
            AudioKeys = CopyStrings(feedback.AudioKeys);
        }

        public string DataVersion { get; }
        public int MaxHealth { get; }
        public int CurrentHealth { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public float HitInvulnerability { get; }
        public float HeadDamage { get; internal set; }
        public float HeadFireInterval { get; internal set; }
        public float HeadRange { get; }
        public float MagnetRadius { get; internal set; }
        public int FragmentRequirement { get; internal set; }
        public int TechnicalGuard { get; }
        public float SegmentSpacing { get; }
        public float PathSampleInterval { get; }
        public float PathReserveDistance { get; }
        public float BodyDamageRate { get; internal set; }
        public float CutGuardDuration { get; }
        public OSExplosionRuntimeSettings Explosion { get; internal set; }
        public IReadOnlyList<OSBodyRoleRuntimeDefinition> BodyRoles { get; }
        public int ActiveEnemyLimit { get; }
        public int ProjectileLimit { get; }
        public int PickupLimit { get; }
        public int VfxLimit { get; }
        public IReadOnlyList<OSEnemyRuntimeDefinition> EnemyDefinitions { get; }
        public IReadOnlyList<OSWaveRuntimeEntry> WaveEntries { get; }
        public IReadOnlyList<OSUpgradeRuntimeDefinition> UpgradeDefinitions { get; }
        public IReadOnlyDictionary<string, int> UpgradeLevels { get; }
        public IReadOnlyList<OSRoleVisualRuntimeDefinition> RoleVisuals { get; }
        public IReadOnlyList<string> AttackVfxKeys { get; }
        public IReadOnlyList<string> TelegraphKeys { get; }
        public IReadOnlyList<string> AudioKeys { get; }

        public static OSRuleResult<OSSessionRuntimeState> InitializeFrom(
            OSPlayerBalanceData player,
            OSBodyBalanceData body,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSUpgradeCatalog upgrades,
            OSFeedbackCatalog feedback)
        {
            return InitializeFrom(player, body, encounter, waves, upgrades, feedback, out _);
        }

        public static OSRuleResult<OSSessionRuntimeState> InitializeFrom(
            OSPlayerBalanceData player,
            OSBodyBalanceData body,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSUpgradeCatalog upgrades,
            OSFeedbackCatalog feedback,
            out OSDataValidationReport validationReport)
        {
            var validation = OSDataValidator.Validate(player, body, encounter, waves, upgrades, feedback);
            validationReport = validation.Payload;
            if (!validation.IsAccepted)
            {
                return OSRuleResult<OSSessionRuntimeState>.Rejected(
                    OSResultCode.ConfigurationError,
                    validation.ReasonKey);
            }

            return OSRuleResult<OSSessionRuntimeState>.Accepted(
                new OSSessionRuntimeState(player, body, encounter, waves, upgrades, feedback),
                "session.initialized");
        }

        private static IReadOnlyList<OSBodyRoleRuntimeDefinition> CopyBodyRoles(
            IReadOnlyList<OSBodyRoleDefinition> source)
        {
            var copy = new OSBodyRoleRuntimeDefinition[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = new OSBodyRoleRuntimeDefinition(source[i]);
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<OSEnemyRuntimeDefinition> CopyEnemies(OSEncounterBalanceData source)
        {
            var copy = new OSEnemyRuntimeDefinition[source.EnemyDefinitions.Count + 2];
            for (var i = 0; i < source.EnemyDefinitions.Count; i++)
            {
                copy[i] = new OSEnemyRuntimeDefinition(source.EnemyDefinitions[i]);
            }

            copy[copy.Length - 2] = new OSEnemyRuntimeDefinition(source.EliteDefinition);
            copy[copy.Length - 1] = new OSEnemyRuntimeDefinition(source.BossDefinition);
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<OSWaveRuntimeEntry> CopyWaves(IReadOnlyList<OSWaveEntry> source)
        {
            var copy = new OSWaveRuntimeEntry[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = new OSWaveRuntimeEntry(source[i]);
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<OSUpgradeRuntimeDefinition> CopyUpgrades(
            IReadOnlyList<OSUpgradeDefinition> source)
        {
            var copy = new OSUpgradeRuntimeDefinition[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = new OSUpgradeRuntimeDefinition(source[i]);
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyDictionary<string, int> CreateUpgradeLevels(
            IReadOnlyList<OSUpgradeRuntimeDefinition> upgrades)
        {
            var levels = new Dictionary<string, int>(upgrades.Count, StringComparer.Ordinal);
            for (var i = 0; i < upgrades.Count; i++)
            {
                levels.Add(upgrades[i].Id, 0);
            }

            return new ReadOnlyDictionary<string, int>(levels);
        }

        private static IReadOnlyList<OSRoleVisualRuntimeDefinition> CopyRoleVisuals(
            IReadOnlyList<OSRoleVisualDefinition> source)
        {
            var copy = new OSRoleVisualRuntimeDefinition[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = new OSRoleVisualRuntimeDefinition(source[i]);
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> source)
        {
            var copy = new string[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
