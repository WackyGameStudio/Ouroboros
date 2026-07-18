using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSShieldChargeEvent
    {
        public OSShieldChargeEvent(int stableId, int chainIndex, int charges, Vector2 position)
        {
            StableId = stableId;
            ChainIndex = chainIndex;
            Charges = charges;
            Position = position;
        }

        public int StableId { get; }
        public int ChainIndex { get; }
        public int Charges { get; }
        public Vector2 Position { get; }
    }

    [DefaultExecutionOrder(7800)]
    [DisallowMultipleComponent]
    public sealed class OSShieldBodyRole : MonoBehaviour
    {
        private const int Capacity = 64;
        private const float TieEpsilon = 0.000001f;
        private const float DefaultRadius = 1.5f;
        private const int DefaultCharges = 1;
        private const float DefaultRecharge = 6f;
        private const int CircleSegments = 40;

        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private LineRenderer[] rangeViews = new LineRenderer[Capacity];

        private int[] _stableIds = new int[Capacity];
        private int[] _charges = new int[Capacity];
        private float[] _rechargeRemaining = new float[Capacity];
        private int[] _nextStableIds = new int[Capacity];
        private int[] _nextCharges = new int[Capacity];
        private float[] _nextRechargeRemaining = new float[Capacity];
        private int _stateCount;
        private bool _subscribed;

        private float _testRadius = -1f;
        private int _testCharges;
        private float _testRecharge = -1f;

        public event Action<OSShieldChargeEvent> ShieldConsumed;
        public event Action<OSShieldChargeEvent> ShieldRecharged;

        public int ActiveSegmentCount => _stateCount;
        public int ChargedCount { get; private set; }
        public int LastConsumedStableId { get; private set; }
        public float Radius => EffectiveRadius;
        public float RechargeDuration => EffectiveRecharge;

        private void Awake()
        {
            ConfigureViewGeometry();
        }

        private void OnEnable()
        {
            Subscribe();
            SynchronizeStates();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideAllViews();
        }

        public OSRuleResult<int> TryBlockDamage(OSDamageEvent damageEvent)
        {
            if (damageEvent.TargetKind is not OSTargetKind.PlayerHead and
                not OSTargetKind.PlayerBody ||
                !float.IsFinite(damageEvent.Damage) || damageEvent.Damage <= 0f ||
                sessionController != null && !sessionController.IsSimulationRunning)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "shield.block.invalid_damage");
            }

            var selectedIndex = -1;
            var selectedDistance = float.PositiveInfinity;
            var selectedChainIndex = int.MaxValue;
            var radiusSquared = EffectiveRadius * EffectiveRadius;
            for (var index = 0; index < _stateCount; index++)
            {
                if (_charges[index] <= 0)
                {
                    continue;
                }

                var segment = roleRegistry?.FindByStableId(OSBodyRoleType.Shield, _stableIds[index]);
                if (segment?.View == null)
                {
                    continue;
                }

                var distanceSquared = ((Vector2)segment.View.transform.position - damageEvent.HitPosition)
                    .sqrMagnitude;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                if (distanceSquared < selectedDistance - TieEpsilon ||
                    Mathf.Abs(distanceSquared - selectedDistance) <= TieEpsilon &&
                    segment.ChainIndex < selectedChainIndex)
                {
                    selectedIndex = index;
                    selectedDistance = distanceSquared;
                    selectedChainIndex = segment.ChainIndex;
                }
            }

            if (selectedIndex < 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRange,
                    "shield.block.no_charged_shield");
            }

            _charges[selectedIndex]--;
            if (_charges[selectedIndex] <= 0)
            {
                _charges[selectedIndex] = 0;
                _rechargeRemaining[selectedIndex] = EffectiveRecharge;
                ChargedCount = Mathf.Max(0, ChargedCount - 1);
            }

            LastConsumedStableId = _stableIds[selectedIndex];
            var selected = roleRegistry.FindByStableId(
                OSBodyRoleType.Shield,
                _stableIds[selectedIndex]);
            var shieldEvent = new OSShieldChargeEvent(
                _stableIds[selectedIndex],
                selected?.ChainIndex ?? selectedChainIndex,
                _charges[selectedIndex],
                selected?.View != null
                    ? (Vector2)selected.View.transform.position
                    : damageEvent.HitPosition);
            ShieldConsumed?.Invoke(shieldEvent);
            RefreshView(selectedIndex);
            return OSRuleResult<int>.Accepted(
                _stableIds[selectedIndex],
                "shield.block.accepted");
        }

        internal void ConfigureForTesting(
            OSBodyRoleRegistry roles,
            OSGameSessionController session,
            LineRenderer[] views = null,
            float radius = DefaultRadius,
            int charges = DefaultCharges,
            float recharge = DefaultRecharge)
        {
            Unsubscribe();
            roleRegistry = roles;
            sessionController = session;
            bodyBalance = null;
            rangeViews = views ?? Array.Empty<LineRenderer>();
            _testRadius = Mathf.Max(0.01f, radius);
            _testCharges = Mathf.Max(1, charges);
            _testRecharge = Mathf.Max(0.01f, recharge);
            ResetState();
            ConfigureViewGeometry();
            Subscribe();
            SynchronizeStates();
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f ||
                sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            for (var index = 0; index < _stateCount; index++)
            {
                if (_charges[index] <= 0 && _rechargeRemaining[index] > 0f)
                {
                    _rechargeRemaining[index] = Mathf.Max(
                        0f,
                        _rechargeRemaining[index] - deltaTime);
                    if (_rechargeRemaining[index] <= 0f)
                    {
                        _charges[index] = EffectiveCharges;
                        ChargedCount++;
                        var segment = roleRegistry?.FindByStableId(
                            OSBodyRoleType.Shield,
                            _stableIds[index]);
                        ShieldRecharged?.Invoke(new OSShieldChargeEvent(
                            _stableIds[index],
                            segment?.ChainIndex ?? -1,
                            _charges[index],
                            segment?.View != null
                                ? (Vector2)segment.View.transform.position
                                : Vector2.zero));
                    }
                }

                RefreshView(index);
            }
        }

        internal int GetChargesForTesting(int stableId)
        {
            var index = FindStateIndex(stableId);
            return index >= 0 ? _charges[index] : -1;
        }

        internal float GetRechargeForTesting(int stableId)
        {
            var index = FindStateIndex(stableId);
            return index >= 0 ? _rechargeRemaining[index] : -1f;
        }

        private float EffectiveRadius => _testRadius >= 0f
            ? _testRadius
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Shield)?.Radius ?? DefaultRadius;
        private int EffectiveCharges => _testCharges > 0
            ? _testCharges
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Shield)?.Charges ?? DefaultCharges;
        private float EffectiveRecharge => _testRecharge >= 0f
            ? _testRecharge
            : bodyBalance?.GetRoleDefinition(OSBodyRoleType.Shield)?.RechargeDuration ?? DefaultRecharge;

        private void SynchronizeStates()
        {
            Array.Clear(_nextStableIds, 0, Capacity);
            Array.Clear(_nextCharges, 0, Capacity);
            Array.Clear(_nextRechargeRemaining, 0, Capacity);

            var nextCount = Mathf.Min(roleRegistry?.GetCount(OSBodyRoleType.Shield) ?? 0, Capacity);
            for (var index = 0; index < nextCount; index++)
            {
                var segment = roleRegistry.GetSegment(OSBodyRoleType.Shield, index);
                if (segment == null)
                {
                    continue;
                }

                _nextStableIds[index] = segment.StableId;
                var previousIndex = FindStateIndex(segment.StableId);
                if (previousIndex >= 0)
                {
                    _nextCharges[index] = _charges[previousIndex];
                    _nextRechargeRemaining[index] = _rechargeRemaining[previousIndex];
                }
                else
                {
                    _nextCharges[index] = EffectiveCharges;
                }
            }

            Swap(ref _stableIds, ref _nextStableIds);
            Swap(ref _charges, ref _nextCharges);
            Swap(ref _rechargeRemaining, ref _nextRechargeRemaining);
            _stateCount = nextCount;
            RecountCharged();
            RefreshAllViews();
        }

        private void RecountCharged()
        {
            ChargedCount = 0;
            for (var index = 0; index < _stateCount; index++)
            {
                if (_charges[index] > 0)
                {
                    ChargedCount++;
                }
            }
        }

        private int FindStateIndex(int stableId)
        {
            for (var index = 0; index < _stateCount; index++)
            {
                if (_stableIds[index] == stableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ConfigureViewGeometry()
        {
            if (rangeViews == null)
            {
                return;
            }

            for (var viewIndex = 0; viewIndex < rangeViews.Length; viewIndex++)
            {
                var view = rangeViews[viewIndex];
                if (view == null)
                {
                    continue;
                }

                view.useWorldSpace = false;
                view.loop = true;
                view.positionCount = CircleSegments;
                view.startWidth = 0.045f;
                view.endWidth = 0.045f;
                for (var pointIndex = 0; pointIndex < CircleSegments; pointIndex++)
                {
                    var angle = pointIndex * Mathf.PI * 2f / CircleSegments;
                    view.SetPosition(
                        pointIndex,
                        new Vector3(
                            Mathf.Cos(angle) * EffectiveRadius,
                            Mathf.Sin(angle) * EffectiveRadius,
                            0f));
                }
            }
        }

        private void RefreshView(int index)
        {
            if (rangeViews == null || index < 0 || index >= rangeViews.Length ||
                rangeViews[index] == null)
            {
                return;
            }

            var view = rangeViews[index];
            var segment = roleRegistry?.FindByStableId(OSBodyRoleType.Shield, _stableIds[index]);
            var visible = segment?.View != null;
            view.enabled = visible;
            if (!visible)
            {
                return;
            }

            view.transform.position = segment.View.transform.position;
            var charged = _charges[index] > 0;
            var color = charged
                ? new Color32(92, 207, 255, 210)
                : new Color32(92, 207, 255, 55);
            view.startColor = color;
            view.endColor = color;
        }

        private void RefreshAllViews()
        {
            HideAllViews();
            for (var index = 0; index < _stateCount; index++)
            {
                RefreshView(index);
            }
        }

        private void HideAllViews()
        {
            if (rangeViews == null)
            {
                return;
            }

            for (var index = 0; index < rangeViews.Length; index++)
            {
                if (rangeViews[index] != null)
                {
                    rangeViews[index].enabled = false;
                }
            }
        }

        private void ResetState()
        {
            Array.Clear(_stableIds, 0, Capacity);
            Array.Clear(_charges, 0, Capacity);
            Array.Clear(_rechargeRemaining, 0, Capacity);
            _stateCount = 0;
            ChargedCount = 0;
            LastConsumedStableId = 0;
            HideAllViews();
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged += SynchronizeStates;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleStateChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged -= SynchronizeStates;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleStateChanged;
            }

            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                ResetState();
                SynchronizeStates();
            }
        }

        private static void Swap<T>(ref T[] left, ref T[] right)
        {
            (left, right) = (right, left);
        }
    }
}
