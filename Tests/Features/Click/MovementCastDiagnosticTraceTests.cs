using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Tests.Features.Click;

[TestClass]
public class MovementCastDiagnosticTraceTests
{
    [TestMethod]
    public void FullBufferNeverCallsExtraSdkReadersAndReportsLostSamples()
    {
        MovementCastDiagnosticReport? report = null;
        var trace = new MovementCastDiagnosticTrace(1, new { skill = "FlameDash" }, 100, r => report = r);
        for (int i = 0; i < MovementCastDiagnosticTrace.Capacity; i++) trace.Note(100 + i, "event", i);
        int reads = 0;
        trace.Capture(300, "extra", () => { reads++; return 1; });
        trace.Finish(400, "done");
        reads.Should().Be(0); report!.Events.Should().HaveCount(128); report.DroppedSamples.Should().Be(1);
    }

    [TestMethod]
    public void ReadAndPublishFaultsAreIsolatedAndFailedReadsRemainUnknown()
    {
        MovementCastDiagnosticReport? report = null;
        var trace = new MovementCastDiagnosticTrace(2, new { }, 0, r => { report = r; throw new IOException("writer"); });
        trace.Capture(20, "state", () => throw new InvalidOperationException("unreadable"));
        Action finish = () => trace.Finish(30, "done"); finish.Should().NotThrow();
        report!.Events.Single().Data.Should().BeNull(); report.Events.Single().ReadError.Should().Contain("unreadable");
    }

    [TestMethod]
    public void PeriodicCaptureIsBoundedAndObservationGapsAreExplicit()
    {
        MovementCastDiagnosticReport? report = null;
        var trace = new MovementCastDiagnosticTrace(3, new { }, 0, r => report = r);
        trace.SampleDue(0).Should().BeTrue(); trace.SampleDue(19).Should().BeFalse();
        trace.SampleDue(20).Should().BeTrue(); trace.SampleDue(200).Should().BeTrue();
        trace.EndDue(1999).Should().BeFalse(); trace.EndDue(2000).Should().BeTrue();
        trace.Finish(2000, "deadline"); report!.MaximumPeriodicGapMs.Should().Be(180);
    }

    [DataTestMethod]
    [DataRow(false, 100)]
    [DataRow(true, 500)]
    public void DispatchedTraceSurvivesScopeAndTracksAcceptanceTail(bool failed, int tailMs)
    {
        MovementCastDiagnosticReport? report = null;
        var trace = new MovementCastDiagnosticTrace(4, new { }, 0, r => report = r);
        trace.MarkDispatched(10); trace.Dispose(); report.Should().BeNull();
        trace.MarkTrackerFinished(800, failed ? "cast-not-observed" : "teleported", failed);
        trace.EndDue(800 + tailMs - 1).Should().BeFalse(); trace.EndDue(800 + tailMs).Should().BeTrue();
        trace.Finish(800 + tailMs, "tail"); report!.TrackerFailed.Should().Be(failed);
    }

    [TestMethod]
    public void CancellationFlushesOnceAndFreezesEventsBeforeAsyncSerialization()
    {
        int publishes = 0; MovementCastDiagnosticReport? report = null;
        var trace = new MovementCastDiagnosticTrace(5, new { }, 0, r => { publishes++; report = r; });
        trace.Note(1, "aim"); trace.Dispose(); trace.Dispose(); trace.Note(2, "late");
        trace.Capture(3, "late-read", () => throw new Exception("must not read"));
        publishes.Should().Be(1); report!.FinishReason.Should().Be("operation-not-dispatched");
        report.Events.Should().ContainSingle(); trace.Finished.Should().BeTrue();
    }
}
