using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Tests.Features.Click.Interaction;

[TestClass]
public class PointDiagnosticsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void InstrumentedSearchMatchesOriginalPointsResultsAndPredicateCallsAcross2000Cases()
    {
        var rng = new Random(614);
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            var rect = new RectangleF(rng.Next(-100, 100), rng.Next(-100, 100), rng.Next(1, 100), rng.Next(1, 100));
            var preferred = new Vector2(rng.Next(-200, 200), rng.Next(-200, 200));
            var overlaps = new List<RectangleF>();
            for (int i = 0; i < iteration % 4; i++) overlaps.Add(new(rect.X + rng.Next(0, 40), rect.Y + rng.Next(0, 40), rng.Next(1, 100), rng.Next(1, 100)));
            int threshold = rng.Next(-150, 150);
            var referenceCalls = new List<Vector2>(); var actualCalls = new List<Vector2>();
            bool ReferenceAllowed(Vector2 p) { referenceCalls.Add(p); return p.X > threshold && p.Y < threshold + 100; }
            bool ActualAllowed(Vector2 p) { actualCalls.Add(p); return p.X > threshold && p.Y < threshold + 100; }
            bool expected = LegacySearch(rect, preferred, overlaps, ReferenceAllowed, out var expectedPoint);
            bool actual; Vector2 actualPoint;
            using (new ClickPointDiagnosticTrace(_ => { }))
                actual = LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, preferred, overlaps, ActualAllowed, out actualPoint);
            // Also compare the returned point outside the scope, with the same deterministic predicate.
            bool again = LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, preferred, overlaps, p => p.X > threshold && p.Y < threshold + 100, out var observed);
            actual.Should().Be(expected); actualPoint.Should().Be(expectedPoint);
            again.Should().Be(expected); observed.Should().Be(expectedPoint);
            actualCalls.Should().Equal(referenceCalls, "diagnostics must not re-run checks or change their order");
        }
    }

    [TestMethod]
    public void FailureIncludesActualLabelLayoutAllProbesAndNamedBlockingUiRectangle()
    {
        var svc = new AreaService();
        svc.ApplyBlockedSnapshot(new AreaBlockedSnapshot
        {
            FullScreenRectangle = new(0, 0, 200, 200), MapPanelBlockedRectangle = new(20, 20, 160, 160),
            LastBlockedUiRectanglesRefreshTimestampMs = 1234
        });
        object? captured = null;
        using (new ClickPointDiagnosticTrace(t => captured = new { failures = t.Failures, events = t.Events, snapshots = t.AreaSnapshots }))
        {
            var resolver = new LabelClickPointResolver(new ClickItMaxSettings());
            resolver.TryCalculateClickPosition(new(50, 50, 50, 20), EntityType.WorldItem, "CurrencyVaal", "Vaal Orb",
                default, Array.Empty<RectangleF>(), svc.PointIsInClickableArea, out _).Should().BeFalse();
        }
        string json = JsonConvert.SerializeObject(captured);
        json.Should().Contain("Vaal Orb").And.Contain("label-layout").And.Contain("no-admissible-probe")
            .And.Contain("ui-rectangle-hit").And.Contain("\"rule\":\"map\"").And.Contain("ui-area-rejected");
    }

    [TestMethod]
    public void OutOfScreenAndOverlapHaveDifferentReasonsAndBlockedProbesDoNotCallUiPredicate()
    {
        var svc = new AreaService(); svc.ApplyBlockedSnapshot(new AreaBlockedSnapshot { FullScreenRectangle = new(0, 0, 200, 200) });
        ClickPointDiagnosticTrace? trace = null;
        using (new ClickPointDiagnosticTrace(t => trace = t))
            LabelClickPointSearch.TryResolveVisibleClickablePoint(new(20, 250, 50, 20), new(45, 260), [], svc.PointIsInClickableArea, out _).Should().BeFalse();
        JsonConvert.SerializeObject(trace!.Events).Should().Contain("outside-full-screen");
        int calls = 0;
        using (new ClickPointDiagnosticTrace(t => trace = t))
            LabelClickPointSearch.TryResolveVisibleClickablePoint(new(20, 20, 50, 20), new(45, 30), [new(0, 0, 200, 200)], _ => { calls++; return true; }, out _).Should().BeFalse();
        calls.Should().Be(0); JsonConvert.SerializeObject(trace!.Events).Should().Contain("overlap-rejected");
    }

    [TestMethod]
    public void DiagnosticCallbackFailureCannotEscapeAndScopeRestoresThreadState()
    {
        using (new ClickPointDiagnosticTrace(_ => throw new IOException("test diagnostic sink")))
            ClickPointDiagnosticTrace.Reject("fixture", "failure");
        ClickPointDiagnosticTrace.Active.Should().BeFalse();
    }

    [TestMethod]
    public void UnreadableRectangleReportsItsReadFailureWithoutChangingReturnValue()
    {
        ClickPointDiagnosticTrace? trace = null;
        using (new ClickPointDiagnosticTrace(t => trace = t))
            LabelGeometry.TryGetElementRect(null, out _).Should().BeFalse();
        JsonConvert.SerializeObject(trace!.Events).Should().Contain("element-null");
        trace.HasFailure.Should().BeFalse("an unrelated overlap label read is not a terminal target failure");
    }

    [TestMethod]
    public void TraceMemoryIsBoundedButTerminalReasonsSurviveTruncation()
    {
        ClickPointDiagnosticTrace? captured = null;
        using (new ClickPointDiagnosticTrace(t => captured = t))
        {
            for (int i = 0; i < 10000; i++) ClickPointDiagnosticTrace.Probe(default, "test");
            ClickPointDiagnosticTrace.Reject("fixture", "terminal-reason");
        }
        captured!.Events.Length.Should().Be(256); captured.TruncatedEvents.Should().BeGreaterThan(0);
        captured.Failures.Should().Contain("fixture:terminal-reason");
    }

    [TestMethod]
    public void DetailedFailuresAreRateLimitedPerEntityAndGloballyWithoutDelayingSelection()
    {
        var gate = new PointDiagnosticRateGate(); var key = new PickupItemKey(1, 100);
        gate.CanCapture(key, 1000).Should().BeTrue(); gate.CanCapture(key, 1000).Should().BeTrue();
        gate.ClaimFailure(key, 1000).Should().BeTrue(); gate.CanCapture(key, 1999).Should().BeFalse();
        gate.ClaimFailure(key, 2000).Should().BeTrue();
        for (int i = 2; i <= 8; i++) gate.ClaimFailure(new((uint)i, i), 2000).Should().BeTrue();
        gate.CanCapture(new(9, 9), 2000).Should().BeFalse(); gate.CanCapture(new(9, 9), 3000).Should().BeTrue();
    }

    [TestMethod]
    public void FullQueueNeverWaitsForSlowDiskAndReportsDrops()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var sink = new BlockingWriter(entered, release);
        using var writer = new BoundedPickupLogWriter(_ => sink, capacity: 2);
        writer.TryWrite(new { first = 1 }).Should().BeTrue(); entered.Wait(2000).Should().BeTrue();
        try
        {
            var producer = Task.Run(() => new[] { writer.TryWrite(new { n = 2 }), writer.TryWrite(new { n = 3 }), writer.TryWrite(new { n = 4 }) });
            producer.Wait(500).Should().BeTrue("disk is stalled but producers never wait for it");
            producer.Result.Should().Equal(true, true, false); writer.Dropped.Should().Be(1);
        }
        finally { release.Set(); }
    }

    [TestMethod]
    public void SerializationFailuresAreIsolatedAndFollowingRecordsStillWrite()
    {
        var sink = new StringWriter(); var writer = new BoundedPickupLogWriter(_ => sink);
        writer.TryWrite(new ThrowingPayload()).Should().BeTrue(); writer.TryWrite(new { good = true }).Should().BeTrue();
        writer.Dispose(); writer.Errors.Should().Be(1); writer.Written.Should().Be(1);
        sink.ToString().Should().Contain("\"good\":true");
    }

    [TestMethod]
    public void LogRotationUsesOnlyFourOwnedPartsAndQueueDrainsAtShutdown()
    {
        var opened = new List<int>();
        var writer = new BoundedPickupLogWriter(part => { opened.Add(part); return new StringWriter(); }, rotateChars: 1);
        for (int i = 0; i < 12; i++) writer.TryWrite(new { i }).Should().BeTrue();
        writer.Dispose(); writer.Written.Should().Be(12); opened.Should().Equal(0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3);
    }

    [TestMethod]
    public void DiagnosticsOverheadMeasurement()
    {
        var svc = new AreaService(); svc.ApplyBlockedSnapshot(new AreaBlockedSnapshot { FullScreenRectangle = new(0, 0, 200, 200), MapPanelBlockedRectangle = new(0, 0, 200, 200) });
        var rect = new RectangleF(20, 20, 80, 30); var point = new Vector2(60, 35);
        void Run(bool capture)
        {
            if (capture)
            {
                using var scope = new ClickPointDiagnosticTrace(t => { if (t.HasFailure) { _ = t.Events; _ = t.AreaSnapshots; } });
                LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, point, [], svc.PointIsInClickableArea, out _);
            }
            else LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, point, [], svc.PointIsInClickableArea, out _);
        }
        for (int i = 0; i < 500; i++) { Run(false); Run(true); }
        const int count = 5000;
        var timer = Stopwatch.StartNew(); for (int i = 0; i < count; i++) Run(false); double baseline = timer.Elapsed.TotalMilliseconds / count;
        timer.Restart(); for (int i = 0; i < count; i++) Run(true); double observed = timer.Elapsed.TotalMilliseconds / count;
        TestContext.WriteLine($"Synthetic worst-case 36 rejected probes + UI snapshot: baseline={baseline:F6}ms observed={observed:F6}ms delta={observed-baseline:F6}ms; metadata SDK reads excluded, no disk/input.");
    }

    public class ThrowingPayload { public int Value => throw new InvalidOperationException("serialization fixture"); }
    private sealed class BlockingWriter(ManualResetEventSlim entered, ManualResetEventSlim release) : StringWriter
    {
        public override void WriteLine(string? value) { entered.Set(); release.Wait(3000); base.WriteLine(value); }
    }

    // Frozen pre-instrumentation search: same traversal, float math and short-circuit checks.
    private static bool LegacySearch(RectangleF r, Vector2 preferred, IReadOnlyList<RectangleF> blocked, Func<Vector2, bool>? allowed, out Vector2 result)
    {
        bool Blocked(Vector2 p) => blocked.Any(b => p.X >= b.Left && p.X <= b.Right && p.Y >= b.Top && p.Y <= b.Bottom);
        var clamped = new Vector2(System.Math.Clamp(preferred.X, r.Left, r.Right), System.Math.Clamp(preferred.Y, r.Top, r.Bottom));
        if (!Blocked(clamped) && (allowed == null || allowed(clamped))) { result = clamped; return true; }
        float dx = r.Width / 7, dy = r.Height / 5, bestDistance = float.MaxValue; Vector2 best = clamped;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 7; x++)
            {
                var p = new Vector2(r.Left + (x + 0.5f) * dx, r.Top + (y + 0.5f) * dy);
                if (Blocked(p) || allowed != null && !allowed(p)) continue;
                float distance = (p.X - clamped.X) * (p.X - clamped.X) + (p.Y - clamped.Y) * (p.Y - clamped.Y);
                if (distance < bestDistance) { bestDistance = distance; best = p; }
            }
        result = bestDistance < float.MaxValue ? best : clamped; return bestDistance < float.MaxValue;
    }
}
