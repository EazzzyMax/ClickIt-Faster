using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Features.Click;

public sealed partial class ClickAutomationPort
{
    private readonly PointDiagnosticRateGate _maxPointDiagnosticGate = new();
    private BoundedPickupLogWriter? _maxLogWriter;

    internal void StopMaxPickupLogging()
    {
        lock (_maxPickupLock)
        {
            _maxMovementDiagnostic?.Finish(Environment.TickCount64, "plugin-stopping");
            _maxLogWriter?.Dispose();
        }
    }

    private ClickPointDiagnosticTrace? MaxBeginPointDiagnostics(LabelOnGround label, Entity target, System.Numerics.Vector2 playerPosition)
    {
        try
        {
            var key = MaxKey(target);
            if (!_maxPointDiagnosticGate.CanCapture(key, Environment.TickCount64)) return null;
            var window = _gameController.Window.GetWindowRectangleTimeCache;
            long startedAt = Environment.TickCount64;
            return new ClickPointDiagnosticTrace(trace =>
            {
                if (!trace.HasFailure || !_maxPointDiagnosticGate.ClaimFailure(key, Environment.TickCount64)) return;
                long freezeStarted = Stopwatch.GetTimestamp();
                // Supplementary SDK fields are read only on a sampled failure. The actual decision
                // rectangle/probes/UI snapshots were already captured by the observer, not re-evaluated.
                string? name = null, path = null, readError = null;
                object? itemGrid = null, itemWorld = null, supplementaryRect = null;
                long? labelElement = null;
                try
                {
                    var actual = target.GetComponent<ExileCore.PoEMemory.Components.WorldItem>()?.ItemEntity;
                    name = actual?.RenderName; path = actual?.Path;
                    var grid = target.GridPosNum; var world = target.PosNum;
                    itemGrid = new { x = grid.X, y = grid.Y }; itemWorld = new { x = world.X, y = world.Y, z = world.Z };
                    labelElement = label.Label?.Address;
                    if (label.Label != null) supplementaryRect = ClickPointDiagnosticTrace.Rect(label.Label.GetClientRect());
                }
                catch (Exception ex) { readError = ex.GetType().Name + ": " + ex.Message; }
                var events = trace.Events; var snapshots = trace.AreaSnapshots;
                double extraReadAndFreezeMs = Stopwatch.GetElapsedTime(freezeStarted).TotalMilliseconds;
                MaxLog("point-diagnostic", new
                {
                    version = 1, target = key, name, path, labelAddress = label.Address, labelElement,
                    startedAt, operationElapsedMs = trace.CaptureMs, extraReadAndFreezeMs,
                    playerGridAtStart = new { x = playerPosition.X, y = playerPosition.Y }, itemGrid, itemWorld,
                    window = ClickPointDiagnosticTrace.Rect(window), failures = trace.Failures, events, uiSnapshots = snapshots,
                    trace.TruncatedEvents, supplementary = new { phase = "after-decision; not used to make the decision", labelRect = supplementaryRect, readError }
                });
            });
        }
        catch { return null; }
    }
}
