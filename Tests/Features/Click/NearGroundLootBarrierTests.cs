using System.Runtime.CompilerServices;
using ClickItMax.Features.Click.MaxPickup;
using ClickItMax.Features.Labels.Selection;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class NearGroundLootBarrierTests
{
    private static readonly PickupItemKey A = new(1, 101), B = new(2, 102);

    [TestMethod]
    public void RecentClickCannotAllowLongDepartureWhileNearLootStillExists()
    {
        var near = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
        var far = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
        var barrier = new NearGroundLootBarrier(); barrier.Track(A);
        var motion = new PickupMotionPolicy(); motion.Observe(0, false, 0, 0);
        motion.RecordDispatch(A, 0, 0, 0, false);
        LabelOnGround? Select(long now, float farDistance)
        {
            barrier.Update(23, _ => new(true, 20)); motion.Observe(now, false, 0, 0);
            return LabelSelectionEngine.SelectNearestByDistance([far, near],
                _ => new(true, null, "Items", null, LabelCandidateRejectReason.None), (label, _) =>
                {
                    bool isNear = ReferenceEquals(label, near); float distance = isNear ? 20 : farDistance;
                    return motion.CanDispatch(isNear ? A : B, now, 100) && !barrier.BlocksDeparture(distance, 40)
                        ? distance : null;
                }, out _).SelectedCandidate;
        }
        Select(99, 73).Should().BeNull("near is cooling; far must wait instead of leaving");
        Select(99, 25).Should().BeSameAs(far, "the user only blocks departures beyond the movement threshold");
        Select(100, 73).Should().BeSameAs(near);
    }

    [TestMethod]
    public void LastRemainingGroundEntityMustDisappearBeforeDepartureIsReleased()
    {
        var barrier = new NearGroundLootBarrier(); barrier.Track(A); barrier.Track(B);
        barrier.Update(23, _ => new(true, 10)); barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Update(23, key => key == A ? new(false, null) : new(true, 10));
        barrier.Remaining.Count.Should().Be(1); barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Update(23, _ => new(false, null));
        barrier.BlocksDeparture(73, 40).Should().BeFalse(); barrier.Tracked.Should().BeEmpty();
    }

    [TestMethod]
    public void HiddenLabelDoesNotClearKnownEntityAndWaitingHasNoAutomaticTimeout()
    {
        var barrier = new NearGroundLootBarrier(); barrier.Track(A);
        // No repeated Track call: the label has gone away, but the entity remains on the ground.
        for (int tick = 0; tick < 10000; tick++) barrier.Update(23, _ => new(true, 23));
        barrier.Remaining.Should().ContainKey(A); barrier.BlocksDeparture(73, 40).Should().BeTrue();
    }

    [TestMethod]
    public void ThresholdsAreInclusiveNearAndStrictlyBeyondMovementAndFollowSettings()
    {
        var barrier = new NearGroundLootBarrier(); barrier.Track(A); barrier.Update(23, _ => new(true, 23));
        barrier.BlocksDeparture(40, 40).Should().BeFalse();
        barrier.BlocksDeparture(40.01f, 40).Should().BeTrue();
        barrier.BlocksDeparture(73, 80).Should().BeFalse();
        barrier.BlocksDeparture(20, 10).Should().BeFalse("near pickup must not block itself if the movement slider is below the near radius");
        barrier.BlocksDeparture(23, 10).Should().BeFalse();
        barrier.BlocksDeparture(23.01f, 10).Should().BeTrue();
        barrier.Update(22, _ => new(true, 23)); barrier.BlocksDeparture(73, 40).Should().BeFalse();
    }

    [TestMethod]
    public void UnknownGroundReadsNeverGrantDepartureAndRecoveryCanReleaseIt()
    {
        var barrier = new NearGroundLootBarrier(); barrier.Track(A);
        barrier.Update(23, _ => throw new IOException("read"));
        barrier.ReadUncertain.Should().BeTrue(); barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Update(23, _ => new(true, float.NaN)); barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Update(23, _ => new(false, null)); barrier.BlocksDeparture(73, 40).Should().BeFalse();
        barrier.Update(23, _ => new(false, null), scanUncertain: true);
        barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Update(23, _ => new(false, null)); barrier.BlocksDeparture(73, 40).Should().BeFalse();
    }

    [TestMethod]
    public void PlayerMovingOutsideRadiusAndAreaResetRemoveOldBlockers()
    {
        var barrier = new NearGroundLootBarrier(); barrier.Track(A); barrier.Update(23, _ => new(true, 10));
        barrier.Update(23, _ => new(true, 23.01f)); barrier.BlocksDeparture(73, 40).Should().BeFalse();
        barrier.Track(new(1, 999)); barrier.Update(23, _ => new(true, 10));
        barrier.BlocksDeparture(73, 40).Should().BeTrue();
        barrier.Reset(); barrier.Tracked.Should().BeEmpty(); barrier.BlocksDeparture(73, 40).Should().BeFalse();
    }
}
