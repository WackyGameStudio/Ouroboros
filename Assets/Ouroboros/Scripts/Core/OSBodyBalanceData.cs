using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ouroboros.Core
{
    [Serializable]
    public sealed class OSBodyRoleDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private OSBodyRoleType roleType;
        [SerializeField] private float range;
        [SerializeField] private float damage;
        [SerializeField] private float interval;
        [SerializeField] private float radius;
        [SerializeField] private int charges;
        [SerializeField] private float rechargeDuration;
        [SerializeField] private float beamWidth;
        [SerializeField] private float telegraphDuration;
        [SerializeField] private float normalControlDuration;
        [SerializeField] private float eliteControlDuration;

        public string Id => id;
        public OSBodyRoleType RoleType => roleType;
        public float Range => range;
        public float Damage => damage;
        public float Interval => interval;
        public float Radius => radius;
        public int Charges => charges;
        public float RechargeDuration => rechargeDuration;
        public float BeamWidth => beamWidth;
        public float TelegraphDuration => telegraphDuration;
        public float NormalControlDuration => normalControlDuration;
        public float EliteControlDuration => eliteControlDuration;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireFiniteNonNegative(range, $"{path}.range", errors);
            OSValidationUtility.RequireFiniteNonNegative(damage, $"{path}.damage", errors);
            OSValidationUtility.RequireFiniteNonNegative(interval, $"{path}.interval", errors);
            OSValidationUtility.RequireFiniteNonNegative(radius, $"{path}.radius", errors);
            if (charges < 0)
            {
                errors.Add($"{path}.charges: expected a value greater than or equal to 0, actual {charges}.");
            }

            OSValidationUtility.RequireFiniteNonNegative(
                rechargeDuration,
                $"{path}.rechargeDuration",
                errors);
            OSValidationUtility.RequireFiniteNonNegative(beamWidth, $"{path}.beamWidth", errors);
            OSValidationUtility.RequireFiniteNonNegative(
                telegraphDuration,
                $"{path}.telegraphDuration",
                errors);
            OSValidationUtility.RequireFiniteNonNegative(
                normalControlDuration,
                $"{path}.normalControlDuration",
                errors);
            OSValidationUtility.RequireFiniteNonNegative(
                eliteControlDuration,
                $"{path}.eliteControlDuration",
                errors);

            switch (roleType)
            {
                case OSBodyRoleType.Shield:
                    OSValidationUtility.RequireFinitePositive(radius, $"{path}.radius", errors);
                    OSValidationUtility.RequireAtLeastOne(charges, $"{path}.charges", errors);
                    OSValidationUtility.RequireFinitePositive(
                        rechargeDuration,
                        $"{path}.rechargeDuration",
                        errors);
                    break;
                case OSBodyRoleType.Attack:
                    RequireAttackValues(errors, path);
                    break;
                case OSBodyRoleType.Laser:
                    RequireAttackValues(errors, path);
                    OSValidationUtility.RequireFinitePositive(beamWidth, $"{path}.beamWidth", errors);
                    break;
                case OSBodyRoleType.Control:
                    OSValidationUtility.RequireFinitePositive(range, $"{path}.range", errors);
                    OSValidationUtility.RequireFinitePositive(interval, $"{path}.interval", errors);
                    OSValidationUtility.RequireFinitePositive(
                        normalControlDuration,
                        $"{path}.normalControlDuration",
                        errors);
                    OSValidationUtility.RequireFinitePositive(
                        eliteControlDuration,
                        $"{path}.eliteControlDuration",
                        errors);
                    break;
            }
        }

        private void RequireAttackValues(List<string> errors, string path)
        {
            OSValidationUtility.RequireFinitePositive(range, $"{path}.range", errors);
            OSValidationUtility.RequireFinitePositive(damage, $"{path}.damage", errors);
            OSValidationUtility.RequireFinitePositive(interval, $"{path}.interval", errors);
        }
    }

    [Serializable]
    public sealed class OSBodyDashSettings
    {
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private float distance = 4.5f;
        [SerializeField] private float cooldown = 2f;

        public float Duration => duration;
        public float Distance => distance;
        public float Cooldown => cooldown;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireFinitePositive(duration, $"{path}.duration", errors);
            OSValidationUtility.RequireFinitePositive(distance, $"{path}.distance", errors);
            OSValidationUtility.RequireFinitePositive(cooldown, $"{path}.cooldown", errors);
        }
    }

    [Serializable]
    public sealed class OSBombSettings
    {
        [SerializeField] private int minimumBodyCount = OSBombMath.DefaultMinimumBodyCount;
        [SerializeField] private float consumeRate = OSBombMath.DefaultConsumeRate;
        [SerializeField] private float drawDuration = OSBombMath.DefaultDrawDuration;
        [SerializeField] private float gatherDuration = OSBombMath.DefaultGatherDuration;
        [SerializeField] private float damage = OSBombMath.DefaultDamage;
        [SerializeField] private float cooldown = OSBombMath.DefaultCooldown;

        public int MinimumBodyCount => minimumBodyCount;
        public float ConsumeRate => consumeRate;
        public float DrawDuration => drawDuration;
        public float GatherDuration => gatherDuration;
        public float Damage => damage;
        public float Cooldown => cooldown;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireAtLeastOne(minimumBodyCount, $"{path}.minimumBodyCount", errors);
            OSValidationUtility.RequireFinitePositive(consumeRate, $"{path}.consumeRate", errors);
            if (float.IsFinite(consumeRate) && consumeRate > 1f)
            {
                errors.Add($"{path}.consumeRate: expected a value no greater than 1, actual {consumeRate}.");
            }

            OSValidationUtility.RequireFinitePositive(drawDuration, $"{path}.drawDuration", errors);
            OSValidationUtility.RequireFinitePositive(gatherDuration, $"{path}.gatherDuration", errors);
            OSValidationUtility.RequireFinitePositive(damage, $"{path}.damage", errors);
            OSValidationUtility.RequireFinitePositive(cooldown, $"{path}.cooldown", errors);
        }
    }

    [CreateAssetMenu(fileName = "OSBodyBalance", menuName = "Ouroboros/Data/Body Balance")]
    public sealed class OSBodyBalanceData : ScriptableObject, IOSValidatableData
    {
        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private int fragmentRequirement = 6;
        [SerializeField] private int technicalGuard = 64;
        [SerializeField] private float segmentSpacing = 0.55f;
        [SerializeField] private float pathSampleInterval = 0.12f;
        [SerializeField] private float pathReserveDistance = 4f;
        [SerializeField] private float bodyDamageRate = 0.04f;
        [SerializeField] private float cutGuardDuration = 0.35f;
        [SerializeField] private List<OSBodyRoleDefinition> roleDefinitions = new();
        [FormerlySerializedAs("explosion")]
        [SerializeField] private OSBodyDashSettings bodyDash = new();
        [SerializeField] private OSBombSettings bomb = new();

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public int FragmentRequirement => fragmentRequirement;
        public int TechnicalGuard => technicalGuard;
        public float SegmentSpacing => segmentSpacing;
        public float PathSampleInterval => pathSampleInterval;
        public float PathReserveDistance => pathReserveDistance;
        public float BodyDamageRate => bodyDamageRate;
        public float CutGuardDuration => cutGuardDuration;
        public IReadOnlyList<OSBodyRoleDefinition> RoleDefinitions => roleDefinitions;
        public OSBodyDashSettings BodyDash => bodyDash;
        public OSBombSettings Bomb => bomb;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSBodyRoleDefinition GetRoleDefinition(OSBodyRoleType role)
        {
            if (roleDefinitions == null)
            {
                return null;
            }

            for (var index = 0; index < roleDefinitions.Count; index++)
            {
                var definition = roleDefinitions[index];
                if (definition != null && definition.RoleType == role)
                {
                    return definition;
                }
            }

            return null;
        }

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSBodyBalanceData));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            OSValidationUtility.RequireAtLeastOne(
                fragmentRequirement,
                $"{path}.fragmentRequirement",
                errors);
            OSValidationUtility.RequireAtLeastOne(technicalGuard, $"{path}.technicalGuard", errors);
            OSValidationUtility.RequireFinitePositive(segmentSpacing, $"{path}.segmentSpacing", errors);
            OSValidationUtility.RequireFinitePositive(
                pathSampleInterval,
                $"{path}.pathSampleInterval",
                errors);
            OSValidationUtility.RequireFiniteNonNegative(
                pathReserveDistance,
                $"{path}.pathReserveDistance",
                errors);
            OSValidationUtility.RequireFiniteNonNegative(bodyDamageRate, $"{path}.bodyDamageRate", errors);
            OSValidationUtility.RequireFiniteNonNegative(
                cutGuardDuration,
                $"{path}.cutGuardDuration",
                errors);

            if (roleDefinitions == null)
            {
                errors.Add($"{path}.roleDefinitions: list is missing.");
            }
            else
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var roleTypes = new HashSet<OSBodyRoleType>();
                for (var i = 0; i < roleDefinitions.Count; i++)
                {
                    var definition = roleDefinitions[i];
                    var itemPath = $"{path}.roleDefinitions[{i}]";
                    if (definition == null)
                    {
                        errors.Add($"{itemPath}: definition is null.");
                        continue;
                    }

                    OSValidationUtility.RequireUniqueId(definition.Id, itemPath, ids, errors);
                    if (!roleTypes.Add(definition.RoleType))
                    {
                        errors.Add($"{itemPath}: duplicate role type '{definition.RoleType}'.");
                    }

                    definition.CollectValidationErrors(errors, itemPath);
                }

                foreach (OSBodyRoleType roleType in Enum.GetValues(typeof(OSBodyRoleType)))
                {
                    if (!roleTypes.Contains(roleType))
                    {
                        errors.Add($"{path}.roleDefinitions: required role '{roleType}' is missing.");
                    }
                }
            }

            if (bodyDash == null)
            {
                errors.Add($"{path}.bodyDash: settings are missing.");
            }
            else
            {
                bodyDash.CollectValidationErrors(errors, $"{path}.bodyDash");
            }

            if (bomb == null)
            {
                errors.Add($"{path}.bomb: settings are missing.");
            }
            else
            {
                bomb.CollectValidationErrors(errors, $"{path}.bomb");
            }
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
