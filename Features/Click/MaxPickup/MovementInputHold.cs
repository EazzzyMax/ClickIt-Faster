namespace ClickItMax.Features.Click.MaxPickup;

internal readonly record struct MovementUseState(bool? IsUsing, bool? OwnAction, int? Charges, byte? Stage,
    string? ReadError = null);
internal sealed record MovementHoldEvent(string Phase, double ClockMs, double ElapsedMs,
    MovementUseState? State = null, string? Error = null);
internal readonly record struct MovementHoldResult(bool InputSent, bool Accepted, string Reason,
    double HeldMs, int Samples, int ReadErrors, string? AcceptedBy = null);

/// <summary>One keydown, bounded observation, one keyup. Acceptance is not arrival.</summary>
internal static class MovementInputHold
{
    internal const int MaximumHoldMs = 80;
    internal const int PollIntervalMs = 5;
    private const int MaximumPolls = MaximumHoldMs / PollIntervalMs + 1;

    // IsUsing/Stage can briefly change without a cast (live FD Cast17). Keep them diagnostic only.
    internal static string? StartEvidence(MovementUseState before, MovementUseState current) =>
        before.OwnAction == false && current.OwnAction == true ? "own-action" :
        before.Charges is int initial && current.Charges is int remaining && remaining < initial ? "charge-spent" : null;

    internal static bool Started(MovementUseState before, MovementUseState current) => StartEvidence(before, current) != null;

    // Dependencies make release/timeout/cancellation testable without native or game input.
    internal static MovementHoldResult Run(MovementUseState before, Func<bool> contextAllowed,
        Func<MovementUseState?> observe, Action down, Action up, Func<double> clockMs, Action<int> sleep,
        Action<MovementHoldEvent>? diagnostic = null)
    {
        bool sent = false, accepted = false;
        int samples = 0, errors = 0;
        double started = clockMs(), ended = started;
        string reason = "timeout";
        string? acceptedBy = null;
        void Emit(string phase, double at, MovementUseState? state = null, string? error = null)
        {
            try { diagnostic?.Invoke(new(phase, at, at - started, state, error)); } catch { }
        }
        try
        {
            if (!contextAllowed()) return new(false, false, "context-before-keydown", 0, 0, 0);
            started = clockMs();
            // Conservatively own cleanup even if the native call throws after partial dispatch.
            sent = true;
            down();
            Emit("key-down", started);
            for (int poll = 0; poll < MaximumPolls; poll++)
            {
                double now = clockMs();
                if (now - started >= MaximumHoldMs) { reason = "timeout"; break; }
                if (!contextAllowed()) { reason = "context-ended"; break; }
                MovementUseState? state = null;
                string? readError = null;
                try { state = observe(); }
                catch (Exception ex) { errors++; readError = ex.GetType().Name; }
                if (state?.ReadError is { } partialError) { errors++; readError = partialError; }
                samples++;
                Emit("hold-observation", clockMs(), state, readError);
                if (clockMs() - started >= MaximumHoldMs) { reason = "timeout"; break; }
                if (state is { } value && StartEvidence(before, value) is { } evidence)
                { accepted = true; acceptedBy = evidence; reason = "use-started"; break; }
                // Include getter time in the deadline; no further sleep after a slow read.
                double remaining = MaximumHoldMs - (clockMs() - started);
                if (remaining <= 0) { reason = "timeout"; break; }
                sleep(Math.Max(1, (int)Math.Min(PollIntervalMs, Math.Ceiling(remaining))));
                reason = "poll-limit"; // Also bounds a stalled/misbehaving injected clock.
            }
        }
        catch (Exception ex)
        {
            reason = "input-or-context-error";
            Emit("hold-error", clockMs(), error: ex.GetType().Name);
        }
        finally
        {
            if (sent)
            {
                ended = clockMs();
                try { up(); Emit("key-up", ended); }
                catch (Exception ex)
                { reason = "key-up-error"; Emit("key-up-error", ended, error: ex.GetType().Name); }
            }
        }
        return new(sent, accepted, reason, Math.Max(0, ended - started), samples, errors, acceptedBy);
    }
}
