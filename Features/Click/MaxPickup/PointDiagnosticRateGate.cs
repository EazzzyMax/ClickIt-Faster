namespace ClickItMax.Features.Click.MaxPickup;

internal sealed class PointDiagnosticRateGate
{
    private readonly Dictionary<PickupItemKey, long> _lastFailure = new();
    private long _windowStart, _lastPrune;
    private int _windowCount;
    internal bool CanCapture(PickupItemKey key, long now) =>
        (!_lastFailure.TryGetValue(key, out long last) || now - last >= 1000) &&
        (now - _windowStart >= 1000 || _windowCount < 8);

    internal bool ClaimFailure(PickupItemKey key, long now)
    {
        if (!CanCapture(key, now)) return false;
        if (now - _windowStart >= 1000) { _windowStart = now; _windowCount = 0; }
        _windowCount++; _lastFailure[key] = now;
        if (now - _lastPrune >= 1000)
        {
            _lastPrune = now;
            foreach (var expired in _lastFailure.Where(p => now - p.Value >= 60000).Select(p => p.Key).ToArray()) _lastFailure.Remove(expired);
        }
        return true;
    }
}
