namespace ClickItMax.Tests.Core.Settings
{
    [TestClass]
    public class AltarWeightSettingsServiceTests
    {
        [TestMethod]
        public void InitializeDefaultWeights_SeedsDivineOrbAlerts()
        {
            var settings = new ClickItMaxSettings();

            AltarWeightSettingsService.InitializeDefaultWeights(settings);

            settings.ModAlerts[AltarWeightSettingsService.BuildCompositeKey(ClickItMaxSettings.AltarTypeMinion, "#% chance to drop an additional Divine Orb")]
                .Should().BeTrue();
            settings.ModAlerts[AltarWeightSettingsService.BuildCompositeKey(ClickItMaxSettings.AltarTypeBoss, "Final Boss drops # additional Divine Orbs")]
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetModAlert_ReturnsFalse_WhenModIdIsMissing()
        {
            var settings = new ClickItMaxSettings();

            AltarWeightSettingsService.GetModAlert(settings, string.Empty, ClickItMaxSettings.AltarTypeBoss)
                .Should().BeFalse();
        }

        [TestMethod]
        public void GetModAlert_ReturnsCompositeAlert_WhenPresent()
        {
            var settings = new ClickItMaxSettings();
            string compositeKey = AltarWeightSettingsService.BuildCompositeKey(ClickItMaxSettings.AltarTypeBoss, "mod-id");
            settings.ModAlerts[compositeKey] = true;

            AltarWeightSettingsService.GetModAlert(settings, "mod-id", ClickItMaxSettings.AltarTypeBoss)
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetModAlert_FallsBackToLegacyBareModId_WhenCompositeAlertIsMissing()
        {
            var settings = new ClickItMaxSettings();
            settings.ModAlerts["legacy-mod"] = true;

            AltarWeightSettingsService.GetModAlert(settings, "legacy-mod", ClickItMaxSettings.AltarTypeMinion)
                .Should().BeTrue();
            settings.GetModAlert("legacy-mod", ClickItMaxSettings.AltarTypeMinion)
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetModAlert_ReturnsFalse_WhenNoAlertEntryExists()
        {
            var settings = new ClickItMaxSettings();

            AltarWeightSettingsService.GetModAlert(settings, "missing-mod", ClickItMaxSettings.AltarTypeMinion)
                .Should().BeFalse();
        }
    }
}