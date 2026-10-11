using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private readonly object _maxPickupLock = new();
        private readonly PickupMotionPolicy _maxPickup = new();
        private uint? _maxArea, _maxPlayer;
        private string _maxLastState = "";
        private long _maxLastStateLog;
        private long? _maxPreviousSentAt;
        private string? _maxLogPath;
        private bool _maxToggleRefreshPending;
        private bool MaxCanTakeOverInitialMovement => _maxSessionActive && _maxSessionClicks == 0 && _maxSessionMovementCasts == 0;
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

        internal void CancelMaxPickup()
        {
            lock (_maxPickupLock)
            {
                ObserveMaxPickupSession();
                _maxPickup.Reset();
                MaxCancelFlameDashWalk("session-or-context-ended");
                _settings.MaxPickupStatus = "Пауза";
            }
        }

        private bool MaxReadPlayer(out Entity? player, out System.Numerics.Vector2 position, out bool? moving)
        {
            player = _gameController.Player;
            position = default; moving = null;
            if (player == null || !player.IsValid || player.IsDead) return false;
            // Fresh private wrappers avoid dependence on the HUD frame-cache cadence.
            var positioned = player.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
            if (positioned == null) return false;
            var freshPosition = player.GetObject<ExileCore.PoEMemory.Components.Positioned>(positioned.Address);
            position = new(freshPosition.GridX, freshPosition.GridY);
            try { moving = MaxFreshActor(player)?.isMoving; } catch { }
            return float.IsFinite(position.X) && float.IsFinite(position.Y);
        }

        private static PickupItemKey MaxKey(Entity item) => new(item.Id, item.Address);

        private static float MaxGridDistance(Entity item, System.Numerics.Vector2 playerPosition)
        {
            var component = item.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
            if (component == null || component.Address == 0) return float.NaN;
            var fresh = item.GetObject<ExileCore.PoEMemory.Components.Positioned>(component.Address);
            return System.Numerics.Vector2.Distance(new(fresh.GridX, fresh.GridY), playerPosition);
        }

        private bool MaxFreshItem(LabelOnGround label, out Entity? item)
        {
            item = label.ItemOnGround;
            return item != null && item.IsValid && item.Type == EntityType.WorldItem
                && label.IsVisible && label.CanPickUp && label.Label != null && label.Label.IsVisible;
        }

        private bool MaxActiveTargetVisible(IReadOnlyList<LabelOnGround> labels)
        {
            if (_maxPickup.ActiveItem is not { } key) return true;
            foreach (var label in labels)
            {
                try
                {
                    var item = label.ItemOnGround;
                    if (item != null && item.IsValid && MaxKey(item) == key && label.IsVisible && label.Label.IsVisible) return true;
                }
                catch { }
            }
            return false;
        }

        private void MaxLog(string kind, object data)
        {
            // Logging is diagnostic only and must never affect dispatch success.
            try
            {
                _maxLogPath ??= Path.Combine(AppContext.BaseDirectory, "Logs", "ClickItMax", "pickup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
                if (_maxLogWriter == null)
                {
                    _maxLogWriter = new BoundedPickupLogWriter(_maxLogPath);
                    _maxLogWriter.TryWrite(new { kind = "logging-config", utc = DateTime.UtcNow, ms = Environment.TickCount64,
                        data = new { version = 1, backgroundSerializationAndDisk = true, queueCapacity = 256,
                            failureDetailIntervalMs = 1000, maxDetailedFailuresPerSecond = 8, maxTraceEvents = 256,
                            rotateAfterChars = 16 * 1024 * 1024, retainedPartsPerRun = 4 } });
                }
                _maxLogWriter.TryWrite(new { kind, utc = DateTime.UtcNow, ms = Environment.TickCount64,
                    session = _maxSessionActive ? (int?)_maxSessionNumber : null,
                    logging = new { dropped = _maxLogWriter.Dropped, errors = _maxLogWriter.Errors }, data });
            }
            catch { }
        }

        private void RunMaxPickupTick()
        {
            lock (_maxPickupLock)
            {
                try { RunMaxPickupTickCore(); }
                catch (Exception ex)
                {
                    ClickPointDiagnosticTrace.Reject("movement", ex.GetType().Name + ": " + ex.Message);
                    _maxPickup.Reset();
                    MaxCancelFlameDashWalk("read-error");
                    _settings.MaxPickupStatus = "Ошибка чтения — пауза";
                    long now = Environment.TickCount64;
                    if (now - _maxLastStateLog > 1000)
                    {
                        MaxLog("error", new { type = ex.GetType().Name, ex.Message });
                        _maxLastStateLog = now;
                    }
                }
            }
        }

        private void RunMaxPickupTickCore()
        {
            long tickStarted = Stopwatch.GetTimestamp();
            ObserveMaxPickupSession();
            if (!_settings.Enable.Value || !_inputHandler.IsClickHotkeyActiveForCurrentInputState()
                || !_gameController.Window.IsForeground() || (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0)
            {
                CancelMaxPickup();
                return;
            }
            if (!MaxReadPlayer(out var player, out var position, out var moving))
            {
                CancelMaxPickup();
                return;
            }
            uint area = _gameController.IngameState.Data.CurrentAreaHash;
            if (_maxArea != area || _maxPlayer != player!.Id)
            {
                _maxPickup.ResetForArea();
                _maxTravel.ResetForArea();
                _maxMovementAttempts.Clear();
                MaxCancelFlameDashWalk("area-player-changed");
                _maxArea = area; _maxPlayer = player!.Id;
                _labelSelectionService.ResetCaches();
                _labelClickPointResolver.InvalidateLayout();
                _cachedLabels.ForceUpdate();
                MaxLog("area-reset", new { area, player = player.Id });
            }
            // Owned casts are observed before label refresh or normal walk/selection policy.
            // Keeping them across hotkey release also prevents a delayed Warp being queued twice.
            bool movementInProgress = MaxObserveTravel(player!, position);
            // Periodic view refresh runs even while approaching a far item or when
            // no candidate is available, but only inside the existing input/UI gates.
            if (_inputHandler.IsPeriodicToggleItemsDue() && _inputHandler.CanClick(_gameController, false, false)
                && _inputHandler.TriggerPeriodicToggleItems())
            {
                _maxToggleRefreshPending = true;
                MaxLog("toggle-items", new { key = _settings.ToggleItemsHotkeyBinding.ToString(),
                    intervalMs = _settings.ToggleItemsIntervalMs.Value, sessionElapsedMs = Environment.TickCount64 - _maxSessionStarted,
                    elapsedMs = Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds, target = _maxPickup.ActiveItem,
                    postBlockMs = _settings.ToggleItemsPostToggleClickBlockMs.Value });
                return; // Don't click a label in the same tick that moved its UI.
            }
            if (movementInProgress) return;
            if (_maxToggleRefreshPending)
            {
                _labelSelectionService.ResetCaches();
                _labelClickPointResolver.InvalidateLayout();
                _cachedLabels.ForceUpdate();
                _maxToggleRefreshPending = false;
            }
            var labels = GetLabelsForRegularSelection();
            if (labels == null) { CancelMaxPickup(); return; }
            long now = Environment.TickCount64;
            var observation = _maxPickup.Observe(now, moving, position.X, position.Y, MaxActiveTargetVisible(labels),
                allowInitialMovementTakeover: MaxCanTakeOverInitialMovement);
            _settings.MaxPickupStatus = observation.State.ToString();
            if (_maxLastState != _settings.MaxPickupStatus)
            {
                _maxLastState = _settings.MaxPickupStatus;
                MaxLog("motion", new { state = _maxLastState, moving, x = position.X, y = position.Y, target = _maxPickup.ActiveItem });
            }
            if (MaxTryResumeFlameDashWalk(labels, player!, position)) return;
            if (!observation.AllowDispatch) { MaxSkip("motion:" + observation.State); return; }

            // Selection stays live. No queue of screen coordinates or inventory acknowledgements.
            LabelOnGround? selected = null;
            bool selectedFar = false;
            long selectionStarted = Stopwatch.GetTimestamp();
            int[] rejected = new int[8];
            bool RejectCandidate(int reason) { rejected[reason]++; return false; }
            for (int pass = 0; pass < 4 && selected == null; pass++)
            {
                bool far = pass >= 2;
                bool unattemptedOnly = pass % 2 == 0;
                selected = _labelSelectionService.GetNextLabelToClick(labels, 0, labels.Count, (label, candidate) =>
                {
                    try
                    {
                        if (!MaxFreshItem(label, out var item)) return RejectCandidate(0);
                        string? mechanic = candidate.MechanicId ?? _labelInteractionPort.GetMechanicIdForLabel(label);
                        if (!string.Equals(mechanic, MechanicIds.Items, StringComparison.Ordinal)) return RejectCandidate(1);
                        var key = MaxKey(item!);
                        if (!_maxPickup.CanDispatch(key, Environment.TickCount64, _settings.MaxPickupRepeatDelayMs.Value)) return RejectCandidate(2);
                        if (unattemptedOnly && !_maxPickup.IsUnattempted(key)) return RejectCandidate(3);
                        float distance = MaxGridDistance(item!, position);
                        if (!float.IsFinite(distance) || distance > _settings.ClickDistance.Value) return RejectCandidate(4);
                        if ((distance > _settings.MaxPickupNearRadius.Value) != far) return RejectCandidate(5);
                        if (_labelClickPointResolver.IsLabelFullyOverlapped(label, labels)) return RejectCandidate(6);
                        return true;
                    }
                    catch { return RejectCandidate(7); }
                });
                if (selected != null) selectedFar = far;
            }
            if (selected == null)
            {
                MaxSkip("no-candidate", new { labels = labels.Count, predicateEvaluationsNotUniqueItems = rejected.Sum(), rejectedNotClickable = rejected[0], rejectedMechanic = rejected[1], rejectedCooldown = rejected[2], rejectedPreviouslyAttempted = rejected[3], rejectedRange = rejected[4], rejectedNearFarPass = rejected[5], rejectedOverlap = rejected[6], readErrors = rejected[7] });
                return;
            }
            double selectionMs = Stopwatch.GetElapsedTime(selectionStarted).TotalMilliseconds;
            if (!MaxFreshItem(selected, out var target)) { MaxSkip("target-disappeared-before-click"); return; }
            var targetKey = MaxKey(target!);
            using var pointDiagnostics = MaxBeginPointDiagnostics(selected, target!, position);
            int flameWaitMs = 0;
            if (selectedFar)
            {
                try { if (MaxTryCastMovement(selected, target!, player!, out flameWaitMs)) return; }
                catch (Exception ex)
                {
                    MaxLog("movement-error", new { ex.Message, type = ex.GetType().Name, inputAlreadySent = _maxTravel.Active });
                    if (_maxTravel.Active) return; // A diagnostic read after keydown cannot authorize a second input.
                }
            }
            var window = _gameController.Window.GetWindowRectangleTimeCache;
            var topLeft = new Vector2(window.X, window.Y);
            long resolveStarted = Stopwatch.GetTimestamp();
            if (!LabelInteraction.TryResolveLabelClickPosition(selected, MechanicIds.Items, topLeft, labels, out var clickPosition))
            { ClickPointDiagnosticTrace.Reject("label-resolution", "click-point-unresolved"); MaxSkip("click-point-unresolved", new { target = targetKey }); return; }
            double resolveMs = Stopwatch.GetElapsedTime(resolveStarted).TotalMilliseconds;

            // Recheck after hover preparation, immediately before sending actual mouse input.
            bool verifyUiHover = selectedFar ? _settings.MaxPickupVerifyFarUiHover.Value : _settings.MaxPickupVerifyNearUiHover.Value;
            string? finalReject = null;
            bool RejectFinal(string reason) { finalReject = reason; ClickPointDiagnosticTrace.Reject("before-dispatch", reason); return false; }
            _interactionExecutor.BeforeDispatch = () =>
            {
                if (!_settings.Enable.Value || !_settings.MaxPickupEnabled.Value) return RejectFinal("disabled");
                if (!_inputHandler.IsClickHotkeyActiveForCurrentInputState()) return RejectFinal("hotkey-released");
                if (!_gameController.Window.IsForeground()) return RejectFinal("foreground-lost");
                if ((GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0) return RejectFinal("manual-mouse-held");
                if (!_inputHandler.CanClick(_gameController, false, false)) return RejectFinal("input-ui-gate");
                if (!MaxReadPlayer(out var freshPlayer, out var fresh, out var freshMoving)) return RejectFinal("player-unreadable");
                if (freshPlayer!.Id != _maxPlayer || _gameController.IngameState.Data.CurrentAreaHash != _maxArea) return RejectFinal("area-player-changed");
                if (!MaxFreshItem(selected, out var freshTarget) || MaxKey(freshTarget!) != targetKey) return RejectFinal("target-changed");
                position = fresh;
                var check = _maxPickup.Observe(Environment.TickCount64, freshMoving, fresh.X, fresh.Y, MaxActiveTargetVisible(labels),
                    allowInitialMovementTakeover: MaxCanTakeOverInitialMovement);
                float distance = MaxGridDistance(freshTarget!, fresh);
                if (verifyUiHover)
                {
                    long expected = selected.Label.Address;
                    long hover = _gameController.IngameState.UIHoverElement?.Address ?? 0;
                    if (expected == 0 || hover != expected) return RejectFinal("hover-mismatch");
                }
                if (!check.AllowDispatch) return RejectFinal("motion:" + check.State);
                if (!float.IsFinite(distance) || distance > _settings.ClickDistance.Value) return RejectFinal("range-changed");
                if ((distance > _settings.MaxPickupNearRadius.Value) != selectedFar) return RejectFinal("near-far-changed");
                if (!_maxPickup.CanDispatch(targetKey, Environment.TickCount64, _settings.MaxPickupRepeatDelayMs.Value)) return RejectFinal("cooldown");
                return true;
            };
            bool sent;
            long executorStarted = Stopwatch.GetTimestamp();
            try { sent = LabelInteraction.PerformResolvedLabelInteraction(clickPosition, selected, MechanicIds.Items,
                forceUiHoverVerification: verifyUiHover, skipUiHoverVerification: !verifyUiHover); }
            finally { _interactionExecutor.BeforeDispatch = null; }
            if (!sent)
            {
                ClickPointDiagnosticTrace.Outcome("pickup-input", "not-sent");
                try
                {
                    MaxSkip("click-rejected:" + (finalReject ?? "executor-preparation"), new { target = targetKey, far = selectedFar, verifyUiHover,
                        expected = selected.Label.Address, hover = verifyUiHover ? _gameController.IngameState.UIHoverElement?.Address : null, clickX = clickPosition.X, clickY = clickPosition.Y });
                }
                catch { MaxSkip("click-rejected:" + (finalReject ?? "executor-preparation")); }
                return;
            }
            long sentAt = Environment.TickCount64 - _performanceMonitor.GetElapsedSinceLastClickMs();
            ClickPointDiagnosticTrace.Outcome("pickup-input", "sent");
            bool tookOverMovement = _maxPickup.State == PickupMotionState.TakingOverMovement;
            _maxPickup.RecordDispatch(targetKey, sentAt, position.X, position.Y, selectedFar);
            if (selectedFar) MaxBeginFlameDashWalk(targetKey, flameWaitMs);
            _maxSessionClicks++;
            _labelClickPointResolver.InvalidateLayout();
            long? previousSentAt = _maxPreviousSentAt; _maxPreviousSentAt = sentAt;
            MaxLog("click-sent", new { target = targetKey, far = selectedFar, tookOverMovement, verifyUiHover, distance = MaxGridDistance(target!, position),
                clickX = clickPosition.X, clickY = clickPosition.Y, sentAt, repeatMs = _settings.MaxPickupRepeatDelayMs.Value,
                previousClickGapMs = previousSentAt is long prev ? sentAt - prev : (long?)null, selectionMs, resolveMs,
                executorMs = Stopwatch.GetElapsedTime(executorStarted).TotalMilliseconds, tickMs = Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds });
        }
    }
}
