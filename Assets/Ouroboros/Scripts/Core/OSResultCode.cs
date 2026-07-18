namespace Ouroboros.Core
{
    public enum OSResultCode
    {
        Accepted,
        Queued,
        RejectedState,
        RejectedRequirement,
        RejectedNoTarget,
        RejectedInvulnerable,
        RejectedCutGuard,
        RejectedCapacity,
        RejectedRange,
        Duplicate,
        CancelledMissingSource,
        CancelledNoReservedSegment,
        ConfigurationError
    }
}
