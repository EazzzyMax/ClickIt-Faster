using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class FlameDashWalkWaitTests
{
    private static readonly PickupItemKey Item = new(42, 4200);

    [DataTestMethod]
    [DataRow(766L, 84)]
    [DataRow(781L, 69)]
    [DataRow(828L, 22)]
    [DataRow(550L, 300)]
    [DataRow(549L, 0)]
    [DataRow(850L, 0)]
    [DataRow(851L, 0)]
    [DataRow(-1L, 0)]
    public void WaitWindowIncludesAllThreeObservedSlowShieldChargeCases(long elapsed, int expected)
        => FlameDashWalkWait.EligibleWaitMs(true, 1000 + elapsed, 1000, 300).Should().Be(expected);

    [TestMethod]
    public void DefaultWaitIsAdjustableAndZeroDisablesIt()
    {
        var settings = new ClickItMaxSettings();
        settings.MaxPickupFlameDashWaitMs.Value.Should().Be(300);
        FlameDashWalkWait.EligibleWaitMs(true, 1766, 1000, 0).Should().Be(0);
        FlameDashWalkWait.EligibleWaitMs(true, 1766, 1000, 83).Should().Be(0);
        FlameDashWalkWait.EligibleWaitMs(true, 1766, 1000, 84).Should().Be(84);
    }

    [TestMethod]
    public void MissingChargeApiGateBindingOrClockCannotRequestWait()
    {
        FlameDashWalkWait.EligibleWaitMs(false, 1800, 1000, 300).Should().Be(0);
        FlameDashWalkWait.EligibleWaitMs(true, 1800, null, 300).Should().Be(0);
        foreach (var flags in new[] {
            (allowed: true, usable: true, weapon: true, cooldown: false, charges: 0),
            (allowed: false, usable: true, weapon: true, cooldown: false, charges: 1),
            (allowed: true, usable: false, weapon: true, cooldown: false, charges: 1),
            (allowed: true, usable: true, weapon: false, cooldown: false, charges: 1),
            (allowed: true, usable: true, weapon: true, cooldown: true, charges: 1) })
        {
            bool readyExceptInterval = LootMovementRules.IsReady(LootMovementKind.FlameDash, flags.allowed,
                flags.usable, flags.weapon, flags.cooldown, flags.charges, 1800, null);
            FlameDashWalkWait.EligibleWaitMs(readyExceptInterval, 1800, 1000, 300).Should().Be(0);
        }
    }

    [TestMethod]
    public void WalkOwnsSameItemWhileWaitingThenReadyBlinkMayInterruptOwnedMovement()
    {
        var walk = new PickupMotionPolicy();
        var wait = new FlameDashWalkWait();
        walk.Observe(1766, false, 0, 0).AllowDispatch.Should().BeTrue();
        walk.RecordDispatch(Item, 1766, 0, 0, true);
        wait.Begin(Item);
        walk.Observe(1800, true, 3, 0).AllowDispatch.Should().BeFalse();
        wait.Observe(walk.ActiveItem, true, true, 100, 20, 60, false, 50).Should().Be(FlameDashWalkDecision.Waiting);
        walk.ActiveItem.Should().Be(Item);
        walk.CanDispatch(new PickupItemKey(99, 9900), 1800).Should().BeFalse("no opposite-target walk during the wait");
        walk.Observe(1850, true, 9, 0).AllowDispatch.Should().BeFalse();
        wait.Observe(walk.ActiveItem, true, true, 94, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cast);
        wait.Target.Should().Be(Item, "a ready decision alone does not claim successful input");
        // Adapter does this only after actual spell input succeeds.
        wait.Reset(); walk.TransferWalkToMovement(Item).Should().BeTrue();
        walk.Observe(1900, false, 50, 0);
        walk.CanDispatch(Item, 1900, 800).Should().BeTrue("the replaced walk command must not block landing pickup for 800 ms");
    }

    [TestMethod]
    public void TransferReleasesOnlyCurrentFarCommandAndPreservesOtherPickupTimers()
    {
        var p = new PickupMotionPolicy();
        var near = new PickupItemKey(43, 4300);
        p.Observe(1000, false, 0, 0);
        p.RecordDispatch(near, 1000, 0, 0, false);
        p.Observe(1020, false, 0, 0);
        p.RecordDispatch(Item, 1020, 0, 0, true);
        p.TransferWalkToMovement(near).Should().BeFalse("a different target cannot release the walk or its own retry timer");
        p.ActiveItem.Should().Be(Item);
        p.TransferWalkToMovement(Item).Should().BeTrue();
        p.Observe(1200, false, 50, 0);
        p.CanDispatch(Item, 1200, 800).Should().BeTrue();
        p.CanDispatch(near, 1200, 800).Should().BeFalse();
    }

    [TestMethod]
    public void FailedCastLeavesOriginalWalkAndRetryTimerIntact()
    {
        var p = new PickupMotionPolicy();
        var wait = Started();
        p.Observe(1000, false, 0, 0);
        p.RecordDispatch(Item, 1000, 0, 0, true);
        wait.Reset(); // Adapter cancels just the deferred blink on aim/input rejection.
        p.ActiveItem.Should().Be(Item);
        p.Observe(1100, true, 10, 0).AllowDispatch.Should().BeFalse();
        p.Observe(1200, false, 20, 0).AllowDispatch.Should().BeTrue();
        p.CanDispatch(Item, 1200, 800).Should().BeFalse();
        p.TransferWalkToMovement(Item).Should().BeFalse("the walk already ended, so its retry timer cannot be released");
    }

    [DataTestMethod]
    [DataRow(59.99f)]
    [DataRow(20f)]
    [DataRow(0f)]
    [DataRow(float.NaN)]
    public void WalkingCloseEnoughCancelsBlinkEvenWhenFlameIsReady(float distance)
    {
        var p = Started();
        p.Observe(Item, true, true, distance, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cancelled);
        p.Reason.Should().Be("already-close-enough");
        p.Target.Should().BeNull();
    }

    [TestMethod]
    public void MovementThresholdIsLiveAndIndependentOfPickupRadius()
    {
        var p = Started();
        p.Observe(Item, true, true, 60, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cast);
        p.Observe(Item, true, true, 60, 20, 70, true, 0).Should().Be(FlameDashWalkDecision.Cancelled);
    }

    [TestMethod]
    public void NoSpeculativeWaitBeforeWalkWasDispatched()
        => new FlameDashWalkWait().Observe(Item, true, true, 100, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.None);

    [TestMethod]
    public void CompletedOrChangedWalkCannotBlinkTowardStaleTarget()
    {
        foreach (var active in new PickupItemKey?[] { null, new(43, 4200), new(42, 4201) })
        {
            var p = Started();
            p.Observe(active, true, true, 100, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cancelled);
            p.Reason.Should().Be("walk-ended-or-target-changed");
        }
    }

    [TestMethod]
    public void HotkeyUiManualInputOrDisabledSettingCancelsEvenReadyFlame()
    {
        var p = Started();
        p.Observe(Item, false, true, 100, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cancelled);
        p.Reason.Should().Be("input-or-setting-gate");
    }

    [TestMethod]
    public void MissingFilteredOrOutOfRangeTargetCancelsEvenReadyFlame()
    {
        var p = Started();
        p.Observe(Item, true, false, 100, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.Cancelled);
        p.Reason.Should().Be("target-unavailable");
    }

    [TestMethod]
    public void LossOfReadinessDoesNotTurnWaitIntoLowerPriorityCast()
    {
        var p = Started();
        p.Observe(Item, true, true, 100, 20, 60, false, 0).Should().Be(FlameDashWalkDecision.Cancelled);
        p.Reason.Should().Be("flame-dash-no-longer-near-ready");
    }

    [TestMethod]
    public void ResetPreventsDeferredBlinkInNextSessionOrArea()
    {
        var p = Started(); p.Reset();
        p.Observe(Item, true, true, 100, 20, 60, true, 0).Should().Be(FlameDashWalkDecision.None);
    }

    private static FlameDashWalkWait Started() { var p = new FlameDashWalkWait(); p.Begin(Item); return p; }
}
