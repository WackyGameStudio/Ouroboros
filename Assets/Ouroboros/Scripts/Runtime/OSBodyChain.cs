using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
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
        private int _nextStableId = 1;
        private bool _subscribed;
        private bool _started;

        public event Action<int, int, OSBodyRoleType> SegmentAppended;
        public event Action<int> SegmentCountChanged;
        public event Action ChainOrderChanged;

        public int ActiveCount { get; private set; }
        public int PoolCapacity => _poolViews.Length;
        public int PathCapacity => _path?.Capacity ?? 0;
        public int PathSampleCount => _path?.Count ?? 0;
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
            if (head == null || sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            SimulatePathStep(head.position);
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

            var newCount = ActiveCount - removeCount;
            for (var index = ActiveCount - 1; index >= newCount; index--)
            {
                _poolViews[index]?.Deactivate();
                _runtimeSlots[index]?.Deactivate();
            }

            ActiveCount = newCount;
            ApplySegmentPoses();
            SegmentCountChanged?.Invoke(ActiveCount);
            ChainOrderChanged?.Invoke();
            return OSRuleResult<int>.Accepted(ActiveCount, "body.remove_tail.accepted");
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
            float reserveDistance = DefaultReserveDistance)
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

        private int _testTechnicalGuard;
        private float _testSegmentSpacing;
        private float _testSampleInterval;
        private float _testReserveDistance;

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
            for (var index = 0; index < ActiveCount; index++)
            {
                _poolViews[index]?.Deactivate();
                _runtimeSlots[index]?.Deactivate();
            }

            var hadSegments = ActiveCount > 0;
            ActiveCount = 0;
            _nextStableId = 1;
            if (hadSegments)
            {
                SegmentCountChanged?.Invoke(0);
                ChainOrderChanged?.Invoke();
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

            _currentHeadPosition = head.position;
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
                _poolViews[chainIndex].SetPose(sample.Position, sample.Forward);
            }
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
