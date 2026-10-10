namespace ClickItMax.UI.Settings.Panels;

internal sealed class HarvestSettingsPanelRenderer(ClickItMaxSettings settings)
{
    private readonly ClickItMaxSettings _settings = settings;

    internal void DrawPanel()
    {
        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Show Visual Indicator##ShowHarvestLifeforceEstimation",
            _settings.ShowHarvestLifeforceEstimation,
            "Shows a visual indicator as to which harvest plot will be clicked on\n" +
            "based on the setting below.");

        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Click Higher Estimate##ClickHigherHarvestEstimate",
            _settings.ClickHigherHarvestEstimate,
            "When enabled, ClickItMax will click the plot with the highest estimated yield.\n" +
            "It will not click until 2 harvest plots are visible so it can compare their\n" +
            "estimated yields.\n\n" +
            "When disabled, ClickItMax will click the harvest plot nearest to the player\n" +
            "regardless of its estimated lifeforce yield.");
    }
}
