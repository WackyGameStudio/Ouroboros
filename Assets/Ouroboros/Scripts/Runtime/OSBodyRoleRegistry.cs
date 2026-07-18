using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-320)]
    [DisallowMultipleComponent]
    public sealed class OSBodyRoleRegistry : MonoBehaviour
    {
        private const int RoleCount = 4;
        private const int DefaultCapacity = 64;

        [SerializeField] private OSBodyChain bodyChain;

        private OSBodySegmentRuntime[][] _segmentsByRole;
        private int[] _counts;
        private bool _subscribed;

        public event Action RoleListsChanged;

        public OSBodyChain BodyChain => bodyChain;

        private void Awake()
        {
            EnsureStorage();
            Rebuild();
        }

        private void OnEnable()
        {
            EnsureStorage();
            Subscribe();
            Rebuild();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public int GetCount(OSBodyRoleType role)
        {
            EnsureStorage();
            var roleIndex = (int)role;
            return (uint)roleIndex < RoleCount ? _counts[roleIndex] : 0;
        }

        public OSBodySegmentRuntime GetSegment(OSBodyRoleType role, int index)
        {
            EnsureStorage();
            var roleIndex = (int)role;
            if ((uint)roleIndex >= RoleCount || index < 0 || index >= _counts[roleIndex])
            {
                return null;
            }

            return _segmentsByRole[roleIndex][index];
        }

        public OSBodySegmentRuntime FindByStableId(OSBodyRoleType role, int stableId)
        {
            EnsureStorage();
            var roleIndex = (int)role;
            if ((uint)roleIndex >= RoleCount || stableId <= 0)
            {
                return null;
            }

            for (var index = 0; index < _counts[roleIndex]; index++)
            {
                var segment = _segmentsByRole[roleIndex][index];
                if (segment != null && segment.IsActive && segment.StableId == stableId)
                {
                    return segment;
                }
            }

            return null;
        }

        internal void ConfigureForTesting(OSBodyChain chain)
        {
            Unsubscribe();
            bodyChain = chain;
            EnsureStorage();
            Subscribe();
            Rebuild();
        }

        internal void RebuildForTesting()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureStorage();
            for (var roleIndex = 0; roleIndex < RoleCount; roleIndex++)
            {
                Array.Clear(_segmentsByRole[roleIndex], 0, _segmentsByRole[roleIndex].Length);
                _counts[roleIndex] = 0;
            }

            if (bodyChain != null)
            {
                for (var chainIndex = 0; chainIndex < bodyChain.ActiveCount; chainIndex++)
                {
                    var segment = bodyChain.GetActiveSegment(chainIndex);
                    var roleIndex = segment != null ? (int)segment.Role : -1;
                    if ((uint)roleIndex >= RoleCount || _counts[roleIndex] >= DefaultCapacity)
                    {
                        continue;
                    }

                    _segmentsByRole[roleIndex][_counts[roleIndex]++] = segment;
                }
            }

            RoleListsChanged?.Invoke();
        }

        private void EnsureStorage()
        {
            if (_segmentsByRole != null && _counts != null)
            {
                return;
            }

            _segmentsByRole = new OSBodySegmentRuntime[RoleCount][];
            for (var roleIndex = 0; roleIndex < RoleCount; roleIndex++)
            {
                _segmentsByRole[roleIndex] = new OSBodySegmentRuntime[DefaultCapacity];
            }

            _counts = new int[RoleCount];
        }

        private void Subscribe()
        {
            if (_subscribed || bodyChain == null || !isActiveAndEnabled)
            {
                return;
            }

            bodyChain.SegmentAppended += HandleSegmentAppended;
            bodyChain.SegmentsRemoved += HandleSegmentsRemoved;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentAppended -= HandleSegmentAppended;
                bodyChain.SegmentsRemoved -= HandleSegmentsRemoved;
            }

            _subscribed = false;
        }

        private void HandleSegmentAppended(int stableId, int chainIndex, OSBodyRoleType role)
        {
            Rebuild();
        }

        private void HandleSegmentsRemoved(OSBodyRemovalEvent removal)
        {
            Rebuild();
        }
    }
}
