using System;
using System.Collections.Generic;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public interface IOSPoolRentInitializer
    {
        void PrepareForRent(OSPoolableBehaviour instance);
    }

    [Serializable]
    public sealed class OSPoolPrewarmEntry
    {
        [SerializeField] private string key = string.Empty;
        [SerializeField] private OSPoolableBehaviour prefab;
        [SerializeField, Min(1)] private int capacity = 1;

        public OSPoolPrewarmEntry(string key, OSPoolableBehaviour prefab, int capacity)
        {
            this.key = key ?? string.Empty;
            this.prefab = prefab;
            this.capacity = Mathf.Max(1, capacity);
        }

        public string Key => key;
        public OSPoolableBehaviour Prefab => prefab;
        public int Capacity => capacity;
    }

    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public sealed class OSPoolRegistry : MonoBehaviour
    {
        private sealed class PoolBucket
        {
            public string Key;
            public OSPoolableBehaviour[] Instances;
            public int[] AvailableIndices;
            public int AvailableCount;
        }

        [SerializeField] private Transform poolRoot;
        [SerializeField] private MonoBehaviour rentInitializer;
        [SerializeField] private OSPoolPrewarmEntry[] entries = Array.Empty<OSPoolPrewarmEntry>();

        private readonly Dictionary<string, int> _bucketByKey = new(StringComparer.Ordinal);
        private PoolBucket[] _buckets = Array.Empty<PoolBucket>();
        private IOSPoolRentInitializer _initializer;
        private int _nextRuntimeId = 1;
        private int _nextAttackEventId = 1;
        private bool _built;

        public int PoolCount => _buckets.Length;

        private void Awake()
        {
            BuildPools();
        }

        public OSRuleResult<OSPoolableBehaviour> Rent(string key, Vector3 position, Quaternion rotation)
        {
            if (!_built)
            {
                BuildPools();
            }

            if (string.IsNullOrWhiteSpace(key) || !_bucketByKey.TryGetValue(key, out var bucketIndex))
            {
                return OSRuleResult<OSPoolableBehaviour>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.rent.unknown_key");
            }

            var bucket = _buckets[bucketIndex];
            if (bucket.AvailableCount <= 0)
            {
                return OSRuleResult<OSPoolableBehaviour>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "pool.rent.exhausted");
            }

            if (_nextRuntimeId == int.MaxValue)
            {
                return OSRuleResult<OSPoolableBehaviour>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.runtime_id.exhausted");
            }

            var instanceIndex = bucket.AvailableIndices[--bucket.AvailableCount];
            var instance = bucket.Instances[instanceIndex];
            _initializer?.PrepareForRent(instance);
            instance.PrepareRent(_nextRuntimeId++, position, rotation);
            return OSRuleResult<OSPoolableBehaviour>.Accepted(instance, "pool.rent.accepted");
        }

        public OSRuleResult<int> Return(OSPoolableBehaviour instance)
        {
            if (instance == null || instance.PoolOwner != this)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.return.foreign_instance");
            }

            if (!instance.IsRented)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.Duplicate,
                    "pool.return.duplicate");
            }

            var bucketIndex = instance.PoolBucketIndex;
            var instanceIndex = instance.PoolInstanceIndex;
            if (bucketIndex < 0 || bucketIndex >= _buckets.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.return.invalid_bucket");
            }

            var bucket = _buckets[bucketIndex];
            if (instanceIndex < 0 || instanceIndex >= bucket.Instances.Length ||
                bucket.Instances[instanceIndex] != instance)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.return.invalid_instance");
            }

            var returnedRuntimeId = instance.RuntimeId;
            instance.PrepareReturn(poolRoot != null ? poolRoot : transform);
            bucket.AvailableIndices[bucket.AvailableCount++] = instanceIndex;
            return OSRuleResult<int>.Accepted(returnedRuntimeId, "pool.return.accepted");
        }

        public int GetCapacity(string key)
        {
            return _bucketByKey.TryGetValue(key, out var index) ? _buckets[index].Instances.Length : 0;
        }

        public int GetActiveCount(string key)
        {
            return _bucketByKey.TryGetValue(key, out var index)
                ? _buckets[index].Instances.Length - _buckets[index].AvailableCount
                : 0;
        }

        public int ReturnAll(string key)
        {
            if (!_bucketByKey.TryGetValue(key, out var bucketIndex))
            {
                return 0;
            }

            var returned = 0;
            var bucket = _buckets[bucketIndex];
            for (var index = 0; index < bucket.Instances.Length; index++)
            {
                var instance = bucket.Instances[index];
                if (instance != null && instance.IsRented && Return(instance).IsAccepted)
                {
                    returned++;
                }
            }

            return returned;
        }

        public OSRuleResult<int> NextAttackEventId()
        {
            return _nextAttackEventId == int.MaxValue
                ? OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "pool.attack_event_id.exhausted")
                : OSRuleResult<int>.Accepted(_nextAttackEventId++, "pool.attack_event_id.accepted");
        }

        internal void ConfigureForTesting(
            Transform root,
            MonoBehaviour initializer,
            params OSPoolPrewarmEntry[] prewarmEntries)
        {
            poolRoot = root;
            rentInitializer = initializer;
            entries = prewarmEntries ?? Array.Empty<OSPoolPrewarmEntry>();
            _built = false;
            BuildPools();
        }

        private void BuildPools()
        {
            if (_built)
            {
                return;
            }

            poolRoot ??= transform;
            _initializer = rentInitializer as IOSPoolRentInitializer;
            _bucketByKey.Clear();
            _buckets = new PoolBucket[entries?.Length ?? 0];

            for (var bucketIndex = 0; bucketIndex < _buckets.Length; bucketIndex++)
            {
                var entry = entries[bucketIndex];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Prefab == null ||
                    entry.Capacity <= 0)
                {
                    throw new InvalidOperationException(
                        $"Pool entry {bucketIndex} is missing a key, prefab, or positive capacity.");
                }

                if (!_bucketByKey.TryAdd(entry.Key, bucketIndex))
                {
                    throw new InvalidOperationException($"Duplicate pool key '{entry.Key}'.");
                }

                var bucket = new PoolBucket
                {
                    Key = entry.Key,
                    Instances = new OSPoolableBehaviour[entry.Capacity],
                    AvailableIndices = new int[entry.Capacity],
                    AvailableCount = entry.Capacity
                };

                for (var instanceIndex = 0; instanceIndex < entry.Capacity; instanceIndex++)
                {
                    var instance = Instantiate(entry.Prefab, poolRoot);
                    instance.name = $"{entry.Key}_{instanceIndex:000}";
                    instance.InitializePoolOwnership(this, entry.Key, bucketIndex, instanceIndex);
                    instance.gameObject.SetActive(false);
                    bucket.Instances[instanceIndex] = instance;
                    bucket.AvailableIndices[entry.Capacity - 1 - instanceIndex] = instanceIndex;
                }

                _buckets[bucketIndex] = bucket;
            }

            _built = true;
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = Array.Empty<OSPoolPrewarmEntry>();
            }
        }
    }
}
