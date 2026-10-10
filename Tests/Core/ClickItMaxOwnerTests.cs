namespace ClickItMax.Tests.Core
{
    [TestClass]
    [DoNotParallelize]
    public class ClickItMaxOwnerTests
    {
        [TestMethod]
        public void OnLoad_EnablesMultithreading()
        {
            var plugin = new ClickItMax();

            plugin.OnLoad();

            plugin.CanUseMultiThreading.Should().BeTrue();
        }

        [TestMethod]
        public void Initialise_WhenSettingsMissing_ThrowsInvalidOperationException()
        {
            var plugin = new ClickItMax();

            Action act = () => plugin.Initialise();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Settings is null during plugin initialization.");
        }

        [TestMethod]
        public void Initialise_WhenSettingsPresent_DelegatesToLifecycleCoordinator()
        {
            var plugin = new ClickItMax();
            var settings = new ClickItMaxSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickItMax.Settings), settings);

            Action act = () => plugin.Initialise();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GameController is null during plugin initialization.*");
        }

        [TestMethod]
        public void OnClose_WithAssignedSettings_ClearsPluginContextFields_BeforeBaseSaveFailure()
        {
            var plugin = new ClickItMax();
            var settings = new ClickItMaxSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickItMax.Settings), settings);
            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            plugin.State.Services.AreaService = new AreaService();
            plugin.State.Rendering.OverlayRenderHost = new OverlayRenderHost();
            LockManager.Instance = new LockManager(settings);

            Action act = () => plugin.OnClose();

            act.Should().Throw<NullReferenceException>();

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Rendering.OverlayRenderHost.Should().BeNull();
            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();
            LockManager.Instance.Should().BeNull();
        }

        [TestMethod]
        public void GetEffectiveSettingsForLifecycle_ReturnsAssignedSettings()
        {
            var plugin = new ClickItMax();
            var settings = new ClickItMaxSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickItMax.Settings), settings);

            ClickItMaxSettings effectiveSettings = plugin.GetEffectiveSettingsForLifecycle();

            effectiveSettings.Should().BeSameAs(settings);
        }

        [TestMethod]
        public void GetDebugClipboardService_ReturnsCachedInstance()
        {
            var plugin = new ClickItMax();

            DebugClipboardService first = plugin.GetDebugClipboardService();
            DebugClipboardService second = plugin.GetDebugClipboardService();

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void Render_ReturnsEarly_WhenShuttingDown()
        {
            var plugin = new ClickItMax();

            plugin.State.Runtime.IsShuttingDown = true;
            plugin.State.Rendering.IsRendering = false;

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_ReturnsEarly_WhenPerformanceMonitorMissing()
        {
            var plugin = new ClickItMax();

            plugin.State.Runtime.IsShuttingDown = false;
            plugin.State.Services.PerformanceMonitor = null;

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_ResetsRenderingFlag_AfterDelegatingToRenderHost()
        {
            var plugin = new ClickItMax();
            var settings = new ClickItMaxSettings();

            plugin.State.Runtime.IsShuttingDown = false;
            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }
    }
}