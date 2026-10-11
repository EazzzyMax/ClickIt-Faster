namespace ClickItMax.Features.Area
{
    internal sealed class BlockedAreaEvaluatorPipeline(IReadOnlyList<Func<AreaBlockedSnapshot, Vector2, bool>> evaluators)
    {
        private readonly IReadOnlyList<Func<AreaBlockedSnapshot, Vector2, bool>> _evaluators = evaluators;

        internal bool IsBlocked(AreaBlockedSnapshot snapshot, Vector2 point)
            => IsBlocked(snapshot, point, out _);

        internal bool IsBlocked(AreaBlockedSnapshot snapshot, Vector2 point, out int ruleIndex)
        {
            for (int i = 0; i < _evaluators.Count; i++)
                if (_evaluators[i](snapshot, point))
                {
                    ruleIndex = i;
                    return true;
                }

            ruleIndex = -1;
            return false;
        }
    }
}
