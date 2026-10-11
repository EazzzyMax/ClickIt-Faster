namespace ClickItMax.Features.Click.Interaction;

// Observes the actual decision path. Never invokes a predicate or reads game memory.
internal sealed class ClickPointDiagnosticTrace : IDisposable
{
    [ThreadStatic] private static ClickPointDiagnosticTrace? s_current;
    internal static bool Active => s_current != null;
    private readonly ClickPointDiagnosticTrace? _previous;
    private readonly Action<ClickPointDiagnosticTrace> _complete;
    private readonly List<object> _events = new(8);
    private readonly List<AreaBlockedSnapshot> _snapshots = new();
    private readonly List<string> _failures = new();
    private readonly long _started = Stopwatch.GetTimestamp();
    private bool _disposed;
    internal int TruncatedEvents { get; private set; }
    internal bool HasFailure => _failures.Count != 0;
    internal double CaptureMs => Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
    internal object[] Events => _events.ToArray();
    internal string[] Failures => _failures.ToArray();
    internal object[] AreaSnapshots => _snapshots.Select(Snapshot).ToArray();

    internal ClickPointDiagnosticTrace(Action<ClickPointDiagnosticTrace> complete)
    {
        _complete = complete; _previous = s_current; s_current = this;
    }

    internal static object Point(Vector2 p) => new { x = p.X, y = p.Y };
    internal static object Rect(RectangleF r) => new { x = r.X, y = r.Y, width = r.Width, height = r.Height };
    private void Add(object value) { if (_events.Count < 256) _events.Add(value); else TruncatedEvents++; }

    internal static void Reject(string stage, string reason)
    {
        try
        {
            if (s_current is not { } c) return;
            string failure = stage + ":" + reason;
            if (!c._failures.Contains(failure)) c._failures.Add(failure);
            c.Add(new { stage, reason });
        }
        catch { }
    }

    internal static void Layout(RectangleF rect, Vector2 topLeft, Vector2 preferred, IReadOnlyList<RectangleF> overlaps,
        string? name, string? path)
    {
        try
        {
            if (s_current is not { } c) return;
            c.Add(new { stage = "label-layout", coordinateSpace = "client", rect = Rect(rect), windowTopLeft = Point(topLeft),
                preferred = Point(preferred), name, path, overlaps = overlaps.Take(128).Select(Rect).ToArray(), overlapCount = overlaps.Count });
        }
        catch { }
    }

    internal static void Probe(Vector2 point, string outcome)
    {
        try { s_current?.Add(new { stage = "label-probe", point = Point(point), outcome }); } catch { }
    }

    internal static void OverlapHit(Vector2 point, RectangleF rect, int index)
    {
        try { s_current?.Add(new { stage = "label-overlap-hit", point = Point(point), rect = Rect(rect), index }); } catch { }
    }

    internal static void UiHit(Vector2 original, Vector2 tested, RectangleF rect, string coordinateSpace)
    {
        try { s_current?.Add(new { stage = "ui-rectangle-hit", original = Point(original), tested = Point(tested), rect = Rect(rect), coordinateSpace }); } catch { }
    }

    internal static void AreaDecision(AreaBlockedSnapshot snapshot, Vector2 point, bool allowed, int rule)
    {
        try
        {
            if (s_current is not { } c) return;
            int index = c._snapshots.FindIndex(s => ReferenceEquals(s, snapshot));
            if (index < 0 && c._snapshots.Count < 8) { index = c._snapshots.Count; c._snapshots.Add(snapshot); }
            c.Add(new { stage = "ui-area-decision", point = Point(point), allowed, rule = RuleName(rule), snapshot = index });
        }
        catch { }
    }

    internal static string RuleName(int index) => index switch
    {
        -2 => "outside-full-screen", -1 => "allowed", 0 => "health", 1 => "flasks", 2 => "flasks-tertiary",
        3 => "skills", 4 => "skills-tertiary", 5 => "mana", 6 => "buffs-union", 7 => "buffs-individual",
        8 => "quest-tracker", 9 => "chat", 10 => "map", 11 => "xp-bar", 12 => "mirage", 13 => "altar",
        14 => "ritual", 15 => "sentinel", _ => "unknown-rule-" + index
    };

    internal static void MovementAim(System.Numerics.Vector3 world, Vector2 projected, Vector2 aim, RectangleF window,
        bool finite, bool inside, bool allowed)
    {
        try
        {
            s_current?.Add(new { stage = "movement-aim", world = new { x = world.X, y = world.Y, z = world.Z },
                projected = Point(projected), aim = Point(aim), window = Rect(window), finite, inside, allowed });
        }
        catch { }
    }

    internal static void Hover(Vector2 point, ulong expected, ulong actual)
    {
        try { s_current?.Add(new { stage = "hover", point = Point(point), expected, actual }); } catch { }
    }

    internal static void Resolved(Vector2 unjittered, Vector2 finalClient, Vector2 finalAbsolute)
    {
        try { s_current?.Add(new { stage = "label-resolved", unjittered = Point(unjittered), finalClient = Point(finalClient), finalAbsolute = Point(finalAbsolute) }); } catch { }
    }

    internal static void Outcome(string stage, string result)
    {
        try { s_current?.Add(new { stage, result }); } catch { }
    }

    internal static void RectangleReadFailure(string reason, long? labelAddress, long? elementAddress, RectangleF? raw = null)
    {
        try
        {
            // May describe an overlap candidate. Only the resolver's terminal failure marks
            // the whole operation failed; a bad unrelated label must not do that.
            s_current?.Add(new { stage = "rectangle-read", reason, labelAddress, elementAddress,
                raw = raw is { } rect ? Rect(rect) : null });
        }
        catch { }
    }

    private static object Snapshot(AreaBlockedSnapshot s) => new
    {
        fullScreen = Rect(s.FullScreenRectangle), refreshedAtMs = s.LastBlockedUiRectanglesRefreshTimestampMs,
        buffsRefreshedAtMs = s.LastBuffsAndDebuffsRectanglesRefreshTimestampMs,
        rectangles = new[] { s.HealthSquareRectangle, s.FlaskRectangle, s.FlaskTertiaryRectangle, s.SkillsRectangle,
            s.SkillsTertiaryRectangle, s.ManaSquareRectangle, s.BuffsAndDebuffsRectangle, RectangleF.Empty, RectangleF.Empty,
            s.ChatPanelBlockedRectangle, s.MapPanelBlockedRectangle, s.XpBarBlockedRectangle, s.MirageBlockedRectangle,
            s.AltarBlockedRectangle, s.RitualBlockedRectangle, s.SentinelBlockedRectangle }
            .Select((r, i) => new { rule = RuleName(i), rect = Rect(r) }).ToArray(),
        buffs = s.BuffsAndDebuffsRectangles.Take(128).Select(Rect).ToArray(),
        quests = s.QuestTrackerBlockedRectangles.Take(128).Select(Rect).ToArray()
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; s_current = _previous;
        try { _complete(this); } catch { } // Diagnostics can never reject an input or escape into the coroutine.
    }
}
