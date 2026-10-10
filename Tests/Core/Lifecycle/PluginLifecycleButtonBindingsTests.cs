namespace ClickItMax.Tests.Core.Lifecycle
{
    [TestClass]
    public class PluginLifecycleButtonBindingsTests
    {
        [TestMethod]
        public void Subscribe_WiresCopyAdditionalDebugInfoButton_ToClipboardRequestPath()
        {
            var plugin = new ClickItMax();
            var settings = new ClickItMaxSettings();
            DebugClipboardService debugClipboardService = plugin.GetDebugClipboardService();
            var bindings = new PluginLifecycleButtonBindings(debugClipboardService);

            bindings.Subscribe(settings);

            SettingsUiRenderHelpers.TriggerButtonNode(settings.CopyAdditionalDebugInfoButton);

            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeTrue();
        }

        [TestMethod]
        public void Unsubscribe_RemovesCopyAdditionalDebugInfoHandler_FromRuntimeAndEffectiveSettings()
        {
            var plugin = new ClickItMax();
            var runtimeSettings = new ClickItMaxSettings();
            var effectiveSettings = new ClickItMaxSettings();
            DebugClipboardService debugClipboardService = plugin.GetDebugClipboardService();
            var bindings = new PluginLifecycleButtonBindings(debugClipboardService);

            bindings.Subscribe(runtimeSettings);
            bindings.Subscribe(effectiveSettings);
            bindings.Unsubscribe(runtimeSettings, effectiveSettings);

            SettingsUiRenderHelpers.TriggerButtonNode(runtimeSettings.CopyAdditionalDebugInfoButton);
            SettingsUiRenderHelpers.TriggerButtonNode(effectiveSettings.CopyAdditionalDebugInfoButton);

            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }
    }
}