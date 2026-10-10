namespace ClickItMax.Features.Area
{
    internal static class AreaVisibilityRules
    {
        internal static bool ShouldUseVisibleUiBlockedRectangle(bool elementIsValid, bool elementIsVisible)
            => elementIsValid && elementIsVisible;
    }
}