namespace Ouroboros.Core
{
    /// <summary>
    /// Owns body-fragment progress and the technical guard without depending on Unity scene state.
    /// </summary>
    public sealed class OSBodyGrowthProgress
    {
        public OSBodyGrowthProgress(int fragmentRequirement, int technicalGuard)
        {
            FragmentRequirement = fragmentRequirement > 0 ? fragmentRequirement : 1;
            TechnicalGuard = technicalGuard > 0 ? technicalGuard : 1;
        }

        public int FragmentRequirement { get; private set; }
        public int TechnicalGuard { get; }
        public int FragmentProgress { get; private set; }
        public bool HasDeferredRequest => FragmentProgress >= FragmentRequirement;

        /// <summary>
        /// Adds fragments and returns the number of BodyRole requests that can be queued now.
        /// Progress is held at one full requirement while the active-plus-pending guard is full.
        /// </summary>
        public OSRuleResult<int> AddFragments(int amount, int activeSegments, int pendingRequests)
        {
            if (amount <= 0 || activeSegments < 0 || pendingRequests < 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.fragment.invalid_input");
            }

            var availableSlots = TechnicalGuard - activeSegments - pendingRequests;
            if (availableSlots <= 0)
            {
                FragmentProgress = FragmentRequirement;
                return OSRuleResult<int>.Queued(0, "body.fragment.deferred");
            }

            var total = (long)FragmentProgress + amount;
            var requestedCount = (int)System.Math.Min(total / FragmentRequirement, availableSlots);
            var remainder = total - ((long)requestedCount * FragmentRequirement);
            FragmentProgress = requestedCount == availableSlots && remainder >= FragmentRequirement
                ? FragmentRequirement
                : (int)remainder;

            return OSRuleResult<int>.Accepted(requestedCount, "body.fragment.applied");
        }

        /// <summary>
        /// Converts one held full gauge into a request after chain capacity becomes available.
        /// </summary>
        public OSRuleResult<int> TryResumeDeferred(int activeSegments, int pendingRequests)
        {
            if (!HasDeferredRequest)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.fragment.no_deferred_request");
            }

            if (activeSegments < 0 || pendingRequests < 0 ||
                activeSegments + pendingRequests >= TechnicalGuard)
            {
                return OSRuleResult<int>.Queued(0, "body.fragment.still_deferred");
            }

            FragmentProgress -= FragmentRequirement;
            return OSRuleResult<int>.Accepted(1, "body.fragment.resumed");
        }

        /// <summary>
        /// Clears all fragment progress for a new session.
        /// </summary>
        public void Reset()
        {
            FragmentProgress = 0;
        }

        public OSRuleResult<int> SetFragmentRequirement(int requirement)
        {
            if (requirement <= 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "body.fragment.requirement.invalid",
                    FragmentRequirement);
            }

            FragmentRequirement = requirement;
            return OSRuleResult<int>.Accepted(FragmentRequirement, "body.fragment.requirement.updated");
        }
    }
}
