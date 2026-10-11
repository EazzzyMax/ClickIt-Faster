namespace ClickItMax
{
    public partial class ClickItMaxSettings
    {
        [IgnoreMenu] public ToggleNode MaxPickupEnabled { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupVerifyNearUiHover { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupVerifyFarUiHover { get; set; } = new(true);
        [IgnoreMenu] public RangeNode<int> MaxPickupNearRadius { get; set; } = new(20, 1, 40);
        [IgnoreMenu] public RangeNode<int> MaxPickupRepeatDelayMs { get; set; } = new(800, 100, 3000);
        [IgnoreMenu] public ToggleNode MaxPickupMovementEnabled { get; set; } = new(true);
        [IgnoreMenu] public RangeNode<int> MaxPickupMovementDistance { get; set; } = new(60, 1, 200);
        [IgnoreMenu] public RangeNode<int> MaxPickupFlameDashWaitMs { get; set; } = new(300, 0, 850);
        [IgnoreMenu] public ToggleNode MaxPickupFlameDash { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupFrostblink { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupLeapSlam { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupWhirlingBlades { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupDash { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupBlinkArrow { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupLightningWarp { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupShieldCharge { get; set; } = new(true);
        [IgnoreMenu] public ToggleNode MaxPickupDashAlwaysAttackInPlace { get; set; } = new(false);
        [IgnoreMenu, JsonIgnore] public string MaxPickupStatus { get; internal set; } = "Пауза";
    }
}
