using ClickItMax.Features.Click.MaxPickup;

namespace ClickItMax.Features.Click;

public sealed partial class ClickAutomationPort
{
    private readonly NearGroundLootBarrier _maxNearGround = new();
    private long _maxNearGroundLogAt;
    private bool _maxNearGroundBlocked;

    private void MaxUpdateNearGroundLoot(IReadOnlyList<LabelOnGround>? labels, System.Numerics.Vector2 position)
    {
        bool uncertain = labels == null;
        var seen = new Dictionary<PickupItemKey, float>();
        if (labels != null)
            foreach (var label in labels)
            {
                try
                {
                    if (!MaxFreshItem(label, out var item)) continue;
                    if (!string.Equals(_labelInteractionPort.GetMechanicIdForLabel(label), MechanicIds.Items, StringComparison.Ordinal)) continue;
                    float distance = MaxGridDistance(item!, position);
                    if (!float.IsFinite(distance)) { uncertain = true; continue; }
                    if (distance > _settings.MaxPickupNearRadius.Value) continue;
                    var key = MaxKey(item!); seen[key] = distance; _maxNearGround.Track(key);
                }
                catch { uncertain = true; }
            }

        // Labels establish eligibility; ground entities retain the barrier even while labels hide/change.
        var ground = new Dictionary<PickupItemKey, Entity>();
        bool groundReadable = true;
        if (_maxNearGround.Tracked.Count != 0)
        {
            try
            {
                var entities = _gameController.EntityListWrapper?.OnlyValidEntities;
                if (entities == null) groundReadable = false;
                else
                    foreach (var entity in entities)
                    {
                        if (entity == null) continue;
                        var key = MaxKey(entity);
                        if (_maxNearGround.Tracked.Contains(key)) ground[key] = entity;
                    }
            }
            catch { groundReadable = false; }
        }
        _maxNearGround.Update(_settings.MaxPickupNearRadius.Value, key =>
        {
            if (!groundReadable) return new(null, null);
            if (ground.TryGetValue(key, out var item))
                return item.IsValid && item.Type == EntityType.WorldItem
                    ? new(true, MaxGridDistance(item, position)) : new(false, null);
            // A newly seen label can be ahead of the core's entity-list update. Do not drop it in that gap.
            return seen.TryGetValue(key, out float distance) ? new(true, distance) : new(false, null);
        }, uncertain);
    }

    private bool MaxNearGroundBlocksDeparture(float distance, System.Numerics.Vector2 position, bool refresh = false)
    {
        if (distance <= _settings.MaxPickupMovementDistance.Value) return false;
        if (refresh) MaxUpdateNearGroundLoot(GetLabelsForRegularSelection(), position);
        bool blocked = _maxNearGround.BlocksDeparture(distance, _settings.MaxPickupMovementDistance.Value);
        long now = Environment.TickCount64;
        if (blocked)
        {
            MaxSkip("departure:near-ground-loot", new { count = _maxNearGround.Remaining.Count, _maxNearGround.ReadUncertain });
            if (!_maxNearGroundBlocked || now - _maxNearGroundLogAt >= 500)
            {
                _maxNearGroundLogAt = now;
                MaxLog("near-ground-departure-blocked", new { distance, nearRadius = _settings.MaxPickupNearRadius.Value,
                    movementDistance = _settings.MaxPickupMovementDistance.Value, _maxNearGround.ReadUncertain,
                    remaining = _maxNearGround.Remaining.Select(p => new { target = p.Key, distance = p.Value }).ToArray(),
                    meaning = "ground presence; independent of click cooldown/hover/overlap" });
            }
        }
        else if (_maxNearGroundBlocked) MaxLog("near-ground-departure-released", new { distance });
        _maxNearGroundBlocked = blocked;
        return blocked;
    }
}
