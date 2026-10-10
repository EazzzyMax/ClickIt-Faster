namespace ClickItMax
{
    internal static class ClickItMaxSettingsMigrationService
    {
        private static readonly SettingsDefaultsService DefaultsService = new();
        private static readonly SettingsNormalizationService NormalizationService = new();

        internal static void Apply(ClickItMaxSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            DefaultsService.Apply(settings);
            NormalizationService.Apply(settings);
        }
    }

    public partial class ClickItMaxSettings
    {
        [OnDeserialized]
        internal void OnDeserializedApplySettingsMigration(StreamingContext context)
        {
            ClickItMaxSettingsMigrationService.Apply(this);
        }
    }
}