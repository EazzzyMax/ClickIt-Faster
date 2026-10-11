namespace ClickItMax.Features.Click.MaxPickup;

/// <summary>Shared instant-repeat clock for manual and automatic casts; recharge is a separate gate.</summary>
public sealed class FlameDashCooldownClock
{
    public long? LastUse { get; private set; }
    private long _address;
    private int _charges;
    private bool _active;

    public void RecordSent(long now) => LastUse = now;

    public void Observe(long now, long address, int charges, int maxCharges, bool active)
    {
        if (_address != address)
        {
            _address = address;
            // With an incomplete first observation we cannot know when the last manual cast happened.
            if (maxCharges <= 0 || charges < maxCharges || active) LastUse = now;
        }
        else if (charges < _charges || active && !_active)
            LastUse = now;
        // Confirmation can be later than keydown. Anchor to that evidence too, so network
        // latency cannot shorten the actual 850 ms interval between accepted casts.
        _charges = charges; _active = active;
    }
}
