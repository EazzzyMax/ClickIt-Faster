namespace ClickItMax.Features.Click.MaxPickup;

/// <summary>Independent key-up watchdog: a stalled SDK getter cannot retain our key.</summary>
internal sealed class MovementKeyLease(Action down, Action up,
    int maximumHoldMs = MovementInputHold.MaximumHoldMs) : IDisposable
{
    private readonly object _gate = new();
    private System.Threading.Timer? _timer;
    private bool _pressed, _disposed, _everPressed;
    internal bool ReleasedByTimer { get; private set; }
    internal string? ReleaseError { get; private set; }
    internal double? NativeHeldMs { get; private set; }
    private long _downAt;

    internal void Press()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MovementKeyLease));
            if (_everPressed) throw new InvalidOperationException("One keydown per movement attempt.");
            _everPressed = true;
            _pressed = true; _downAt = Stopwatch.GetTimestamp();
            down();
            _timer = new System.Threading.Timer(_ => Release(true), null, maximumHoldMs, Timeout.Infinite);
        }
    }

    internal void Release() => Release(false);
    private void Release(bool watchdog)
    {
        lock (_gate)
        {
            if (!_pressed) return;
            _pressed = false; ReleasedByTimer = watchdog;
            NativeHeldMs = Stopwatch.GetElapsedTime(_downAt).TotalMilliseconds;
            try { up(); } catch (Exception ex) { ReleaseError = ex.GetType().Name; }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Release(false);
            _timer?.Dispose();
        }
    }
}
