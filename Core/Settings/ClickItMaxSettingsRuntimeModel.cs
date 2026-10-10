namespace ClickItMax
{
    public partial class ClickItMaxSettings
    {
        public bool IsLazyModeDisableHotkeyToggleModeEnabled()
            => ClickItMaxSettingsRuntimeService.IsLazyModeDisableHotkeyToggleModeEnabled(this);

        public bool IsClickHotkeyToggleModeEnabled()
            => ClickItMaxSettingsRuntimeService.IsClickHotkeyToggleModeEnabled(this);

        public bool IsInitialUltimatumClickEnabled()
            => ClickItMaxSettingsRuntimeService.IsInitialUltimatumClickEnabled(this);

        public bool IsOtherUltimatumClickEnabled()
            => ClickItMaxSettingsRuntimeService.IsOtherUltimatumClickEnabled(this);

        public bool IsUltimatumTakeRewardButtonClickEnabled()
            => ClickItMaxSettingsRuntimeService.IsUltimatumTakeRewardButtonClickEnabled(this);

        public IReadOnlyList<string> GetMechanicPriorityOrder()
            => ClickItMaxSettingsRuntimeService.GetMechanicPriorityOrder(this);

        public IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds()
            => ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(this);

        public IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById()
            => ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(this);
    }
}