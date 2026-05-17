namespace TMBS.Core.Execution
{
    public enum ExecutionDecisionType
    {
        Reject,
        ExecuteImmediate
    }

    public readonly struct ExecutionDecision
    {
        public readonly ExecutionDecisionType Type;

        public ExecutionDecision(ExecutionDecisionType type)
        {
            Type = type;
        }

        public static ExecutionDecision Reject => new ExecutionDecision(ExecutionDecisionType.Reject);
        public static ExecutionDecision ExecuteImmediate => new ExecutionDecision(ExecutionDecisionType.ExecuteImmediate);
    }
}

