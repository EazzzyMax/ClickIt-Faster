namespace ClickItMax.Tests.Core.Settings
{
    [TestClass]
    public class ClickItMaxSettingsRuntimeServiceTests
    {
        [TestMethod]
        public void GetMechanicPriorityOrder_RebuildsSnapshotWhenSelectionChanges()
        {
            var settings = new ClickItMaxSettings
            {
                MechanicPriorityOrder = new List<string> { "essences", "items" },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItMaxSettings.PriorityComparer),
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
            };

            IReadOnlyList<string> initial = ClickItMaxSettingsRuntimeService.GetMechanicPriorityOrder(settings);
            settings.MechanicPriorityOrder = new List<string> { "items", "essences" };

            IReadOnlyList<string> updated = ClickItMaxSettingsRuntimeService.GetMechanicPriorityOrder(settings);

            updated.Should().ContainInOrder("items", "essences");
            updated.Should().NotEqual(initial);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceIds_ReusesSnapshot_WhenSetUnchanged()
        {
            var settings = new ClickItMaxSettings
            {
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItMaxSettings.PriorityComparer) { "shrines", "essences" },
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
            };

            IReadOnlyCollection<string> first = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);
            IReadOnlyCollection<string> second = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceIds_Rebuilds_WhenSameCountSetChangesContent()
        {
            var settings = new ClickItMaxSettings
            {
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItMaxSettings.PriorityComparer) { "shrines", "essences" },
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
            };

            IReadOnlyCollection<string> initial = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            settings.MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItMaxSettings.PriorityComparer) { "shrines", "strongboxes" };

            IReadOnlyCollection<string> updated = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(settings);

            updated.Should().NotBeSameAs(initial);
            updated.Should().Equal("shrines", "strongboxes");
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceWithinById_ReusesSnapshot_WhenMapUnchanged()
        {
            var settings = new ClickItMaxSettings
            {
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
                {
                    ["shrines"] = 40,
                    ["essences"] = 60,
                }
            };

            IReadOnlyDictionary<string, int> first = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);
            IReadOnlyDictionary<string, int> second = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void GetMechanicPriorityIgnoreDistanceWithinById_Rebuilds_WhenValueChanges()
        {
            var settings = new ClickItMaxSettings
            {
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
                {
                    ["shrines"] = 40,
                }
            };

            ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            settings.MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItMaxSettings.PriorityComparer)
            {
                ["shrines"] = 80,
            };

            IReadOnlyDictionary<string, int> updated = ClickItMaxSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(settings);

            updated["shrines"].Should().Be(80);
        }
    }
}