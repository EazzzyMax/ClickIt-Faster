namespace ClickItMax.Features.Observability
{
    internal sealed class PluginDebugTelemetrySource(BaseSettingsPlugin<ClickItMaxSettings> plugin) : IDebugTelemetrySource
    {
        private readonly BaseSettingsPlugin<ClickItMaxSettings> _plugin = plugin;

        public DebugTelemetrySnapshot GetSnapshot()
        {
            if (_plugin is ClickItMax clickIt)
                return clickIt.State.GetDebugTelemetrySnapshot();

            return DebugTelemetrySnapshot.Empty;
        }

        public bool TryGetFreezeState(out long remainingMs, out string reason)
        {
            if (_plugin is ClickItMax clickIt)
                return clickIt.State.TryGetDebugTelemetryFreezeState(out remainingMs, out reason);

            remainingMs = 0;
            reason = string.Empty;
            return false;
        }
    }
}