namespace ClickItMax.Core.Runtime
{
    internal static class PluginAlertServiceHost
    {
        public static AlertService GetOrCreateAlertService(ClickItMax owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            owner.State.Services.AlertService ??= new AlertService(
                () => owner.Settings,
                owner.GetEffectiveSettingsForLifecycle,
                () => SafeGetConfigDirectory(owner),
                () => owner.GameController,
                owner.LogMessage,
                owner.LogError);

            return owner.State.Services.AlertService;
        }

        private static string SafeGetConfigDirectory(ClickItMax owner)
        {
            try
            {
                return owner.ConfigDirectory;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }
    }
}