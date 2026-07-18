namespace Ouroboros.Core
{
    public readonly struct OSRuleResult<T>
    {
        public OSRuleResult(OSResultCode code, string reasonKey, T payload)
        {
            Code = code;
            ReasonKey = reasonKey ?? string.Empty;
            Payload = payload;
        }

        public OSResultCode Code { get; }
        public string ReasonKey { get; }
        public T Payload { get; }
        public bool IsAccepted => Code is OSResultCode.Accepted or OSResultCode.Queued;

        public static OSRuleResult<T> Accepted(T payload, string reasonKey = "rule.accepted")
        {
            return new OSRuleResult<T>(OSResultCode.Accepted, reasonKey, payload);
        }

        public static OSRuleResult<T> Queued(T payload, string reasonKey = "rule.queued")
        {
            return new OSRuleResult<T>(OSResultCode.Queued, reasonKey, payload);
        }

        public static OSRuleResult<T> Rejected(OSResultCode code, string reasonKey, T payload = default)
        {
            return new OSRuleResult<T>(code, reasonKey, payload);
        }
    }
}
