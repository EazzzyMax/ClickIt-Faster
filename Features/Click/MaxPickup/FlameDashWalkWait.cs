namespace ClickItMax.Features.Click.MaxPickup;

public enum FlameDashWalkDecision { None, Waiting, Cast, Cancelled }

/// <summary>Owns only a deferred blink for an already dispatched walk to the same item.</summary>
public sealed class FlameDashWalkWait
{
    public PickupItemKey? Target { get; private set; }
    public string Reason { get; private set; } = "";

    // All API/binding/resource gates must pass before calling this with readyExceptInterval=true.
    public static int EligibleWaitMs(bool readyExceptInterval, long now, long? lastUse, int maximumWaitMs)
    {
        if (!readyExceptInterval || maximumWaitMs <= 0 || lastUse == null) return 0;
        long elapsed = now - lastUse.Value;
        if (elapsed < 0 || elapsed >= LootMovementRules.FlameDashInstantIntervalMs) return 0;
        int remaining = (int)(LootMovementRules.FlameDashInstantIntervalMs - elapsed);
        return remaining <= maximumWaitMs ? remaining : 0;
    }

    public void Begin(PickupItemKey target) { Target = target; Reason = "walking-until-ready"; }
    public void Reset() { Target = null; Reason = ""; }

    public FlameDashWalkDecision Observe(PickupItemKey? activeWalk, bool contextAllowed, bool targetAvailable,
        float distance, int nearRadius, int movementDistance, bool flameReady, int eligibleWaitMs)
    {
        if (Target == null) return FlameDashWalkDecision.None;
        if (activeWalk != Target) return Cancel("walk-ended-or-target-changed");
        if (!contextAllowed) return Cancel("input-or-setting-gate");
        if (!targetAvailable) return Cancel("target-unavailable");
        if (!LootMovementRules.ShouldUse(distance, nearRadius, movementDistance)) return Cancel("already-close-enough");
        if (flameReady) { Reason = "ready-during-walk"; return FlameDashWalkDecision.Cast; }
        if (eligibleWaitMs > 0) { Reason = "walking-until-ready"; return FlameDashWalkDecision.Waiting; }
        return Cancel("flame-dash-no-longer-near-ready");
    }

    private FlameDashWalkDecision Cancel(string reason)
    {
        Target = null; Reason = reason;
        return FlameDashWalkDecision.Cancelled;
    }
}
