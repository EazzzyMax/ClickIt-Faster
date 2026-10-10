namespace ClickItMax.Core.Bootstrap
{
    internal readonly record struct SettingsDomainServices(
        AlertService AlertService,
        ClickItMaxSettings EffectiveSettings);

    internal static class SettingsDomainAssembler
    {
        // Thin runtime entry wrapper so the injected internal overload stays testable without runtime traversal.
        public static SettingsDomainServices Assemble(ClickItMax owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            return Assemble(
                owner.GetAlertService(),
                owner.GetEffectiveSettingsForLifecycle());
        }
        internal static SettingsDomainServices Assemble(
            AlertService alertService,
            ClickItMaxSettings effectiveSettings)
            => new(alertService, effectiveSettings);

        public static void WireActions(
            ClickItMaxSettings settings,
            ClickItMaxSettings effectiveSettings,
            AlertService alertService,
            PluginServiceRegistry registry)
        {
            settings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            settings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;

            registry.Register(() => settings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => settings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);

            if (ReferenceEquals(settings, effectiveSettings))
                return;

            effectiveSettings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            effectiveSettings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;

            registry.Register(() => effectiveSettings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => effectiveSettings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
        }
    }
}
