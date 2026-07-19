using System;
using System.Collections.Generic;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public enum OSBodyRemovalCause
    {
        Cut,
        Tail
    }

    public readonly struct OSBodyRemovalEvent
    {
        public OSBodyRemovalEvent(
            OSBodyRemovalCause cause,
            int startIndex,
            int previousCount,
            int remainingCount,
            Vector2 hitPosition,
            int[] removedStableIds)
        {
            Cause = cause;
            StartIndex = startIndex;
            PreviousCount = previousCount;
            RemainingCount = remainingCount;
            HitPosition = hitPosition;
            RemovedStableIds = removedStableIds ?? Array.Empty<int>();
        }

        public OSBodyRemovalCause Cause { get; }
        public int StartIndex { get; }
        public int PreviousCount { get; }
        public int RemainingCount { get; }
        public int RemovedCount => PreviousCount - RemainingCount;
        public Vector2 HitPosition { get; }
        public IReadOnlyList<int> RemovedStableIds { get; }
    }

    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class OSBodyChain : MonoBehaviour
    {
        private const int DefaultTechnicalGuard = 64;
        private const float DefaultSegmentSpacing = 0.55f;
        private const float DefaultSampleInterval = 0.12f;
        private const float DefaultReserveDistance = 4f;
        private const float PositionEpsilon = 0.000001f;

        [SerializeField] private Transform head;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private OSBodySegmentView segmentPrefab;
        [SerializeField] private Transform poolRoot;
        [SerializeField, Range(0, DefaultTechnicalGuard)] private int initialDebugSegmentCount = 20;

        private OSPathSampleRingBuffer _path;
        private OSBodySegmentView[] _poolViews = Array.Empty<OSBodySegmentView>();
        private OSBodySegmentRuntime[] _runtimeSlots = Array.Empty<OSBodySegmentRuntime>();
        private Vector2 _currentHeadPosition;
        private Vector2 _lastHeadPosition;
        private Vector2 _lastForward = Vector2.right;
        private float _headCumulativeDistance;
        private float _cutGuardRemaining;
        private int _nextStableId = 1;
        private bool _subscribed;
        private bool _started;
        private readonly Vector2[] _bodyConvergenceStarts = new Vector2[DefaultTechnicalGuard];
        private bool _bodyConvergenceActive;
        private float _bodyConvergenceDuration;
        private float _bodyRecoveryDuration;
        private float _bodyConvergenceElapsed;

        public event Action<int, int, OSBodyRoleType> SegmentAppended;
        public event Action<OSBodyRemovalEvent> SegmentsRemoving;
        public event Action<OSBodyRemovalEvent> SegmentsRemoved;
        public event Action<OSBodyRemovalEvent> SegmentsCut;
        public event Action<int> SegmentCountChanged;
        public event Action ChainOrderChanged;

        public int ActiveCount { get; private set; }
        public int PoolCapacity => _poolViews.Length;
        public int PathCapacity => _path?.Capacity ?? 0;
        public int PathSampleCount => _path?.Count ?? 0;
        public float CutGuardRemaining => _cutGuardRemaining;
        public bool IsBodyConvergenceActive => _bodyConvergenceActive;
        public float BodyConvergenceProgress => !_bodyConvergenceActive || _bodyConvergenceDuration <= 0f
            ? 0f
            : Mathf.Clamp01(_bodyConvergenceElapsed / _bodyConvergenceDuration);
        public float SegmentSpacing => bodyBalance != null
            ? bodyBalance.SegmentSpacing
            : DefaultSegmentSpacing;
        public int TechnicalGuard => bodyBalance != null
            ? bodyBalance.TechnicalGuard
            : DefaultTechnicalGuard;

        private void Awake()
        {
            ResolveReferences();
            BuildPool();
            InitializePath();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            _started = true;
            ResetDebugChain();
        }

        private void FixedUpdate()
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            SimulateCutGuard(Time.fixedDeltaTime);
            if (head == null)
            {
                return;
            }

            RecordHeadPosition(ResolveCurrentHeadPosition());
            SimulateBodyConvergence(Time.fixedDeltaTime);
            ApplySegmentPoses();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public OSBodySegmentRuntime GetActiveSegment(int chainIndex)
        {
            if ((uint)chainIndex >= (uint)ActiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(chainIndex));
            }

            return _runtimeSlots[chainIndex];
        }

        public OSRuleResult<int> AppendSegment(OSBodyRoleType role)
        {
            if (!Enum.IsDefined(typeof(OSBodyRoleType), role))
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.append.invalid_role",
                    ActiveCount);
            }

            if (_poolViews.Length == 0 || _runtimeSlots.Length == 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "body.append.pool_missing",
                    ActiveCount);
            }

            if (ActiveCount >= TechnicalGuard || ActiveCount >= _poolViews.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "body.append.capacity",
                    ActiveCount);
            }

            var chainIndex = ActiveCount;
            var stableId = _nextStableId++;
            var view = _poolViews[chainIndex];
            var runtime = _runtimeSlots[chainIndex];
            runtime.Activate(stableId, chainIndex, role, view);
            view.Configure(role, stableId, chainIndex);
            ActiveCount++;
            ApplySegmentPoses();

            SegmentAppended?.Invoke(stableId, chainIndex, role);
            SegmentCountChanged?.Invoke(ActiveCount);
            ChainOrderChanged?.Invoke();
            return OSRuleResult<int>.Accepted(stableId, "body.append.accepted");
        }

        public OSRuleResult<int> SetDebugSegmentCount(int segmentCount)
        {
            if (segmentCount < 0 || segmentCount > TechnicalGuard || segmentCount > _poolViews.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "body.debug_count.capacity",
                    ActiveCount);
            }

            ClearSegments();
            InitializePath();
            for (var index = 0; index < segmentCount; index++)
            {
                var role = (OSBodyRoleType)(index % 4);
                var appendResult = AppendSegment(role);
                if (!appendResult.IsAccepted)
                {
                    return OSRuleResult<int>.Rejected(
                        appendResult.Code,
                        appendResult.ReasonKey,
                        ActiveCount);
                }
            }

            ApplySegmentPoses();
            return OSRuleResult<int>.Accepted(ActiveCount, "body.debug_count.accepted");
        }

        public void BeginBodyConvergence(float duration, float recoveryDuration)
        {
            _bodyConvergenceDuration = Mathf.Max(OSBodyDashMath.MinimumDuration, duration);
            _bodyRecoveryDuration = Mathf.Max(0f, recoveryDuration);
            _bodyConvergenceElapsed = 0f;
            _bodyConvergenceActive = true;
            for (var index = 0; index < ActiveCount && index < _bodyConvergenceStarts.Length; index++)
            {
                _bodyConvergenceStarts[index] = _poolViews[index] != null
                    ? (Vector2)_poolViews[index].transform.position
                    : _currentHeadPosition;
            }
        }

        public void CancelBodyConvergence()
        {
            _bodyConvergenceActive = false;
            _bodyConvergenceDuration = 0f;
            _bodyRecoveryDuration = 0f;
            _bodyConvergenceElapsed = 0f;
            ApplySegmentPoses();
        }

        /// <summary>
        /// Removes a requested number of tail segments in reverse order and reports the new count.
        /// </summary>
        public OSRuleResult<int> RemoveTailSegments(int removeCount)
        {
            if (removeCount <= 0 || removeCount > ActiveCount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.remove_tail.invalid_count",
                    ActiveCount);
            }

            RemoveFrom(ActiveCount - removeCount, OSBodyRemovalCause.Tail, Vector2.zero);
            return OSRuleResult<int>.Accepted(ActiveCount, "body.remove_tail.accepted");
        }

        /// <summary>
        /// Cuts the hit segment and every segment behind it. Removed stable IDs are published
        /// in their original head-to-tail order while pool deactivation runs tail-first.
        /// </summary>
        public OSRuleResult<int> TryCutFrom(int chainIndex, Vector2 hitPosition)
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "body.cut.invalid_state",
                    ActiveCount);
            }

            if ((uint)chainIndex >= (uint)ActiveCount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.cut.invalid_index",
                    ActiveCount);
            }

            if (_cutGuardRemaining > 0f)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCutGuard,
                    "body.cut.guard_active",
                    ActiveCount);
            }

            var removal = RemoveFrom(chainIndex, OSBodyRemovalCause.Cut, hitPosition);
            _cutGuardRemaining = EffectiveCutGuardDuration;
            SegmentsCut?.Invoke(removal);
            return OSRuleResult<int>.Accepted(removal.RemovedCount, "body.cut.accepted");
        }

        public int FindChainIndexByStableId(int stableId)
        {
            if (stableId <= 0)
            {
                return -1;
            }

            for (var index = 0; index < ActiveCount; index++)
            {
                if (_runtimeSlots[index].StableId == stableId)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Counts active segments assigned to one visual role.
        /// </summary>
        public int GetRoleCount(OSBodyRoleType role)
        {
            var count = 0;
            for (var index = 0; index < ActiveCount; index++)
            {
                if (_runtimeSlots[index].Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        [ContextMenu("Debug/Set 2 Segments")]
        private void DebugSetTwoSegments()
        {
            SetDebugSegmentCount(2);
        }

        [ContextMenu("Debug/Set 20 Segments")]
        private void DebugSetTwentySegments()
        {
            SetDebugSegmentCount(20);
        }

        [ContextMenu("Debug/Set 40 Segments")]
        private void DebugSetFortySegments()
        {
            SetDebugSegmentCount(40);
        }

        [ContextMenu("Debug/Set 64 Segments")]
        private void DebugSetSixtyFourSegments()
        {
            SetDebugSegmentCount(64);
        }

        internal void ConfigureForTesting(
            Transform headTransform,
            OSBodySegmentView prefab,
            Transform targetPoolRoot,
            int maximumSegments = DefaultTechnicalGuard,
            float spacing = DefaultSegmentSpacing,
            float sampleInterval = DefaultSampleInterval,
            float reserveDistance = DefaultReserveDistance,
            float cutGuardDuration = 0.35f)
        {
            head = headTransform;
            playerController = null;
            sessionController = null;
            bodyBalance = null;
            segmentPrefab = prefab;
            poolRoot = targetPoolRoot;
            initialDebugSegmentCount = 0;
            _testTechnicalGuard = maximumSegments;
            _testSegmentSpacing = spacing;
            _testSampleInterval = sampleInterval;
            _testReserveDistance = reserveDistance;
            _testCutGuardDuration = Mathf.Max(0f, cutGuardDuration);
            DestroyPool();
            BuildPool();
            InitializePath();
        }

        internal void SimulatePathStep(Vector2 nextHeadPosition)
        {
            if (_path == null)
            {
                InitializePath();
            }

            if (_path == null || !IsFinite(nextHeadPosition))
            {
                return;
            }

            RecordHeadPosition(nextHeadPosition);
            ApplySegmentPoses();
        }

        internal void SimulateBodyConvergenceForTesting(float deltaTime)
        {
            if (head != null)
            {
                RecordHeadPosition(ResolveCurrentHeadPosition());
            }

            SimulateBodyConvergence(deltaTime);
            ApplySegmentPoses();
        }

        internal void SimulateCutGuardForTesting(float deltaTime)
        {
            SimulateCutGuard(deltaTime);
        }

        private int _testTechnicalGuard;
        private float _testSegmentSpacing;
        private float _testSampleInterval;
        private float _testReserveDistance;
        private float _testCutGuardDuration = -1f;

        private int EffectiveTechnicalGuard => _testTechnicalGuard > 0
            ? _testTechnicalGuard
            : TechnicalGuard;
        private float EffectiveSegmentSpacing => _testSegmentSpacing > 0f
            ? _testSegmentSpacing
            : SegmentSpacing;
        private float EffectiveSampleInterval => _testSampleInterval > 0f
            ? _testSampleInterval
            : bodyBalance != null ? bodyBalance.PathSampleInterval : DefaultSampleInterval;
        private float EffectiveReserveDistance => _testReserveDistance >= 0f && _testTechnicalGuard > 0
            ? _testReserveDistance
            : bodyBalance != null ? bodyBalance.PathReserveDistance : DefaultReserveDistance;
        private float EffectiveCutGuardDuration => _testCutGuardDuration >= 0f
            ? _testCutGuardDuration
            : bodyBalance != null ? bodyBalance.CutGuardDuration : 0.35f;

        private void ResetDebugChain()
        {
            if (_poolViews.Length == 0)
            {
                BuildPool();
            }

            var requestedCount = Mathf.Clamp(initialDebugSegmentCount, 0, EffectiveTechnicalGuard);
            SetDebugSegmentCount(Mathf.Min(requestedCount, _poolViews.Length));
        }

        private void ClearSegments()
        {
            CancelBodyConvergence();
            for (var index = 0; index < ActiveCount; index++)
            {
                _poolViews[index]?.Deactivate();
                _runtimeSlots[index]?.Deactivate();
            }

            var hadSegments = ActiveCount > 0;
            ActiveCount = 0;
            _cutGuardRemaining = 0f;
            _nextStableId = 1;
            if (hadSegments)
            {
                SegmentCountChanged?.Invoke(0);
                ChainOrderChanged?.Invoke();
            }
        }

        private OSBodyRemovalEvent RemoveFrom(
            int startIndex,
            OSBodyRemovalCause cause,
            Vector2 hitPosition)
        {
            var previousCount = ActiveCount;
            var removedStableIds = new int[previousCount - startIndex];
            for (var index = startIndex; index < previousCount; index++)
            {
                removedStableIds[index - startIndex] = _runtimeSlots[index].StableId;
            }

            var removal = new OSBodyRemovalEvent(
                cause,
                startIndex,
                previousCount,
                startIndex,
                hitPosition,
                removedStableIds);
            SegmentsRemoving?.Invoke(removal);

            for (var index = previousCount - 1; index >= startIndex; index--)
            {
                _poolViews[index]?.Deactivate();
                _runtimeSlots[index]?.Deactivate();
            }

            ActiveCount = startIndex;
            ApplySegmentPoses();
            SegmentCountChanged?.Invoke(ActiveCount);
            ChainOrderChanged?.Invoke();
            SegmentsRemoved?.Invoke(removal);
            return removal;
        }

        private void SimulateCutGuard(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f || _cutGuardRemaining <= 0f)
            {
                return;
            }

            _cutGuardRemaining = Mathf.Max(0f, _cutGuardRemaining - deltaTime);
        }

        private void SimulateBodyConvergence(float deltaTime)
        {
            if (!_bodyConvergenceActive || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            _bodyConvergenceElapsed += deltaTime;
            if (_bodyConvergenceElapsed >= _bodyConvergenceDuration + _bodyRecoveryDuration)
            {
                _bodyConvergenceActive = false;
            }
        }

        private void BuildPool()
        {
            if (segmentPrefab == null)
            {
                return;
            }

            if (poolRoot == null)
            {
                var root = new GameObject("SegmentPool");
                root.transform.SetParent(transform, false);
                poolRoot = root.transform;
            }

            var capacity = Mathf.Max(1, EffectiveTechnicalGuard);
            if (_poolViews.Length == capacity)
            {
                return;
            }

            DestroyPool();
            _poolViews = new OSBodySegmentView[capacity];
            _runtimeSlots = new OSBodySegmentRuntime[capacity];
            for (var index = 0; index < capacity; index++)
            {
                var view = Instantiate(segmentPrefab, poolRoot);
                view.name = $"BodySegment_{index + 1:00}";
                view.Deactivate();
                _poolViews[index] = view;
                _runtimeSlots[index] = new OSBodySegmentRuntime();
            }
        }

        private void DestroyPool()
        {
            ClearSegments();
            for (var index = 0; index < _poolViews.Length; index++)
            {
                var view = _poolViews[index];
                if (view == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            _poolViews = Array.Empty<OSBodySegmentView>();
            _runtimeSlots = Array.Empty<OSBodySegmentRuntime>();
        }

        private void InitializePath()
        {
            ResolveReferences();
            if (head == null)
            {
                return;
            }

            var capacity = OSPathSampleRingBuffer.CalculateRequiredCapacity(
                Mathf.Max(1, EffectiveTechnicalGuard),
                Mathf.Max(PositionEpsilon, EffectiveSegmentSpacing),
                Mathf.Max(PositionEpsilon, EffectiveSampleInterval),
                Mathf.Max(0f, EffectiveReserveDistance));
            if (_path == null || _path.Capacity != capacity)
            {
                _path = new OSPathSampleRingBuffer(capacity);
            }
            else
            {
                _path.Clear();
            }

            _currentHeadPosition = ResolveCurrentHeadPosition();
            _lastHeadPosition = _currentHeadPosition;
            _lastForward = playerController != null && playerController.LastDirection.sqrMagnitude > PositionEpsilon
                ? playerController.LastDirection.normalized
                : Vector2.right;
            _headCumulativeDistance = 0f;
            _path.Append(_currentHeadPosition, 0f, _lastForward);
            ApplySegmentPoses();
        }

        private void RecordHeadPosition(Vector2 nextHeadPosition)
        {
            var delta = nextHeadPosition - _lastHeadPosition;
            var distance = delta.magnitude;
            _currentHeadPosition = nextHeadPosition;
            if (distance <= PositionEpsilon)
            {
                return;
            }

            var forward = delta / distance;
            var previousDistance = _headCumulativeDistance;
            var nextDistance = previousDistance + distance;
            var sampleDistance = _path.Newest.CumulativeDistance + EffectiveSampleInterval;
            while (sampleDistance <= nextDistance + PositionEpsilon)
            {
                var t = Mathf.Clamp01((sampleDistance - previousDistance) / distance);
                _path.Append(
                    Vector2.Lerp(_lastHeadPosition, nextHeadPosition, t),
                    sampleDistance,
                    forward);
                sampleDistance += EffectiveSampleInterval;
            }

            _lastHeadPosition = nextHeadPosition;
            _lastForward = forward;
            _headCumulativeDistance = nextDistance;
            var oldestRequiredDistance = _headCumulativeDistance -
                                         ((EffectiveTechnicalGuard * EffectiveSegmentSpacing) +
                                          EffectiveReserveDistance);
            _path.DiscardBefore(oldestRequiredDistance);
        }

        private void ApplySegmentPoses()
        {
            if (_path == null || _path.Count == 0)
            {
                return;
            }

            var newerIndex = _path.Count - 1;
            for (var chainIndex = 0; chainIndex < ActiveCount; chainIndex++)
            {
                var targetDistance = _headCumulativeDistance -
                                     ((chainIndex + 1) * EffectiveSegmentSpacing);
                var sample = EvaluateTarget(targetDistance, ref newerIndex);
                var visualPosition = ResolveBodyConvergencePosition(chainIndex, sample.Position);
                _poolViews[chainIndex].SetPose(visualPosition, sample.Forward);
            }
        }

        private Vector2 ResolveBodyConvergencePosition(int chainIndex, Vector2 normalPosition)
        {
            if (!_bodyConvergenceActive || (uint)chainIndex >= (uint)_bodyConvergenceStarts.Length)
            {
                return normalPosition;
            }

            if (_bodyConvergenceElapsed <= _bodyConvergenceDuration)
            {
                var progress = OSBodyDashMath.EaseOutCubic(
                    _bodyConvergenceElapsed / Mathf.Max(OSBodyDashMath.MinimumDuration, _bodyConvergenceDuration));
                return Vector2.Lerp(_bodyConvergenceStarts[chainIndex], _currentHeadPosition, progress);
            }

            if (_bodyRecoveryDuration <= 0f)
            {
                return normalPosition;
            }

            var recoveryProgress = OSBodyDashMath.EaseOutCubic(
                (_bodyConvergenceElapsed - _bodyConvergenceDuration) / _bodyRecoveryDuration);
            return Vector2.Lerp(_currentHeadPosition, normalPosition, recoveryProgress);
        }

        private OSPathSample EvaluateTarget(float targetDistance, ref int newerIndex)
        {
            var oldest = _path.Oldest;
            if (targetDistance <= oldest.CumulativeDistance)
            {
                var offset = targetDistance - oldest.CumulativeDistance;
                return new OSPathSample(
                    oldest.Position + (oldest.Forward * offset),
                    targetDistance,
                    oldest.Forward);
            }

            var newest = _path.Newest;
            if (targetDistance >= newest.CumulativeDistance)
            {
                var span = _headCumulativeDistance - newest.CumulativeDistance;
                if (span <= PositionEpsilon)
                {
                    return newest;
                }

                var t = Mathf.Clamp01((targetDistance - newest.CumulativeDistance) / span);
                return new OSPathSample(
                    Vector2.Lerp(newest.Position, _currentHeadPosition, t),
                    targetDistance,
                    _lastForward);
            }

            newerIndex = Mathf.Clamp(newerIndex, 1, _path.Count - 1);
            while (newerIndex > 1 && _path[newerIndex - 1].CumulativeDistance > targetDistance)
            {
                newerIndex--;
            }

            return OSPathSampleRingBuffer.Interpolate(
                _path[newerIndex - 1],
                _path[newerIndex],
                targetDistance);
        }

        private void ResolveReferences()
        {
            if (head == null)
            {
                head = transform.parent != null ? transform.parent.Find("Head") : null;
            }

            playerController ??= head != null ? head.GetComponent<OSPlayerController>() : null;
        }

        private Vector2 ResolveCurrentHeadPosition()
        {
            return playerController != null
                ? playerController.Position
                : head != null ? (Vector2)head.position : _currentHeadPosition;
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.PositionReset += HandlePlayerPositionReset;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleSessionStateChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.PositionReset -= HandlePlayerPositionReset;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandlePlayerPositionReset()
        {
            InitializePath();
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot && _started)
            {
                ResetDebugChain();
            }
            else if (current is OSSessionState.Dead or OSSessionState.Cleared or OSSessionState.Result)
            {
                CancelBodyConvergence();
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        private void OnValidate()
        {
            initialDebugSegmentCount = Mathf.Clamp(initialDebugSegmentCount, 0, DefaultTechnicalGuard);
        }
    }
}
