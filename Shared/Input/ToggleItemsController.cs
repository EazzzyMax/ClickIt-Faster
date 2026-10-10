namespace ClickItMax.Shared.Input
{
    internal sealed class ToggleItemsController(ClickItMaxSettings settings, Action<Keys, int> keyPress, Func<long>? clock = null)
    {
        private readonly ClickItMaxSettings _settings = settings;
        private readonly Action<Keys, int> _keyPress = keyPress;
        private readonly Func<long> _clock = clock ?? (static () => Environment.TickCount64);
        private long _lastToggleItemsTimestampMs;
        private long _lastPeriodicStartMs;
        private bool _periodicSessionActive, _periodicWasEnabled;

        internal void BeginPeriodicSession()
        {
            _periodicSessionActive = true;
            _periodicWasEnabled = _settings.ToggleItems.Value;
            _lastPeriodicStartMs = _clock();
        }

        internal void EndPeriodicSession() => _periodicSessionActive = false;

        internal bool IsPeriodicToggleDue()
        {
            if (!_periodicSessionActive) return false;
            long now = _clock();
            if (!_settings.ToggleItems.Value || _settings.ToggleItemsHotkeyBinding == Keys.None)
            {
                _periodicWasEnabled = false;
                return false;
            }
            if (!_periodicWasEnabled)
            {
                _periodicWasEnabled = true;
                _lastPeriodicStartMs = now;
                return false;
            }
            int intervalMs = SystemMath.Max(100, _settings.ToggleItemsIntervalMs.Value);
            return now - _lastPeriodicStartMs >= intervalMs;
        }

        internal bool TriggerPeriodicToggleItems()
        {
            if (!IsPeriodicToggleDue()) return false;
            long now = _clock();
            _keyPress(_settings.ToggleItemsHotkeyBinding, 20);
            _keyPress(_settings.ToggleItemsHotkeyBinding, 20);
            _lastPeriodicStartMs = now; // One pair after a long gap, never a catch-up burst.
            _lastToggleItemsTimestampMs = _clock(); // The post-toggle block starts AFTER both keys.
            return true;
        }

        internal bool TriggerToggleItems()
        {
            if (!_settings.ToggleItems.Value)
                return false;


            int intervalMs = SystemMath.Max(100, _settings.ToggleItemsIntervalMs.Value);
            long now = _clock();

            if (_lastToggleItemsTimestampMs > 0)
            {
                long elapsed = now - _lastToggleItemsTimestampMs;
                if (elapsed >= 0 && elapsed < intervalMs)
                    return false;

            }

            _keyPress(_settings.ToggleItemsHotkeyBinding, 20);
            _keyPress(_settings.ToggleItemsHotkeyBinding, 20);
            _lastToggleItemsTimestampMs = now;
            return true;
        }

        internal int GetToggleItemsPostClickBlockMs()
            => SystemMath.Max(0, _settings.ToggleItemsPostToggleClickBlockMs.Value);

        internal bool IsInPostClickBlockWindow()
        {
            int blockMs = GetToggleItemsPostClickBlockMs();
            if (blockMs <= 0 || _lastToggleItemsTimestampMs <= 0)
                return false;


            long elapsed = _clock() - _lastToggleItemsTimestampMs;
            return elapsed >= 0 && elapsed < blockMs;
        }

        internal void SetLastToggleItemsTimestamp(long timestampMs)
            => _lastToggleItemsTimestampMs = timestampMs;
    }
}
