using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    [Serializable]
    public sealed class OSUpgradeDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private OSUpgradeCategory category;
        [SerializeField] private int maxLevel = 1;
        [SerializeField] private OSUpgradeOperation operation;
        [SerializeField] private float perLevelValue;
        [SerializeField] private float clampMinimum;
        [SerializeField] private float clampMaximum = 1f;
        [SerializeField] private float candidateWeight = 1f;

        public string Id => id;
        public OSUpgradeCategory Category => category;
        public int MaxLevel => maxLevel;
        public OSUpgradeOperation Operation => operation;
        public float PerLevelValue => perLevelValue;
        public float ClampMinimum => clampMinimum;
        public float ClampMaximum => clampMaximum;
        public float CandidateWeight => candidateWeight;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireAtLeastOne(maxLevel, $"{path}.maxLevel", errors);
            if (!OSValidationUtility.IsFinite(perLevelValue))
            {
                errors.Add($"{path}.perLevelValue: expected a finite value, actual {perLevelValue}.");
            }

            if (!OSValidationUtility.IsFinite(clampMinimum))
            {
                errors.Add($"{path}.clampMinimum: expected a finite value, actual {clampMinimum}.");
            }

            if (!OSValidationUtility.IsFinite(clampMaximum))
            {
                errors.Add($"{path}.clampMaximum: expected a finite value, actual {clampMaximum}.");
            }
            else if (OSValidationUtility.IsFinite(clampMinimum) && clampMaximum < clampMinimum)
            {
                errors.Add($"{path}: clampMaximum must be greater than or equal to clampMinimum.");
            }

            OSValidationUtility.RequireFinitePositive(candidateWeight, $"{path}.candidateWeight", errors);
        }
    }

    [CreateAssetMenu(fileName = "OSUpgradeCatalog", menuName = "Ouroboros/Data/Upgrade Catalog")]
    public sealed class OSUpgradeCatalog : ScriptableObject, IOSValidatableData
    {
        public const int RequiredUpgradeCount = 15;

        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private List<OSUpgradeDefinition> entries = new();

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public IReadOnlyList<OSUpgradeDefinition> Entries => entries;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSUpgradeCatalog));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            if (entries == null)
            {
                errors.Add($"{path}.entries: list is missing.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var eligibleCount = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var definition = entries[i];
                var itemPath = $"{path}.entries[{i}]";
                if (definition == null)
                {
                    errors.Add($"{itemPath}: definition is null.");
                    continue;
                }

                OSValidationUtility.RequireUniqueId(definition.Id, itemPath, ids, errors);
                definition.CollectValidationErrors(errors, itemPath);
                if (definition.MaxLevel > 0 && OSValidationUtility.IsFinite(definition.CandidateWeight) &&
                    definition.CandidateWeight > 0f)
                {
                    eligibleCount++;
                }
            }

            if (eligibleCount < 3)
            {
                errors.Add($"{path}.entries: at least 3 eligible upgrade candidates are required, actual {eligibleCount}.");
            }

            if (entries.Count != RequiredUpgradeCount)
            {
                errors.Add($"{path}.entries: expected {RequiredUpgradeCount} upgrade definitions, actual {entries.Count}.");
            }
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
