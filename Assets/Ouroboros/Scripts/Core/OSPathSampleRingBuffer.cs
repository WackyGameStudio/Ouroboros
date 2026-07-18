using System;
using UnityEngine;

namespace Ouroboros.Core
{
    public readonly struct OSPathSample
    {
        public OSPathSample(Vector2 position, float cumulativeDistance, Vector2 forward)
        {
            Position = position;
            CumulativeDistance = cumulativeDistance;
            Forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector2.right;
        }

        public Vector2 Position { get; }
        public float CumulativeDistance { get; }
        public Vector2 Forward { get; }
    }

    /// <summary>
    /// Fixed-capacity chronological path storage. Appending and querying never allocate.
    /// </summary>
    public sealed class OSPathSampleRingBuffer
    {
        private const float DistanceEpsilon = 0.00001f;

        private readonly OSPathSample[] _samples;
        private int _oldestIndex;

        public OSPathSampleRingBuffer(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Path capacity must be at least two.");
            }

            _samples = new OSPathSample[capacity];
        }

        public int Capacity => _samples.Length;
        public int Count { get; private set; }
        public OSPathSample Oldest => Count > 0
            ? this[0]
            : throw new InvalidOperationException("The path buffer is empty.");
        public OSPathSample Newest => Count > 0
            ? this[Count - 1]
            : throw new InvalidOperationException("The path buffer is empty.");

        public OSPathSample this[int chronologicalIndex]
        {
            get
            {
                if ((uint)chronologicalIndex >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
                }

                return _samples[ToPhysicalIndex(chronologicalIndex)];
            }
        }

        public static int CalculateRequiredCapacity(
            int maximumSegments,
            float segmentSpacing,
            float sampleInterval,
            float reserveDistance)
        {
            if (maximumSegments < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSegments));
            }

            if (!float.IsFinite(segmentSpacing) || segmentSpacing <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentSpacing));
            }

            if (!float.IsFinite(sampleInterval) || sampleInterval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleInterval));
            }

            if (!float.IsFinite(reserveDistance) || reserveDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveDistance));
            }

            var requiredDistance = (maximumSegments * (double)segmentSpacing) + reserveDistance;
            var capacity = Math.Ceiling(requiredDistance / sampleInterval) + 2d;
            if (capacity > int.MaxValue)
            {
                throw new OverflowException("The requested path capacity exceeds Int32.MaxValue.");
            }

            return Math.Max(2, (int)capacity);
        }

        public void Clear()
        {
            _oldestIndex = 0;
            Count = 0;
        }

        public void Append(Vector2 position, float cumulativeDistance, Vector2 forward)
        {
            if (!IsFinite(position) || !float.IsFinite(cumulativeDistance) || !IsFinite(forward))
            {
                throw new ArgumentException("Path samples must contain only finite values.");
            }

            if (Count > 0)
            {
                var newest = Newest;
                if (cumulativeDistance < newest.CumulativeDistance - DistanceEpsilon)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cumulativeDistance),
                        "Cumulative distance cannot move backwards.");
                }

                if (Mathf.Abs(cumulativeDistance - newest.CumulativeDistance) <= DistanceEpsilon)
                {
                    _samples[ToPhysicalIndex(Count - 1)] = new OSPathSample(
                        position,
                        cumulativeDistance,
                        NormalizeForward(forward, newest.Forward));
                    return;
                }
            }

            var fallbackForward = Count > 0 ? Newest.Forward : Vector2.right;
            var sample = new OSPathSample(
                position,
                cumulativeDistance,
                NormalizeForward(forward, fallbackForward));
            if (Count < Capacity)
            {
                _samples[ToPhysicalIndex(Count)] = sample;
                Count++;
                return;
            }

            _samples[_oldestIndex] = sample;
            _oldestIndex = (_oldestIndex + 1) % Capacity;
        }

        /// <summary>
        /// Drops obsolete history while retaining the nearest sample at or before the boundary.
        /// </summary>
        public void DiscardBefore(float minimumCumulativeDistance)
        {
            if (!float.IsFinite(minimumCumulativeDistance))
            {
                return;
            }

            while (Count > 2 && this[1].CumulativeDistance <= minimumCumulativeDistance)
            {
                _oldestIndex = (_oldestIndex + 1) % Capacity;
                Count--;
            }
        }

        /// <summary>
        /// Interpolates inside the stored path and extends a straight virtual path outside its ends.
        /// </summary>
        public bool TryEvaluate(float cumulativeDistance, out OSPathSample sample)
        {
            sample = default;
            if (Count == 0 || !float.IsFinite(cumulativeDistance))
            {
                return false;
            }

            var oldest = Oldest;
            if (cumulativeDistance <= oldest.CumulativeDistance)
            {
                var offset = cumulativeDistance - oldest.CumulativeDistance;
                sample = new OSPathSample(
                    oldest.Position + (oldest.Forward * offset),
                    cumulativeDistance,
                    oldest.Forward);
                return true;
            }

            var newest = Newest;
            if (cumulativeDistance >= newest.CumulativeDistance)
            {
                var offset = cumulativeDistance - newest.CumulativeDistance;
                sample = new OSPathSample(
                    newest.Position + (newest.Forward * offset),
                    cumulativeDistance,
                    newest.Forward);
                return true;
            }

            var low = 0;
            var high = Count - 1;
            while (high - low > 1)
            {
                var middle = low + ((high - low) / 2);
                if (this[middle].CumulativeDistance <= cumulativeDistance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            sample = Interpolate(this[low], this[high], cumulativeDistance);
            return true;
        }

        public static OSPathSample Interpolate(
            in OSPathSample older,
            in OSPathSample newer,
            float cumulativeDistance)
        {
            var span = newer.CumulativeDistance - older.CumulativeDistance;
            if (span <= DistanceEpsilon)
            {
                return new OSPathSample(newer.Position, cumulativeDistance, newer.Forward);
            }

            var t = Mathf.Clamp01((cumulativeDistance - older.CumulativeDistance) / span);
            var forward = Vector2.Lerp(older.Forward, newer.Forward, t);
            return new OSPathSample(
                Vector2.Lerp(older.Position, newer.Position, t),
                cumulativeDistance,
                NormalizeForward(forward, newer.Forward));
        }

        private int ToPhysicalIndex(int chronologicalIndex)
        {
            return (_oldestIndex + chronologicalIndex) % Capacity;
        }

        private static Vector2 NormalizeForward(Vector2 value, Vector2 fallback)
        {
            if (value.sqrMagnitude > DistanceEpsilon * DistanceEpsilon)
            {
                return value.normalized;
            }

            return fallback.sqrMagnitude > DistanceEpsilon * DistanceEpsilon
                ? fallback.normalized
                : Vector2.right;
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }
}
