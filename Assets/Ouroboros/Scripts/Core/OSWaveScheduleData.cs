using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    [Serializable]
    public sealed class OSWeightedEnemyEntry
    {
        [SerializeField] private string enemyId;
        [SerializeField] private float weight;

        public string EnemyId => enemyId;
        public float Weight => weight;
    }

    [Serializable]
    public sealed class OSWaveEntry
    {
        [SerializeField] private float startSeconds;
        [SerializeField] private float endSeconds;
        [SerializeField] private List<OSWeightedEnemyEntry> enemyWeights = new();
        [SerializeField] private float spawnRate = 1f;
        [SerializeField] private OSWaveSpecialEvent specialEvent;
        [SerializeField] private int targetActiveEnemies = 1;

        public float StartSeconds => startSeconds;
        public float EndSeconds => endSeconds;
        public IReadOnlyList<OSWeightedEnemyEntry> EnemyWeights => enemyWeights;
        public float SpawnRate => spawnRate;
        public OSWaveSpecialEvent SpecialEvent => specialEvent;
        public int TargetActiveEnemies => targetActiveEnemies;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireFiniteNonNegative(startSeconds, $"{path}.startSeconds", errors);
            OSValidationUtility.RequireFinitePositive(endSeconds, $"{path}.endSeconds", errors);
            if (OSValidationUtility.IsFinite(startSeconds) && OSValidationUtility.IsFinite(endSeconds) &&
                endSeconds <= startSeconds)
            {
                errors.Add($"{path}: endSeconds must be greater than startSeconds.");
            }

            OSValidationUtility.RequireFiniteNonNegative(spawnRate, $"{path}.spawnRate", errors);
            OSValidationUtility.RequireAtLeastOne(
                targetActiveEnemies,
                $"{path}.targetActiveEnemies",
                errors);

            if (enemyWeights == null)
            {
                errors.Add($"{path}.enemyWeights: list is missing.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var weightSum = 0f;
            for (var i = 0; i < enemyWeights.Count; i++)
            {
                var entry = enemyWeights[i];
                var weightPath = $"{path}.enemyWeights[{i}]";
                if (entry == null)
                {
                    errors.Add($"{weightPath}: weight entry is null.");
                    continue;
                }

                OSValidationUtility.RequireUniqueId(entry.EnemyId, weightPath, ids, errors);
                OSValidationUtility.RequireFiniteNonNegative(entry.Weight, $"{weightPath}.weight", errors);
                if (OSValidationUtility.IsFinite(entry.Weight))
                {
                    weightSum += entry.Weight;
                }
            }

            if (specialEvent == OSWaveSpecialEvent.None)
            {
                if (enemyWeights.Count == 0)
                {
                    errors.Add($"{path}.enemyWeights: a normal wave requires at least one enemy weight.");
                }

                if (!OSValidationUtility.IsFinite(weightSum) || Math.Abs(weightSum - 1f) > 0.001f)
                {
                    errors.Add($"{path}.enemyWeights: normal wave weights must sum to 1, actual {weightSum}.");
                }

                OSValidationUtility.RequireFinitePositive(spawnRate, $"{path}.spawnRate", errors);
            }
        }
    }

    [CreateAssetMenu(fileName = "OSWaveSchedule", menuName = "Ouroboros/Data/Wave Schedule")]
    public sealed class OSWaveScheduleData : ScriptableObject, IOSValidatableData
    {
        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private List<OSWaveEntry> entries = new();

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public IReadOnlyList<OSWaveEntry> Entries => entries;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSWaveScheduleData));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            if (entries == null || entries.Count == 0)
            {
                errors.Add($"{path}.entries: at least one wave entry is required.");
                return;
            }

            var previousEnd = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entryPath = $"{path}.entries[{i}]";
                if (entry == null)
                {
                    errors.Add($"{entryPath}: entry is null.");
                    continue;
                }

                entry.CollectValidationErrors(errors, entryPath);
                if (i > 0 && OSValidationUtility.IsFinite(entry.StartSeconds) &&
                    entry.StartSeconds < previousEnd - 0.001f)
                {
                    errors.Add($"{entryPath}: wave overlaps the previous entry.");
                }

                previousEnd = entry.EndSeconds;
            }
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
