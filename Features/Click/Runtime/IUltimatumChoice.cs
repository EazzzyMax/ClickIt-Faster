namespace ClickItMax.Features.Click.Runtime
{
    internal interface IUltimatumChoice
    {
        int PriorityIndex { get; }
        bool IsSaturated { get; }
    }
}
