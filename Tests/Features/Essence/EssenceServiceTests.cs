namespace ClickItMax.Tests.Features.Essence
{
    [TestClass]
    public class EssenceServiceTests
    {
        [TestMethod]
        public void ShouldCorruptEssence_ReturnsFalse_WhenLabelIsNull()
        {
            var settings = new ClickItMaxSettings();
            var service = new EssenceService(settings);

            bool shouldCorrupt = service.ShouldCorruptEssence(null);

            shouldCorrupt.Should().BeFalse();
        }
    }
}