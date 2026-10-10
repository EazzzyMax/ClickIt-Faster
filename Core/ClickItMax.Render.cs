namespace ClickItMax
{
    public partial class ClickItMax
    {

        private void RenderInternal()
            => PluginRenderHost.Render(State, EffectiveSettings, GameController, Graphics, DebugClipboardService, PerformanceSettingsPanelRenderer.DrawStartupSetupFlow);

    }
}


