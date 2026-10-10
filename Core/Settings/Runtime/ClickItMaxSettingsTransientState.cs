namespace ClickItMax.Core.Settings.Runtime
{
    internal sealed class ClickItMaxSettingsTransientState
    {
        internal ClickItMaxSettingsUiState UiState { get; } = new();
        internal ClickItMaxSettingsRuntimeCacheState RuntimeCache { get; } = new();
    }
}