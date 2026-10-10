namespace ClickItMax.UI.Settings
{
    internal static class SettingsUiBootstrapper
    {
        internal static void InitializeScreenNodes(ClickItMaxSettings settings)
            => CreateScreenNodes(settings).ApplyTo(settings);

        internal static ClickItMaxSettingsScreenNodes CreateScreenNodes(ClickItMaxSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            SettingsPanelSafeRenderer safePanelRenderer = new(settings);
            DebugTestingPanelRenderer debugTestingPanelRenderer = new(settings);
            ControlsPanelRenderer controlsPanelRenderer = new(settings);
            PerformanceSettingsPanelRenderer performancePanelRenderer = new(settings);
            PerformanceSettingsPanelRenderer.SetCurrent(() => performancePanelRenderer);
            ItemTypeFiltersPanelRenderer itemTypeFiltersPanelRenderer = new(settings);
            EssenceCorruptionPanelRenderer essenceCorruptionPanelRenderer = new(settings);
            StrongboxFilterPanelRenderer strongboxFilterPanelRenderer = new(settings);
            LazyModeNearbyMonsterRulesPanelRenderer lazyModeNearbyMonsterRulesPanelRenderer = new(settings);
            MechanicPriorityTablePanelRenderer mechanicPriorityPanelRenderer = new(settings);
            AltarSettingsPanelRenderer altarSettingsPanelRenderer = new(settings);
            UltimatumSettingsPanelRenderer ultimatumSettingsPanelRenderer = new(settings);
            HarvestSettingsPanelRenderer harvestSettingsPanelRenderer = new(settings);
            BlightSettingsPanelRenderer blightSettingsPanelRenderer = new(settings);
            MechanicsEmbeddedSettingsPanelRenderer mechanicsEmbeddedSettingsPanelRenderer = new(settings, itemTypeFiltersPanelRenderer, essenceCorruptionPanelRenderer, strongboxFilterPanelRenderer, ultimatumSettingsPanelRenderer, altarSettingsPanelRenderer, harvestSettingsPanelRenderer, blightSettingsPanelRenderer);
            MechanicsTablePanelRenderer mechanicsTablePanelRenderer = new(settings, mechanicsEmbeddedSettingsPanelRenderer);

            return ClickItMaxSettingsScreen.Compose(new ClickItMaxSettingsScreenBindings(
                debugTestingPanelRenderer.Draw,
                controlsPanelRenderer.Draw,
                performancePanelRenderer.Draw,
                lazyModeNearbyMonsterRulesPanelRenderer.Draw,
                () => altarSettingsPanelRenderer.DrawAltarsPanel(),
                altarSettingsPanelRenderer.DrawAltarModWeights,
                () => itemTypeFiltersPanelRenderer.DrawPanel(),
                mechanicPriorityPanelRenderer.Draw,
                essenceCorruptionPanelRenderer.DrawPanel,
                strongboxFilterPanelRenderer.DrawPanel,
                mechanicsTablePanelRenderer.Draw,
                () => ultimatumSettingsPanelRenderer.DrawModifierTablePanel(),
                () => ultimatumSettingsPanelRenderer.DrawTakeRewardModifierTablePanel(),
                SettingsUiRenderHelpers.PushStandardSliderWidth,
                SettingsUiRenderHelpers.PopStandardSliderWidth,
                safePanelRenderer.DrawPanel));
        }
    }
}