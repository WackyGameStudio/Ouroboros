using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Ouroboros.Core
{
    public sealed class OSDataValidationReport
    {
        private readonly ReadOnlyCollection<string> _errors;

        public OSDataValidationReport(IList<string> errors)
        {
            var copy = errors == null ? Array.Empty<string>() : new List<string>(errors).ToArray();
            _errors = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<string> Errors => _errors;
        public bool IsValid => _errors.Count == 0;

        public string Message
        {
            get
            {
                if (IsValid)
                {
                    return "Data validation passed.";
                }

                var builder = new StringBuilder("Data validation failed:");
                for (var i = 0; i < _errors.Count; i++)
                {
                    builder.Append("\n- ").Append(_errors[i]);
                }

                return builder.ToString();
            }
        }
    }

    public static class OSDataValidator
    {
        public static OSRuleResult<OSDataValidationReport> Validate(
            OSPlayerBalanceData player,
            OSBodyBalanceData body,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSUpgradeCatalog upgrades,
            OSFeedbackCatalog feedback)
        {
            var errors = new List<string>(32);
            Collect(player, "player", errors);
            Collect(body, "body", errors);
            Collect(encounter, "encounter", errors);
            Collect(waves, "waves", errors);
            Collect(upgrades, "upgrades", errors);
            Collect(feedback, "feedback", errors);

            if (encounter != null && waves != null)
            {
                ValidateWaveEnemyReferences(encounter, waves, errors);
            }

            var report = new OSDataValidationReport(errors);
            return report.IsValid
                ? OSRuleResult<OSDataValidationReport>.Accepted(report, "data.valid")
                : OSRuleResult<OSDataValidationReport>.Rejected(
                    OSResultCode.ConfigurationError,
                    "data.configuration_error",
                    report);
        }

        private static void Collect(IOSValidatableData data, string name, List<string> errors)
        {
            if (data == null)
            {
                errors.Add($"{name}: required data asset is missing.");
                return;
            }

            data.CollectValidationErrors(errors, name);
        }

        private static void ValidateWaveEnemyReferences(
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            List<string> errors)
        {
            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            encounter.CollectEnemyIds(enemyIds);

            for (var i = 0; i < waves.Entries.Count; i++)
            {
                var entry = waves.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                for (var j = 0; j < entry.EnemyWeights.Count; j++)
                {
                    var weight = entry.EnemyWeights[j];
                    if (weight != null && !string.IsNullOrWhiteSpace(weight.EnemyId) &&
                        !enemyIds.Contains(weight.EnemyId))
                    {
                        errors.Add($"waves.entries[{i}].weights[{j}]: unknown enemy ID '{weight.EnemyId}'.");
                    }
                }
            }
        }
    }

    public interface IOSValidatableData
    {
        void CollectValidationErrors(List<string> errors, string path);
    }

    internal static class OSValidationUtility
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void RequireVersion(string value, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{path}: dataVersion is required.");
            }
        }

        public static void RequireId(string value, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{path}: ID is required.");
                return;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    continue;
                }

                errors.Add($"{path}: ID '{value}' must use lowercase snake_case.");
                return;
            }
        }

        public static void RequireFiniteNonNegative(float value, string path, List<string> errors)
        {
            if (!IsFinite(value) || value < 0f)
            {
                errors.Add($"{path}: expected a finite value greater than or equal to 0, actual {value}.");
            }
        }

        public static void RequireFinitePositive(float value, string path, List<string> errors)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                errors.Add($"{path}: expected a finite value greater than 0, actual {value}.");
            }
        }

        public static void RequireAtLeastOne(int value, string path, List<string> errors)
        {
            if (value < 1)
            {
                errors.Add($"{path}: expected a value of at least 1, actual {value}.");
            }
        }

        public static void RequireUniqueId(
            string value,
            string path,
            HashSet<string> ids,
            List<string> errors)
        {
            RequireId(value, path, errors);
            if (!string.IsNullOrWhiteSpace(value) && !ids.Add(value))
            {
                errors.Add($"{path}: duplicate ID '{value}'.");
            }
        }
    }
}
