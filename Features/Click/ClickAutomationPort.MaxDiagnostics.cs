namespace ClickItMax.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private bool _maxSessionActive;
        private int _maxSessionNumber, _maxSessionClicks;
        private long _maxSessionStarted, _maxLastKeySample, _maxLastSkipFlush;
        private string _maxSessionKey = "";
        private readonly Dictionary<string, int> _maxSkips = new();

        // Called from Render and before pickup selection. Never block the render thread on a click.
        internal void ObserveMaxPickupSession()
        {
            if (!Monitor.TryEnter(_maxPickupLock)) return;
            try
            {
                long now = Environment.TickCount64;
                string key = _settings.ClickLabelKeyBinding.ToString();
                bool rawDown = (GetAsyncKeyState((int)_settings.ClickLabelKeyBinding) & 0x8000) != 0;
                bool foreground = _gameController.Window?.IsForeground() == true;
                bool enabled = _settings.Enable.Value && _settings.MaxPickupEnabled.Value;
                bool active = enabled && foreground && _inputHandler.IsClickHotkeyActiveForCurrentInputState();
                if (_maxSessionActive && (!active || key != _maxSessionKey))
                    EndMaxPickupSession(!enabled ? "disabled" : !foreground ? "foreground-lost" : key != _maxSessionKey ? "key-changed" : "hotkey-released", now, rawDown);
                if (active && !_maxSessionActive)
                {
                    _maxSessionActive = true; _maxSessionNumber++; _maxSessionStarted = now; _maxSessionClicks = 0; _maxSessionKey = key;
                    _inputHandler.BeginPeriodicToggleItemsSession();
                    _maxLastState = ""; // Each activation must log its initial motion state even if unchanged.
                    _maxSkips.Clear(); _maxLastSkipFlush = now;
                    MaxLog("session-start", new { session = _maxSessionNumber, key, rawDown, foreground,
                        sampling = "observed key state, not native keyboard-hook timestamp", previousSampleGapMs = _maxLastKeySample == 0 ? (long?)null : now - _maxLastKeySample,
                        repeatMs = _settings.MaxPickupRepeatDelayMs.Value, nearRadius = _settings.MaxPickupNearRadius.Value, frequencyMs = _settings.ClickFrequencyTarget.Value,
                        startTimeoutMs = _maxPickup.StartTimeoutMs, globalBackoffMs = 0, positionSettleMs = _maxPickup.PositionSettleMs,
                        verifyNearUiHover = _settings.MaxPickupVerifyNearUiHover.Value, verifyFarUiHover = _settings.MaxPickupVerifyFarUiHover.Value,
                        toggleItems = _settings.ToggleItems.Value, toggleItemsIntervalMs = _settings.ToggleItemsIntervalMs.Value,
                        toggleItemsKey = _settings.ToggleItemsHotkeyBinding.ToString(), toggleItemsPostBlockMs = _settings.ToggleItemsPostToggleClickBlockMs.Value,
                        initialMovementTakeover = "until first actual dispatch in this hotkey session" });
                }
                if (_maxSessionActive && _maxLastKeySample != 0 && now - _maxLastKeySample > 250)
                    MaxLog("key-observation-gap", new { gapMs = now - _maxLastKeySample, key, rawDown, foreground });
                _maxLastKeySample = now;
            }
            catch { }
            finally { Monitor.Exit(_maxPickupLock); }
        }

        internal void EndMaxPickupSession(string reason, long? at = null, bool? rawDown = null)
        {
            lock (_maxPickupLock)
            {
                if (!_maxSessionActive) return;
                long now = at ?? Environment.TickCount64;
                FlushMaxSkips();
                MaxLog("session-end", new { session = _maxSessionNumber, key = _maxSessionKey, reason, rawDown, durationMs = now - _maxSessionStarted, clicks = _maxSessionClicks,
                    previousSampleGapMs = _maxLastKeySample == 0 ? (long?)null : now - _maxLastKeySample });
                _maxSessionActive = false;
                _inputHandler.EndPeriodicToggleItemsSession();
            }
        }

        internal void NoteMaxPickupGate(string reason)
        {
            if (!_settings.MaxPickupEnabled.Value || !Monitor.TryEnter(_maxPickupLock)) return;
            try { MaxSkip("outer-gate:" + reason); }
            finally { Monitor.Exit(_maxPickupLock); }
        }

        private void MaxSkip(string reason, object? detail = null)
        {
            if (!_maxSessionActive) return;
            _maxSkips.TryGetValue(reason, out int count); _maxSkips[reason] = count + 1;
            long now = Environment.TickCount64;
            if (_maxSessionActive && now - _maxLastSkipFlush >= 500) FlushMaxSkips(detail);
        }

        private void FlushMaxSkips(object? detail = null)
        {
            if (_maxSkips.Count == 0) return;
            MaxLog("skip-summary", new { counts = new Dictionary<string, int>(_maxSkips), detail, motionState = _maxPickup.State.ToString(), activeTarget = _maxPickup.ActiveItem });
            _maxSkips.Clear(); _maxLastSkipFlush = Environment.TickCount64;
        }
    }
}
