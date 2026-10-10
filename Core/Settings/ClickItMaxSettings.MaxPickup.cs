namespace ClickItMax
{
    public partial class ClickItMaxSettings
    {
        [IgnoreMenu] public ToggleNode MaxPickupEnabled { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupVerifyNearUiHover { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupVerifyFarUiHover { get; set; } = new(true);
        [IgnoreMenu] public RangeNode<int> MaxPickupNearRadius { get; set; } = new(20, 1, 40);
        [IgnoreMenu] public RangeNode<int> MaxPickupRepeatDelayMs { get; set; } = new(800, 100, 3000);
        [IgnoreMenu, JsonIgnore] public string MaxPickupStatus { get; internal set; } = "Пауза";
    }
}
