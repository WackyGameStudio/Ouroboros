using System;
using System.Collections.Generic;

namespace Ouroboros.Core
{
    public readonly struct OSWaveRuntimeWeight
    {
        public OSWaveRuntimeWeight(string enemyId, float weight)
        {
            EnemyId = enemyId ?? string.Empty;
            Weight = float.IsFinite(weight) ? Math.Max(0f, weight) : 0f;
        }

        public string EnemyId { get; }
        public float Weight { get; }
    }

    public readonly struct OSWaveScheduleEntryRuntime
    {
        public OSWaveScheduleEntryRuntime(
            float startSeconds,
            float endSeconds,
            IReadOnlyList<OSWaveRuntimeWeight> enemyWeights,
            float spawnRate,
            OSWaveSpecialEvent specialEvent,
            int targetActiveEnemies)
        {
            StartSeconds = Math.Max(0f, startSeconds);
            EndSeconds = Math.Max(StartSeconds, endSeconds);
            EnemyWeights = CopyWeights(enemyWeights);
            SpawnRate = Math.Max(0f, spawnRate);
            SpecialEvent = specialEvent;
            TargetActiveEnemies = Math.Max(1, targetActiveEnemies);
        }

        public OSWaveScheduleEntryRuntime(OSWaveEntry source)
        {
            StartSeconds = Math.Max(0f, source?.StartSeconds ?? 0f);
            EndSeconds = Math.Max(StartSeconds, source?.EndSeconds ?? 0f);
            SpawnRate = Math.Max(0f, source?.SpawnRate ?? 0f);
            SpecialEvent = source?.SpecialEvent ?? OSWaveSpecialEvent.None;
            TargetActiveEnemies = Math.Max(1, source?.TargetActiveEnemies ?? 1);

            var sourceWeights = source?.EnemyWeights;
            EnemyWeights = new OSWaveRuntimeWeight[sourceWeights?.Count ?? 0];
            for (var index = 0; index < EnemyWeights.Length; index++)
            {
                var weight = sourceWeights[index];
                EnemyWeights[index] = weight != null
                    ? new OSWaveRuntimeWeight(weight.EnemyId, weight.Weight)
                    : default;
            }
        }

        public float StartSeconds { get; }
        public float EndSeconds { get; }
        public OSWaveRuntimeWeight[] EnemyWeights { get; }
        public float SpawnRate { get; }
        public OSWaveSpecialEvent SpecialEvent { get; }
        public int TargetActiveEnemies { get; }
        public bool HasNormalSpawns => EnemyWeights != null && EnemyWeights.Length > 0 && SpawnRate > 0f;

        private static OSWaveRuntimeWeight[] CopyWeights(IReadOnlyList<OSWaveRuntimeWeight> source)
        {
            var copy = new OSWaveRuntimeWeight[source?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public sealed class OSWaveScheduleRuntime
    {
        private readonly OSWaveScheduleEntryRuntime[] _entries;

        public OSWaveScheduleRuntime(IReadOnlyList<OSWaveEntry> source)
        {
            _entries = new OSWaveScheduleEntryRuntime[source?.Count ?? 0];
            for (var index = 0; index < _entries.Length; index++)
            {
                _entries[index] = new OSWaveScheduleEntryRuntime(source[index]);
            }
        }

        public OSWaveScheduleRuntime(IReadOnlyList<OSWaveScheduleEntryRuntime> source)
        {
            _entries = new OSWaveScheduleEntryRuntime[source?.Count ?? 0];
            for (var index = 0; index < _entries.Length; index++)
            {
                var entry = source[index];
                _entries[index] = new OSWaveScheduleEntryRuntime(
                    entry.StartSeconds,
                    entry.EndSeconds,
                    entry.EnemyWeights,
                    entry.SpawnRate,
                    entry.SpecialEvent,
                    entry.TargetActiveEnemies);
            }
        }

        public int Count => _entries.Length;

        public OSWaveScheduleEntryRuntime GetEntry(int index)
        {
            return index >= 0 && index < _entries.Length ? _entries[index] : default;
        }

        public int FindEntryIndex(float elapsedSeconds)
        {
            if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            {
                return -1;
            }

            for (var index = 0; index < _entries.Length; index++)
            {
                if (elapsedSeconds >= _entries[index].StartSeconds &&
                    elapsedSeconds < _entries[index].EndSeconds)
                {
                    return index;
                }
            }

            return _entries.Length > 0 && elapsedSeconds >= _entries[^1].EndSeconds
                ? _entries.Length - 1
                : -1;
        }

        public string SelectEnemyId(int entryIndex, OSRunRandom random)
        {
            if (random == null || entryIndex < 0 || entryIndex >= _entries.Length)
            {
                return string.Empty;
            }

            var weights = _entries[entryIndex].EnemyWeights;
            var total = 0f;
            for (var index = 0; index < weights.Length; index++)
            {
                total += Math.Max(0f, weights[index].Weight);
            }

            if (total <= 0f)
            {
                return string.Empty;
            }

            var roll = random.NextDouble() * total;
            for (var index = 0; index < weights.Length; index++)
            {
                roll -= Math.Max(0f, weights[index].Weight);
                if (roll <= 0d)
                {
                    return weights[index].EnemyId;
                }
            }

            return weights[^1].EnemyId;
        }

        public static float CalculateHealthMultiplier(float elapsedSeconds)
        {
            if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                return 1f;
            }

            return (float)Math.Pow(1.12d, elapsedSeconds / 60d);
        }

        public static float CalculateSpawnRateMultiplier(float elapsedSeconds)
        {
            if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                return 1f;
            }

            return (float)Math.Pow(1.15d, elapsedSeconds / 60d);
        }

        public static bool CanSpawn(int activeCount, int activeLimit, int targetActiveEnemies)
        {
            return activeLimit > 0 && targetActiveEnemies > 0 && activeCount >= 0 &&
                   activeCount < activeLimit && activeCount < targetActiveEnemies;
        }

        public static bool Crossed(float previousSeconds, float currentSeconds, float eventSeconds)
        {
            return float.IsFinite(previousSeconds) && float.IsFinite(currentSeconds) &&
                   float.IsFinite(eventSeconds) && previousSeconds < eventSeconds &&
                   currentSeconds >= eventSeconds;
        }
    }
}
