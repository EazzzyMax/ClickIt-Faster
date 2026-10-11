using System.Runtime.CompilerServices;
using ClickItMax.Features.Click.MaxPickup;
using ClickItMax.Features.Labels.Selection;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class NearestPickupSelectionTests
{
    private static LabelOnGround Label() => (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
    private static LabelCandidateBuildResult Eligible(LabelOnGround _) => new(true,
        (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity)), "Items", null, LabelCandidateRejectReason.None);

    [DataTestMethod]
    [DataRow(20f, 25f)]
    [DataRow(73f, 77f)]
    [DataRow(20f, 21f)]
    public void RepeatBoundaryReturnsCloserTargetToNormalDistanceRanking(float closeDistance, float farDistance)
    {
        var close = Label(); var far = Label();
        IReadOnlyList<LabelOnGround> labels = [far, close]; // Deliberately not distance sorted.
        var closeKey = new PickupItemKey(1, 101); var farKey = new PickupItemKey(2, 102);
        var motion = new PickupMotionPolicy(); motion.Observe(0, false, 0, 0);
        motion.RecordDispatch(closeKey, 0, 0, 0, false);
        LabelOnGround? Select(long now)
        {
            motion.Observe(now, false, 0, 0);
            return LabelSelectionEngine.SelectNearestByDistance(labels, Eligible,
                (label, _) => motion.CanDispatch(ReferenceEquals(label, close) ? closeKey : farKey, now, 100)
                    ? ReferenceEquals(label, close) ? closeDistance : farDistance : null, out _).SelectedCandidate;
        }
        Select(99).Should().BeSameAs(far);
        Select(100).Should().BeSameAs(close);
        Select(101).Should().BeSameAs(close);
    }

    [TestMethod]
    public void FailedFarApproachReturnsToCloserTargetInsteadOfNewFarTarget()
    {
        var close = Label(); var far = Label();
        var motion = new PickupMotionPolicy(); motion.Observe(0, false, 0, 0);
        var closeKey = new PickupItemKey(1, 101); var farKey = new PickupItemKey(2, 102);
        motion.RecordDispatch(closeKey, 0, 0, 0, true);
        motion.Observe(150, false, 0, 0).AllowDispatch.Should().BeTrue();
        var result = LabelSelectionEngine.SelectNearestByDistance([far, close], Eligible,
            (label, _) => motion.CanDispatch(ReferenceEquals(label, close) ? closeKey : farKey, 150, 100)
                ? ReferenceEquals(label, close) ? 73f : 77f : null, out float distance);
        result.SelectedCandidate.Should().BeSameAs(close); distance.Should().Be(73);
    }

    [TestMethod]
    public void EveryScanUsesCurrentDistanceAndDoesNotKeepPreviousWinner()
    {
        var a = Label(); var b = Label(); float aDistance = 20, bDistance = 25;
        LabelOnGround? Select() => LabelSelectionEngine.SelectNearestByDistance([a, b], Eligible,
            (label, _) => ReferenceEquals(label, a) ? aDistance : bDistance, out _).SelectedCandidate;
        Select().Should().BeSameAs(a);
        aDistance = 30; bDistance = 15;
        Select().Should().BeSameAs(b);
    }

    [TestMethod]
    public void RejectedCandidateDoesNotStopScanAndInvalidDistancesNeverWin()
    {
        var rejected = Label(); var nan = Label(); var excluded = Label(); var valid = Label();
        var result = LabelSelectionEngine.SelectNearestByDistance([rejected, nan, excluded, valid],
            label => ReferenceEquals(label, rejected)
                ? new(false, null, null, null, LabelCandidateRejectReason.OutOfDistance) : Eligible(label),
            (label, _) => ReferenceEquals(label, nan) ? float.NaN : ReferenceEquals(label, excluded) ? null : 73f,
            out float distance);
        result.SelectedCandidate.Should().BeSameAs(valid); distance.Should().Be(73);
        result.Stats.ConsideredCandidates.Should().Be(4);
    }
}
