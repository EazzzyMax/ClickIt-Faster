namespace ClickItMax
{
    public partial class ClickItMax
    {

        public override Job? Tick()
        {
            PluginInputHost.Tick(State, Settings);
            return null;
        }
    }
}
