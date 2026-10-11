using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class MovementInputHoldTests
{
    private static readonly MovementUseState Ready = new(false, false, 3, 1);
    private sealed class Rig
    {
        internal double Now;
        internal int Downs, Ups, Reads;
        internal readonly List<MovementHoldEvent> Events = [];
        internal Func<bool> Allowed = () => true;
        internal Func<MovementUseState?>? Read;
        internal MovementHoldResult Run() => MovementInputHold.Run(Ready, Allowed,
            () => { Reads++; return Read?.Invoke() ?? Ready; }, () => Downs++, () => Ups++,
            () => Now, n => Now += n, Events.Add);
    }

    [DataTestMethod]
    [DataRow(5)]
    [DataRow(45)]
    [DataRow(75)]
    public void ReleasesAtObservedStartWithoutAnotherKeydown(int acceptedAt)
    {
        var rig = new Rig(); rig.Read = () => rig.Now >= acceptedAt ? Ready with { OwnAction = true, IsUsing = true, Stage = 2 } : Ready;
        var result = rig.Run();
        result.Accepted.Should().BeTrue(); result.HeldMs.Should().Be(acceptedAt);
        rig.Downs.Should().Be(1); rig.Ups.Should().Be(1);
        result.Reason.Should().Be("use-started");
        result.AcceptedBy.Should().Be("own-action");
    }

    [TestMethod]
    public void MissingStartHasHardDeadlineAndSingleRelease()
    {
        var rig = new Rig(); var result = rig.Run();
        result.Accepted.Should().BeFalse(); result.HeldMs.Should().Be(80); result.Reason.Should().Be("timeout");
        rig.Downs.Should().Be(1); rig.Ups.Should().Be(1); rig.Reads.Should().Be(16);
    }

    [TestMethod]
    public void ContextLossReleasesAndStillReportsInputSentSoNoSecondInputIsAuthorized()
    {
        var rig = new Rig(); rig.Allowed = () => rig.Now < 25;
        var result = rig.Run();
        result.InputSent.Should().BeTrue(); result.Accepted.Should().BeFalse(); result.HeldMs.Should().Be(25);
        result.Reason.Should().Be("context-ended"); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void DisallowedContextSendsNothing()
    {
        var rig = new Rig { Allowed = () => false }; var result = rig.Run();
        result.InputSent.Should().BeFalse(); rig.Downs.Should().Be(0); rig.Ups.Should().Be(0);
    }

    [TestMethod]
    public void PreexistingUseOrStageAloneCannotBecomeAcknowledgement()
    {
        MovementInputHold.Started(new(true, true, 3, 2), new(true, true, 3, 2)).Should().BeFalse();
        MovementInputHold.Started(Ready, Ready with { Stage = 2 }).Should().BeFalse();
        MovementInputHold.Started(new(null, null, null, null), new(true, true, 3, 2)).Should().BeFalse();
        MovementInputHold.Started(Ready, Ready with { OwnAction = true }).Should().BeTrue();
        MovementInputHold.Started(Ready, Ready with { Charges = 2 }).Should().BeTrue();
    }

    [TestMethod]
    public void TransientIsUsingAndStageThreeLikeFailedFdCannotReleaseEarly()
    {
        var rig = new Rig();
        rig.Read = () => rig.Now is >= 50 and < 60 ? Ready with { IsUsing = true, Stage = 3 } : Ready;
        var result = rig.Run();
        result.Accepted.Should().BeFalse(); result.AcceptedBy.Should().BeNull();
        result.HeldMs.Should().Be(80); result.Reason.Should().Be("timeout");
        rig.Downs.Should().Be(1); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void IsUsingDoesNotReleaseWhileWaitingForActualOwnAction()
    {
        var rig = new Rig();
        rig.Read = () => Ready with { IsUsing = rig.Now >= 5, OwnAction = rig.Now >= 65, Charges = null, Stage = 3 };
        var result = rig.Run();
        result.HeldMs.Should().Be(65); result.AcceptedBy.Should().Be("own-action");
        rig.Downs.Should().Be(1); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void ChargeSpendAcknowledgesAnInstantSkillWithoutActionSample()
    {
        var rig = new Rig();
        rig.Read = () => Ready with { OwnAction = null, Charges = rig.Now >= 35 ? 2 : 3 };
        var result = rig.Run();
        result.HeldMs.Should().Be(35); result.AcceptedBy.Should().Be("charge-spent");
        result.Accepted.Should().BeTrue(); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void NonChargeSkillCanAcknowledgeOwnActionDespiteOptionalGetterFault()
    {
        double now = 0; int ups = 0;
        var before = new MovementUseState(null, false, null, null);
        var result = MovementInputHold.Run(before, () => true,
            () => new(null, now >= 25, null, null, "charges:IOException"),
            () => { }, () => ups++, () => now, n => now += n);
        result.AcceptedBy.Should().Be("own-action"); result.HeldMs.Should().Be(25);
        result.ReadErrors.Should().BeGreaterThan(0); ups.Should().Be(1);
    }

    [TestMethod]
    public void StrongEvidenceReturnedAfterDeadlineDoesNotExtendHold()
    {
        var rig = new Rig(); rig.Read = () => { rig.Now += 81; return Ready with { OwnAction = true }; };
        var result = rig.Run();
        result.Accepted.Should().BeFalse(); result.Reason.Should().Be("timeout");
        rig.Reads.Should().Be(1); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void ReadFaultsCannotHoldForeverAndRemainUnknown()
    {
        var rig = new Rig { Read = () => throw new IOException("read") }; var result = rig.Run();
        result.Accepted.Should().BeFalse(); result.ReadErrors.Should().Be(16); rig.Ups.Should().Be(1);
        rig.Events.Where(e => e.Phase == "hold-observation").Should().OnlyContain(e => e.State == null && e.Error != null);
    }

    [TestMethod]
    public void SlowReadTimeCountsTowardDeadlineWithoutAnotherSleep()
    {
        var rig = new Rig(); rig.Read = () => { rig.Now += 81; return Ready; };
        var result = rig.Run(); result.HeldMs.Should().Be(81); rig.Reads.Should().Be(1); rig.Ups.Should().Be(1);
    }

    [TestMethod]
    public void ExceptionAfterKeydownStillReleasesAndDiagnosticFaultCannotChangeResult()
    {
        double now = 0; int ups = 0, contexts = 0;
        var result = MovementInputHold.Run(Ready, () => ++contexts == 1 ? true : throw new IOException("context"),
            () => Ready, () => { }, () => ups++, () => now, n => now += n, _ => throw new IOException("log"));
        result.InputSent.Should().BeTrue(); result.Reason.Should().Be("input-or-context-error"); ups.Should().Be(1);
    }

    [TestMethod]
    public void ThrowingKeydownStillGetsCleanupAndBrokenClockHasFinitePollCount()
    {
        int ups = 0;
        var failed = MovementInputHold.Run(Ready, () => true, () => Ready,
            () => throw new IOException("down"), () => ups++, () => 0, _ => { });
        failed.InputSent.Should().BeTrue(); ups.Should().Be(1);
        int reads = 0;
        var stalled = MovementInputHold.Run(Ready, () => true, () => { reads++; return Ready; },
            () => { }, () => ups++, () => 0, _ => { });
        stalled.Reason.Should().Be("poll-limit"); reads.Should().Be(17); ups.Should().Be(2);
    }

    [TestMethod]
    public void IndependentWatchdogReleasesWithoutCallerOrSdkProgressAndNeverTwice()
    {
        using var released = new ManualResetEventSlim(); int ups = 0;
        using var lease = new MovementKeyLease(() => { }, () => { Interlocked.Increment(ref ups); released.Set(); }, 25);
        lease.Press();
        released.Wait(2000).Should().BeTrue("the caller does not poll, return or release the key");
        lease.Release(); lease.Dispose();
        lease.ReleasedByTimer.Should().BeTrue(); ups.Should().Be(1); lease.ReleaseError.Should().BeNull();
    }

    [TestMethod]
    public void EarlyReleaseAndDisposalCannotDoubleReleaseOrSendAnotherKeydown()
    {
        int downs = 0, ups = 0;
        using var lease = new MovementKeyLease(() => downs++, () => ups++);
        lease.Press(); lease.Release(); lease.Release();
        Action repeat = lease.Press; repeat.Should().Throw<InvalidOperationException>();
        lease.Dispose();
        downs.Should().Be(1); ups.Should().Be(1); lease.ReleasedByTimer.Should().BeFalse();
    }
}
