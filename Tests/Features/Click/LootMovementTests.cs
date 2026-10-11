using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class LootMovementTests
{
    [DataTestMethod]
    [DataRow("flame_dash", LootMovementKind.FlameDash)]
    [DataRow("frostblink", LootMovementKind.Frostblink)]
    [DataRow("leap_slam", LootMovementKind.LeapSlam)]
    [DataRow("whirling_blades", LootMovementKind.WhirlingBlades)]
    [DataRow("dash", LootMovementKind.Dash)]
    [DataRow("blink_arrow", LootMovementKind.BlinkArrow)]
    [DataRow("lightning_warp", LootMovementKind.LightningWarp)]
    [DataRow("shield_charge", LootMovementKind.ShieldCharge)]
    public void OnlyApprovedSkillsAreClassified(string name, LootMovementKind expected) => LootMovementRules.Classify(name).Should().Be(expected);

    [DataTestMethod]
    [DataRow("flame_dash_alt_y")]
    [DataRow("charged_dash_alt_x")]
    [DataRow("mirror_arrow")]
    [DataRow("chain_hook")]
    [DataRow("ambush_player")]
    [DataRow("consecrated_path")]
    [DataRow("phase_run")]
    [DataRow("withering_step")]
    [DataRow("bodyswap")]
    [DataRow("corpse_warp")]
    [DataRow("charged_dash")]
    [DataRow("smoke_mine")]
    [DataRow("slow_dodge")]
    public void ExcludedSkillsAndVariantsAreNotSubstringMatched(string name) => LootMovementRules.Classify(name).Should().BeNull();

    [TestMethod]
    public void PriorityIsUserOrderWithShieldChargeLast() => Enum.GetValues<LootMovementKind>().Should().Equal(
        LootMovementKind.FlameDash, LootMovementKind.Frostblink, LootMovementKind.LeapSlam, LootMovementKind.WhirlingBlades,
        LootMovementKind.Dash, LootMovementKind.BlinkArrow, LootMovementKind.LightningWarp, LootMovementKind.ShieldCharge);

    [TestMethod]
    public void DefaultDistanceIsSixtyAndCanBeChanged()
    {
        var settings = new ClickItMaxSettings();
        settings.MaxPickupMovementDistance.Value.Should().Be(60);
        LootMovementRules.ShouldUse(59.99f, 20, 60).Should().BeFalse();
        LootMovementRules.ShouldUse(60, 20, 60).Should().BeTrue();
        LootMovementRules.ShouldUse(30, 20, 25).Should().BeTrue();
        LootMovementRules.ShouldUse(20, 20, 10).Should().BeFalse();
        LootMovementRules.ShouldUse(float.NaN, 20, 60).Should().BeFalse();
    }

    [TestMethod]
    public void FlameDashNeedsChargeAnd850SinceAnyUse()
    {
        bool Ready(long now, int charges = 1) => LootMovementRules.IsReady(LootMovementKind.FlameDash, true, true, true, false, charges, now, 1000);
        Ready(1849).Should().BeFalse(); Ready(1850).Should().BeTrue(); Ready(2000, 0).Should().BeFalse();
        LootMovementRules.IsReady(LootMovementKind.ShieldCharge, true, true, true, false, 0, 1000, 1000).Should().BeTrue();
    }

    [TestMethod]
    public void ActualCooldownResourceAndWeaponGatesAreRequired()
    {
        LootMovementRules.IsReady(LootMovementKind.Frostblink, true, true, true, true, 1, 0, null).Should().BeFalse();
        LootMovementRules.IsReady(LootMovementKind.LeapSlam, true, false, true, false, 0, 0, null).Should().BeFalse();
        LootMovementRules.IsReady(LootMovementKind.ShieldCharge, true, true, false, false, 0, 0, null).Should().BeFalse();
        LootMovementRules.IsReady(LootMovementKind.Dash, false, true, true, false, 1, 0, null).Should().BeFalse();
    }

    [TestMethod]
    public void ManualAndAutomaticCastsShareClockAndDelayedAcceptanceCannotShorten850()
    {
        var clock = new FlameDashCooldownClock();
        clock.Observe(500, 123, 3, 3, false); clock.LastUse.Should().BeNull();
        clock.RecordSent(1000);
        clock.Observe(1120, 123, 2, 3, true); clock.LastUse.Should().Be(1120);
        clock.Observe(1300, 123, 2, 3, true); clock.LastUse.Should().Be(1120, "animation tail is not another use");
        LootMovementRules.IsReady(LootMovementKind.FlameDash, true, true, true, false, 2, 1969, clock.LastUse).Should().BeFalse();
        LootMovementRules.IsReady(LootMovementKind.FlameDash, true, true, true, false, 2, 1970, clock.LastUse).Should().BeTrue();
        clock.Observe(1500, 123, 2, 3, false);
        clock.Observe(2000, 123, 1, 3, true); clock.LastUse.Should().Be(2000, "manual cast shares the interval");
        clock.Observe(2500, 123, 2, 3, false); clock.LastUse.Should().Be(2000, "recharge is not use");
    }

    [TestMethod]
    public void FirstPartialChargesObservationIsConservativeAndMaxChargesIsNotHardcoded()
    {
        var clock = new FlameDashCooldownClock();
        clock.Observe(1000, 123, 3, 4, false); clock.LastUse.Should().Be(1000);
    }

    [DataTestMethod]
    [DataRow("W", Keys.W, false)]
    [DataRow("Ctrl+D", Keys.D, true)]
    [DataRow("Ctrl+Q", Keys.Q, true)]
    [DataRow("Spc", Keys.Space, false)]
    [DataRow("4", Keys.D4, false)]
    public void PanelBindingsRetainCtrl(string text, Keys key, bool ctrl)
    {
        LootMovementChord.TryParse(text, out var chord).Should().BeTrue();
        chord.Key.Should().Be(key); chord.Ctrl.Should().Be(ctrl);
    }

    [DataTestMethod]
    [DataRow("LMB")]
    [DataRow("RMB")]
    [DataRow("Ctrl")]
    [DataRow("Foo+W")]
    [DataRow("Ctrl+")]
    [DataRow("")]
    public void InvalidBindingsCannotBecomeBareKeys(string text) => LootMovementChord.TryParse(text, out _).Should().BeFalse();

    [TestMethod]
    public void FlameDashChargeSpendIsNotArrivalButJumpReleasesImmediately()
    {
        var p = Begin(LootMovementKind.FlameDash);
        p.Observe(20, 0, 0, true, 2, false).Finished.Should().BeFalse();
        p.Observe(200, 0, 0, true, 2, false).Finished.Should().BeFalse();
        var jumped = p.Observe(300, 50, 0, true, 2, false);
        jumped.Finished.Should().BeTrue(); jumped.Failed.Should().BeFalse(); jumped.Reason.Should().Be("teleported");
    }

    [DataTestMethod]
    [DataRow(LootMovementKind.BlinkArrow)]
    [DataRow(LootMovementKind.LightningWarp)]
    public void DeferredTeleportWaitsAfterCastAnimationEnds(LootMovementKind kind)
    {
        var p = Begin(kind);
        p.Observe(20, 0, 0, true, 0, true).Finished.Should().BeFalse();
        p.Observe(300, 0, 0, false, 0, true).Finished.Should().BeFalse();
        p.Observe(1000, 0, 0, false, 0, false).Finished.Should().BeFalse();
        p.Observe(1300, 40, 0, false, 0, false).Finished.Should().BeTrue();
    }

    [TestMethod]
    public void OldWalkAndStopDoNotSatisfyDeferredTeleport()
    {
        var p = Begin(LootMovementKind.LightningWarp);
        p.Observe(20, 1, 0, true, 0, false).Finished.Should().BeFalse();
        p.Observe(40, 2, 0, false, 0, false).Finished.Should().BeFalse();
        p.Observe(200, 2, 0, false, 0, false).Finished.Should().BeFalse();
        p.Observe(400, 50, 0, false, 0, false).Reason.Should().Be("teleported");
    }

    [TestMethod]
    public void ShieldChargeCannotBeInterruptedByFalseWalkingFlagOrNearbyLoot()
    {
        var p = Begin(LootMovementKind.ShieldCharge);
        p.Observe(20, 0, 0, true, 0, false).Finished.Should().BeFalse();
        p.Observe(100, 10, 0, true, 0, false).Finished.Should().BeFalse();
        p.Observe(200, 25, 0, true, 0, false).Finished.Should().BeFalse();
        var finished = p.Observe(230, 26, 0, false, 0, false);
        finished.Finished.Should().BeTrue(); finished.Failed.Should().BeFalse();
    }

    [TestMethod]
    public void ShieldBashAtWallFailsWithoutInventedMovement()
    {
        var p = Begin(LootMovementKind.ShieldCharge);
        p.Observe(20, 0, 0, true, 0, false);
        p.Observe(200, 0, 0, false, 0, false).Failed.Should().BeTrue();
        p.Active.Should().BeFalse();
    }

    [TestMethod]
    public void NoAcceptedInputHasBoundedStartWatchdog()
    {
        var p = Begin(LootMovementKind.FlameDash);
        p.Observe(249, 0, 0, false, 3, false).Finished.Should().BeFalse();
        var end = p.Observe(250, 0, 0, false, 3, false);
        end.Finished.Should().BeTrue(); end.Failed.Should().BeTrue();
    }

    [TestMethod]
    public void AcceptedButBlockedCastHasBoundedCompletionAndResetClearsOwnership()
    {
        var p = Begin(LootMovementKind.FlameDash);
        p.Observe(20, 0, 0, true, 2, false);
        p.Observe(1999, 0, 0, false, 2, false).Finished.Should().BeFalse();
        p.Observe(2000, 0, 0, false, 2, false).Failed.Should().BeTrue();
        p = Begin(LootMovementKind.LightningWarp); p.Reset(); p.Active.Should().BeFalse();
    }

    [TestMethod]
    public void UncertainWarpCannotBeQueuedAgainEvenAfterHotkeyReset()
    {
        var p = Begin(LootMovementKind.LightningWarp);
        p.Observe(20, 0, 0, true, 0, false);
        p.Observe(5000, 0, 0, false, 0, false).Failed.Should().BeTrue();
        p.WarpUncertain.Should().BeTrue(); p.Reset(); p.WarpUncertain.Should().BeTrue();
        p.ResetForArea(); p.WarpUncertain.Should().BeFalse();
    }

    private static LootMovementTracker Begin(LootMovementKind kind)
    {
        var p = new LootMovementTracker();
        p.Begin(new(1, 100), kind, 42, 0, 0, 0, kind == LootMovementKind.FlameDash ? 3 : 0, false, 0);
        return p;
    }
}
