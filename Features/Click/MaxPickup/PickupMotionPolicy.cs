using System;
using System.Collections.Generic;

namespace ClickItMax.Features.Click.MaxPickup;

/// <summary>Area lifetime belongs to the adapter: call ResetForArea when it changes.</summary>
public readonly record struct PickupItemKey(uint EntityId, long Address);

public enum PickupMotionState
{
    Idle,
    AwaitingMovement,
    Approaching,
    MovementPause,
    UnknownPause,
    BlockedPause,
    TakingOverMovement
}

public readonly record struct PickupMotionObservation(bool AllowDispatch, PickupMotionState State);

/// <summary>
/// Pure dispatch policy. All times are monotonic milliseconds and positions are world/grid coordinates.
/// Observe immediately before selecting/sending input; record only input that actually succeeded.
/// No inventory acknowledgement, queued clicks, or inference about which item caused unexpected motion.
/// </summary>
public sealed class PickupMotionPolicy
{
    // 66 observed automatic starts: median 42 ms, p95 88 ms, maximum 123 ms.
    // This bounds an unobserved start, not a walk that has already begun.
    public int StartTimeoutMs { get; init; } = 150;
    public int PositionSettleMs { get; init; } = 100;
    public int NoProgressTimeoutMs { get; init; } = 1500;
    public int MaxApproachMs { get; init; } = 15000;
    public double PositionEpsilon { get; init; } = 0.25;

    private readonly Dictionary<PickupItemKey, long> _lastDispatch = new();
    private PickupItemKey? _activeItem;
    private long _approachStarted;
    private bool _approachMoved;
    private bool _hasPosition;
    private double _progressX;
    private double _progressY;
    private long _lastProgress;
    private long _positionMotionUntil;
    private bool? _previousMoving;
    private bool _hasObservation;
    private bool _allowDispatch;
    private long _lastPrune;
    private const int CooldownRetentionMs = 60000;

    public PickupItemKey? ActiveItem => _activeItem;
    public PickupMotionState State { get; private set; } = PickupMotionState.UnknownPause;

    public bool IsUnattempted(PickupItemKey key) => !_lastDispatch.ContainsKey(key);

    public bool CanDispatch(PickupItemKey key, long now, int cooldownMs = 800) =>
        _hasObservation && _allowDispatch && _activeItem == null &&
        (!_lastDispatch.TryGetValue(key, out var last) || now - last >= Math.Max(0, cooldownMs));

    /// <param name="targetVisible">Visibility of ActiveItem only; ignored if there is no active approach.</param>
    public PickupMotionObservation Observe(long now, bool? moving, double x, double y, bool targetVisible = true,
        bool allowInitialMovementTakeover = false)
    {
        PruneCooldowns(now);
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            _hasObservation = true;
            return Finish(PickupMotionState.UnknownPause);
        }

        var changed = false;
        if (!_hasPosition)
        {
            _hasPosition = true;
            _progressX = x;
            _progressY = y;
            _lastProgress = now;
        }
        else
        {
            var dx = x - _progressX;
            var dy = y - _progressY;
            changed = dx * dx + dy * dy >= PositionEpsilon * PositionEpsilon;
            if (changed)
            {
                _progressX = x;
                _progressY = y;
                _lastProgress = now;
                _positionMotionUntil = now + PositionSettleMs;
            }
        }

        // A newly observed true flag starts its watchdog now, not at the last idle sample.
        var flagStopped = _previousMoving == true && moving == false;
        if (moving == true && _previousMoving != true && !changed)
            _lastProgress = now;
        _previousMoving = moving;
        _hasObservation = true;

        // A sampled true -> false transition is the fast stop signal, including the
        // final position update. Settle only when that transition was unavailable/missed.
        if (flagStopped)
            _positionMotionUntil = now;
        var motion = moving == true || (!flagStopped && (changed || now < _positionMotionUntil));
        var stalled = moving == true && now - _lastProgress >= NoProgressTimeoutMs;

        if (_activeItem != null)
        {
            if (!targetVisible)
                ClearApproach();
            else if (stalled || now - _approachStarted >= MaxApproachMs)
            {
                ClearApproach();
                return Finish(PickupMotionState.BlockedPause);
            }
            else
            {
                if (motion)
                    _approachMoved = true;

                if (_approachMoved)
                {
                    if (motion || (moving == null && now - _lastProgress < PositionSettleMs))
                        return Finish(PickupMotionState.Approaching);
                    // Stopping ends this approach even when still outside pickup radius.
                    ClearApproach();
                }
                else if (now - _approachStarted < StartTimeoutMs)
                    return Finish(PickupMotionState.AwaitingMovement);
                else
                {
                    ClearApproach();
                    // Only this item's existing dispatch cooldown remains. Other
                    // candidates can be selected on this same stationary observation.
                }
            }
        }

        // A fresh hotkey session can redirect motion that predates its first input.
        // The adapter consumes this permission only after an actual dispatch, not
        // on a rejected preparation or selection. It never overrides an active approach.
        if (allowInitialMovementTakeover && motion)
            return Finish(PickupMotionState.TakingOverMovement);
        if (stalled)
        {
            // The active approach is gone, but a stuck true flag never licenses another click.
            return Finish(PickupMotionState.BlockedPause);
        }
        if (motion)
            return Finish(PickupMotionState.MovementPause);
        if (moving == null && now - _lastProgress < PositionSettleMs)
            return Finish(PickupMotionState.UnknownPause);
        return Finish(PickupMotionState.Idle);
    }

    public void RecordDispatch(PickupItemKey key, long now, double x, double y, bool isFar)
    {
        PruneCooldowns(now);
        _lastDispatch[key] = now;
        if (!isFar || _activeItem != null)
            return;

        // An adapter must sample before input. Only idle or explicitly permitted
        // first-session takeover can acquire a target; an erroneous blocked input cannot.
        if (!_hasObservation || !_allowDispatch)
            return;

        _activeItem = key;
        _approachStarted = now;
        // Only post-dispatch observations can satisfy the start condition. A
        // momentary stop of the old walk is not arrival at the newly selected item.
        _approachMoved = false;
        _progressX = x;
        _progressY = y;
        _hasPosition = double.IsFinite(x) && double.IsFinite(y);
        _lastProgress = now;
        _positionMotionUntil = now;
        Finish(PickupMotionState.AwaitingMovement);
    }

    /// <summary>Hotkey/UI cancellation releases the approach but preserves actual-input cooldowns.</summary>
    public void Reset(bool preserveCooldown = true)
    {
        ClearApproach();
        _hasPosition = false;
        _hasObservation = false;
        _previousMoving = null;
        _positionMotionUntil = 0;
        if (!preserveCooldown)
        {
            _lastDispatch.Clear();
            _lastPrune = 0;
        }
        Finish(PickupMotionState.UnknownPause);
    }

    public void ResetForArea() => Reset(preserveCooldown: false);

    private void PruneCooldowns(long now)
    {
        if (now - _lastPrune < 1000)
            return;
        _lastPrune = now;
        var expired = new List<PickupItemKey>();
        foreach (var entry in _lastDispatch)
            if (now - entry.Value >= CooldownRetentionMs)
                expired.Add(entry.Key);
        foreach (var key in expired)
            _lastDispatch.Remove(key);
    }

    private void ClearApproach()
    {
        _activeItem = null;
        _approachMoved = false;
    }

    private PickupMotionObservation Finish(PickupMotionState state)
    {
        State = state;
        _allowDispatch = state is PickupMotionState.Idle or PickupMotionState.TakingOverMovement;
        return new PickupMotionObservation(_allowDispatch, state);
    }
}
