using System;

namespace Ouroboros.Core
{
    /// <summary>
    /// Collects one physics tick of hostile damage candidates and normalizes duplicates
    /// before runtime state is mutated.
    /// </summary>
    public sealed class OSCombatEventBuffer
    {
        public const int DefaultCapacity = 1024;

        private readonly OSDamageEvent[] _damageEvents;
        private int _count;
        private int _combatTick;
        private bool _tickOpen;

        public OSCombatEventBuffer(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _damageEvents = new OSDamageEvent[capacity];
        }

        public int Count => _count;
        public int Capacity => _damageEvents.Length;
        public int CombatTick => _combatTick;

        public OSRuleResult<int> BeginTick(int combatTick)
        {
            if (combatTick < 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "combat_buffer.begin.invalid_tick",
                    _count);
            }

            if (_count > 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "combat_buffer.begin.pending_events",
                    _count);
            }

            _combatTick = combatTick;
            _tickOpen = true;
            return OSRuleResult<int>.Accepted(combatTick, "combat_buffer.begin.accepted");
        }

        public OSRuleResult<int> EnqueueDamage(OSDamageEvent damageEvent)
        {
            if (!_tickOpen)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "combat_buffer.enqueue.tick_closed",
                    _count);
            }

            if (damageEvent.AttackEventId <= 0 || damageEvent.SourceRuntimeId <= 0 ||
                damageEvent.TargetRuntimeId <= 0 || !float.IsFinite(damageEvent.Damage) ||
                damageEvent.Damage <= 0f ||
                damageEvent.TargetKind is not OSTargetKind.PlayerHead and not OSTargetKind.PlayerBody)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "combat_buffer.enqueue.invalid_damage",
                    _count);
            }

            var incomingIsHead = damageEvent.TargetKind == OSTargetKind.PlayerHead;
            var existingHeadForAttack = false;
            for (var index = 0; index < _count; index++)
            {
                var existing = _damageEvents[index];
                if (existing.AttackEventId != damageEvent.AttackEventId)
                {
                    continue;
                }

                if (existing.TargetKind == OSTargetKind.PlayerHead)
                {
                    existingHeadForAttack = true;
                    break;
                }

                if (!incomingIsHead && existing.TargetKind == damageEvent.TargetKind &&
                    existing.TargetRuntimeId == damageEvent.TargetRuntimeId)
                {
                    return OSRuleResult<int>.Rejected(
                        OSResultCode.Duplicate,
                        "combat_buffer.enqueue.duplicate_target",
                        _count);
                }
            }

            // A single hostile attack touching both the head and body becomes one head candidate.
            if (existingHeadForAttack)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.Duplicate,
                    incomingIsHead
                        ? "combat_buffer.enqueue.duplicate_head"
                        : "combat_buffer.enqueue.body_suppressed_by_head",
                    _count);
            }

            if (incomingIsHead)
            {
                RemoveBodyCandidatesForAttack(damageEvent.AttackEventId);
            }

            if (_count >= _damageEvents.Length)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "combat_buffer.enqueue.capacity",
                    _count);
            }

            _damageEvents[_count++] = new OSDamageEvent(
                damageEvent.AttackEventId,
                _combatTick,
                damageEvent.SourceRuntimeId,
                damageEvent.TargetRuntimeId,
                damageEvent.TargetKind,
                damageEvent.Damage,
                damageEvent.HitPosition,
                damageEvent.ControlDuration);
            return OSRuleResult<int>.Accepted(_count, "combat_buffer.enqueue.accepted");
        }

        /// <summary>
        /// Copies the normalized tick into caller-owned storage in deterministic attack order.
        /// </summary>
        public int DrainTo(OSDamageEvent[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length < _count)
            {
                throw new ArgumentException("Destination is smaller than the pending event count.", nameof(destination));
            }

            SortDeterministically();
            var drained = _count;
            Array.Copy(_damageEvents, destination, drained);
            Array.Clear(_damageEvents, 0, drained);
            _count = 0;
            _tickOpen = false;
            return drained;
        }

        public void Clear()
        {
            Array.Clear(_damageEvents, 0, _count);
            _count = 0;
            _tickOpen = false;
        }

        private void RemoveBodyCandidatesForAttack(int attackEventId)
        {
            for (var index = _count - 1; index >= 0; index--)
            {
                if (_damageEvents[index].AttackEventId != attackEventId ||
                    _damageEvents[index].TargetKind != OSTargetKind.PlayerBody)
                {
                    continue;
                }

                var last = --_count;
                if (index != last)
                {
                    _damageEvents[index] = _damageEvents[last];
                }

                _damageEvents[last] = default;
            }
        }

        private void SortDeterministically()
        {
            for (var index = 1; index < _count; index++)
            {
                var value = _damageEvents[index];
                var insertion = index - 1;
                while (insertion >= 0 && Compare(value, _damageEvents[insertion]) < 0)
                {
                    _damageEvents[insertion + 1] = _damageEvents[insertion];
                    insertion--;
                }

                _damageEvents[insertion + 1] = value;
            }
        }

        private static int Compare(OSDamageEvent left, OSDamageEvent right)
        {
            var attackComparison = left.AttackEventId.CompareTo(right.AttackEventId);
            if (attackComparison != 0)
            {
                return attackComparison;
            }

            var leftPriority = left.TargetKind == OSTargetKind.PlayerHead ? 0 : 1;
            var rightPriority = right.TargetKind == OSTargetKind.PlayerHead ? 0 : 1;
            var targetComparison = leftPriority.CompareTo(rightPriority);
            return targetComparison != 0
                ? targetComparison
                : left.TargetRuntimeId.CompareTo(right.TargetRuntimeId);
        }
    }
}
