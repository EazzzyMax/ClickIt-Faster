namespace ClickItMax.Core.Lifecycle
{
    internal sealed class PluginLifecycleButtonBindings(DebugClipboardService debugClipboardService)
    {
        private readonly DebugClipboardService _debugClipboardService = debugClipboardService;

        public void Subscribe(ClickItMaxSettings settings)
        {
            settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            settings.CopyAdditionalDebugInfoButton.OnPressed += CopyAdditionalDebugInfoButtonPressed;
        }

        public void Unsubscribe(ClickItMaxSettings runtimeSettings, ClickItMaxSettings effectiveSettings)
        {
            runtimeSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            runtimeSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            if (!ReferenceEquals(runtimeSettings, effectiveSettings))
            {
                effectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
                effectiveSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            }
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "https://github.com/Barragek0/ClickIt/issues");
        }

        private void CopyAdditionalDebugInfoButtonPressed()
        {
            _debugClipboardService.RequestAdditionalDebugInfoCopy();
        }
    }
}
