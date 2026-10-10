namespace ClickItMax.Features.Labels.Application
{
    internal sealed class LabelClickSettingsService(
        ClickItMaxSettings settings,
        IMechanicPrioritySnapshotProvider mechanicPrioritySnapshotProvider,
        Func<IReadOnlyList<LabelOnGround>?, bool> hasLazyModeRestrictedItems,
        Func<Keys, bool> isClickHotkeyHeld)
    {
        private readonly ClickSettingsFactory _factory = new(
            settings,
            mechanicPrioritySnapshotProvider,
            hasLazyModeRestrictedItems,
            isClickHotkeyHeld);

        public ClickSettings Create(IReadOnlyList<LabelOnGround>? allLabels)
            => _factory.Create(allLabels);
    }
}