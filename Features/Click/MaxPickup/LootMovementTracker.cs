using System;

namespace ClickItMax.Features.Click.MaxPickup;

public readonly record struct LootMovementObservation(bool Finished, bool Failed, string Reason);

/// <summary>Owns one cast until real displacement/completion or a bounded failure. Never sends input.</summary>
public sealed class LootMovementTracker
{
    public bool Active { get; private set; }
    public PickupItemKey Target { get; private set; }
    public LootMovementKind Kind { get; private set; }
    public ushort SkillId { get; private set; }
    public long SentAt { get; private set; }
    public bool WarpUncertain { get; private set; }
    private double _originX, _originY, _lastX, _lastY;
    private long _lastMotion;
    private int _charges;
    private bool _cooldown, _sawAction, _accepted, _moved;
    private int _startTimeout, _completionTimeout;

    public void Begin(PickupItemKey target, LootMovementKind kind, ushort skillId, long now,
        double x, double y, int charges, bool cooldown, int castTimeMs)
    {
        Target = target; Kind = kind; SkillId = skillId; SentAt = now;
        _originX = _lastX = x; _originY = _lastY = y; _lastMotion = now;
        _charges = charges; _cooldown = cooldown;
        _accepted = _sawAction = _moved = false;
        // Watchdogs, not deliberate post-cast sleeps. Successful casts finish on evidence immediately.
        _startTimeout = Math.Clamp(castTimeMs + 200, 250, 1500);
        _completionTimeout = kind == LootMovementKind.LightningWarp ? 5000 : Math.Max(2000, castTimeMs + 1500);
        Active = true;
    }

    public LootMovementObservation Observe(long now, double x, double y, bool ownAction, int charges, bool cooldown)
    {
        if (!Active) return new(true, false, "idle");
        if (!double.IsFinite(x) || !double.IsFinite(y)) return new(false, false, "unreadable-position");
        double step = Math.Sqrt((x - _lastX) * (x - _lastX) + (y - _lastY) * (y - _lastY));
        double total = Math.Sqrt((x - _originX) * (x - _originX) + (y - _originY) * (y - _originY));
        if (step >= 0.25) { _lastMotion = now; _lastX = x; _lastY = y; }
        if (ownAction) _sawAction = true;
        _accepted |= ownAction || charges < _charges || !_cooldown && cooldown;
        _moved |= total >= 1;

        // Teleports complete on the coordinate jump, even while their animation tail remains.
        // Charge spend alone is never arrival. Starting an old walk is never a teleport.
        if (LootMovementRules.IsTeleport(Kind) && _accepted && step >= 8)
            return Finish(false, "teleported");
        // Ground skills must finish their own action; isMoving can stay false throughout Shield Charge.
        if (!LootMovementRules.IsTeleport(Kind) && _sawAction && !ownAction)
            return Finish(!_moved, _moved ? "action-finished" : "action-without-displacement");
        if (!LootMovementRules.IsTeleport(Kind) && _accepted && _moved && !ownAction && now - _lastMotion >= 60)
            return Finish(false, "position-settled");
        if (!_accepted && now - SentAt >= _startTimeout)
            return Finish(true, "cast-not-observed");
        if (now - SentAt >= _completionTimeout)
            return Finish(true, "completion-watchdog");
        return new(false, false, _accepted ? "movement-in-progress" : "awaiting-cast");
    }

    public void Reset() => Active = false;
    public void ResetForArea() { Active = false; WarpUncertain = false; }
    private LootMovementObservation Finish(bool failed, string reason)
    {
        if (failed && Kind == LootMovementKind.LightningWarp) WarpUncertain = true;
        Active = false;
        return new(true, failed, reason);
    }
}
