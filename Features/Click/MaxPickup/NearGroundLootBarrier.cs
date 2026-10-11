namespace ClickItMax.Features.Click.MaxPickup;

internal readonly record struct GroundLootPresence(bool? Exists, float? Distance);

/// <summary>Known eligible near loot blocks long departures until it leaves the ground/near radius.</summary>
internal sealed class NearGroundLootBarrier
{
    private readonly HashSet<PickupItemKey> _tracked = [];
    private readonly Dictionary<PickupItemKey, float?> _remaining = [];
    private int _nearRadius;
    internal IReadOnlyCollection<PickupItemKey> Tracked => _tracked;
    internal IReadOnlyDictionary<PickupItemKey, float?> Remaining => _remaining;
    internal bool ReadUncertain { get; private set; }

    internal void Track(PickupItemKey key) => _tracked.Add(key);

    internal void Update(int nearRadius, Func<PickupItemKey, GroundLootPresence> read, bool scanUncertain = false)
    {
        _nearRadius = nearRadius; _remaining.Clear(); ReadUncertain = scanUncertain;
        foreach (var key in _tracked.ToArray())
        {
            GroundLootPresence presence;
            try { presence = read(key); }
            catch { presence = new(null, null); }
            if (presence.Exists == false) { _tracked.Remove(key); continue; }
            if (presence.Exists == true && presence.Distance is float distance && float.IsFinite(distance) && distance >= 0)
            {
                if (distance > nearRadius) { _tracked.Remove(key); continue; }
                _remaining[key] = distance;
            }
            else { _remaining[key] = null; ReadUncertain = true; }
        }
    }

    // Literal threshold: 40 itself is allowed; farther than 40 is blocked while near loot remains.
    internal bool BlocksDeparture(float targetDistance, int movementDistance) =>
        (_remaining.Count != 0 || ReadUncertain) &&
        (!float.IsFinite(targetDistance) || targetDistance > Math.Max(movementDistance, _nearRadius));

    internal void Reset() { _tracked.Clear(); _remaining.Clear(); ReadUncertain = false; }
}
