using System.Collections.Generic;

namespace Ouroboros.Core
{
    /// <summary>
    /// Serializes selection requests while preserving FIFO order within Body and LevelUp priorities.
    /// </summary>
    public sealed class OSSelectionQueue
    {
        private readonly Queue<OSSelectionRequest> _bodyRequests = new();
        private readonly Queue<OSSelectionRequest> _levelUpRequests = new();
        private readonly HashSet<int> _knownRequestIds = new();

        public int Count => _bodyRequests.Count + _levelUpRequests.Count;
        public int BodyCount => _bodyRequests.Count;
        public int LevelUpCount => _levelUpRequests.Count;

        /// <summary>
        /// Enqueues one request. StartBody and BodyRole requests always precede pending LevelUp requests.
        /// </summary>
        public OSRuleResult<OSSelectionRequest> Enqueue(OSSelectionRequest request)
        {
            if (request.RequestId <= 0 || request.CreatedTick < 0)
            {
                return OSRuleResult<OSSelectionRequest>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "selection.invalid_request",
                    request);
            }

            if (!_knownRequestIds.Add(request.RequestId))
            {
                return OSRuleResult<OSSelectionRequest>.Rejected(
                    OSResultCode.Duplicate,
                    "selection.duplicate_request",
                    request);
            }

            switch (request.Kind)
            {
                case OSSelectionKind.StartBody:
                case OSSelectionKind.BodyRole:
                    _bodyRequests.Enqueue(request);
                    break;
                case OSSelectionKind.LevelUp:
                    _levelUpRequests.Enqueue(request);
                    break;
                default:
                    _knownRequestIds.Remove(request.RequestId);
                    return OSRuleResult<OSSelectionRequest>.Rejected(
                        OSResultCode.RejectedRequirement,
                        "selection.unknown_kind",
                        request);
            }

            return OSRuleResult<OSSelectionRequest>.Queued(request, "selection.queued");
        }

        /// <summary>
        /// Returns the next Body-priority request without removing it.
        /// </summary>
        public bool TryPeek(out OSSelectionRequest request)
        {
            if (_bodyRequests.Count > 0)
            {
                request = _bodyRequests.Peek();
                return true;
            }

            if (_levelUpRequests.Count > 0)
            {
                request = _levelUpRequests.Peek();
                return true;
            }

            request = default;
            return false;
        }

        /// <summary>
        /// Removes and returns the next Body-priority request.
        /// </summary>
        public bool TryDequeue(out OSSelectionRequest request)
        {
            if (_bodyRequests.Count > 0)
            {
                request = _bodyRequests.Dequeue();
                return true;
            }

            if (_levelUpRequests.Count > 0)
            {
                request = _levelUpRequests.Dequeue();
                return true;
            }

            request = default;
            return false;
        }

        /// <summary>
        /// Clears all requests and duplicate tracking for a new session.
        /// </summary>
        public void Clear()
        {
            _bodyRequests.Clear();
            _levelUpRequests.Clear();
            _knownRequestIds.Clear();
        }
    }
}
