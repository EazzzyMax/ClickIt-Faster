namespace ClickItMax.Features.Click.MaxPickup;

internal sealed record MovementDiagnosticEvent(long AtMs, string Phase, object? Data, double ReadMs, string? ReadError);
internal sealed record MovementCastDiagnosticReport(int Version, long CastId, object Header, long StartedAt,
    long FinishedAt, string FinishReason, string? TrackerReason, bool? TrackerFailed, int DroppedSamples,
    long MaximumPeriodicGapMs, double TotalReadMs, MovementDiagnosticEvent[] Events);

/// <summary>Bounded observer only. No sleeps, input, SDK objects, retries or gameplay decisions.</summary>
internal sealed class MovementCastDiagnosticTrace(long castId, object header, long startedAt,
    Action<MovementCastDiagnosticReport> publish) : IDisposable
{
    internal const int Capacity = 128;
    internal const int SampleIntervalMs = 20;
    internal const int MaximumDurationMs = 2000;
    private readonly List<MovementDiagnosticEvent> _events = new(Capacity);
    private long? _lastPeriodic, _trackerFinishedAt;
    private string? _trackerReason;
    private bool? _trackerFailed;
    private bool _dispatched, _finished;
    private int _dropped;
    private long _maximumGap;
    private double _totalReadMs;
    internal long CastId => castId;
    internal bool Finished => _finished;

    internal void Note(long now, string phase, object? data = null)
    {
        if (_finished) return;
        if (_events.Count >= Capacity) { _dropped++; return; }
        _events.Add(new(now, phase, data, 0, null));
    }

    internal void Capture(long now, string phase, Func<object?> read)
    {
        if (_finished) return;
        if (_events.Count >= Capacity) { _dropped++; return; } // Never read SDK after the bound.
        object? data = null; string? error = null;
        long began = Stopwatch.GetTimestamp();
        try { data = read(); }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        double readMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
        _totalReadMs += readMs;
        _events.Add(new(now, phase, data, readMs, error));
    }

    internal bool SampleDue(long now)
    {
        if (_finished || _lastPeriodic is long previous && now - previous < SampleIntervalMs) return false;
        if (_lastPeriodic is long last) _maximumGap = Math.Max(_maximumGap, now - last);
        _lastPeriodic = now;
        return true;
    }

    internal void MarkDispatched(long now)
    {
        _dispatched = true;
        Note(now, "input-dispatched", new { meaning = "keybd_event invoked; no OS/game acknowledgement" });
    }

    internal void MarkTrackerFinished(long now, string reason, bool failed)
    {
        _trackerFinishedAt = now; _trackerReason = reason; _trackerFailed = failed;
        Note(now, "tracker-finished", new { reason, failed });
    }

    internal bool EndDue(long now) => !_finished && (now - startedAt >= MaximumDurationMs ||
        _trackerFinishedAt is long ended && now - ended >= (_trackerFailed == true ? 500 : 100));

    internal void Finish(long now, string reason)
    {
        if (_finished) return;
        _finished = true;
        var report = new MovementCastDiagnosticReport(1, castId, header, startedAt, now, reason,
            _trackerReason, _trackerFailed, _dropped, _maximumGap, _totalReadMs, _events.ToArray());
        try { publish(report); } catch { /* Diagnostics must never affect input or cancellation. */ }
    }

    // A rejected operation flushes immediately. A dispatched operation survives its scope
    // to observe acceptance and a short tail, including hotkey release, without sending input.
    public void Dispose() { if (!_dispatched) Finish(Environment.TickCount64, "operation-not-dispatched"); }
}
