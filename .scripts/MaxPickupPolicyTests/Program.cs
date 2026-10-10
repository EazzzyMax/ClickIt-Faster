using ClickItMax.Features.Click.MaxPickup;

var a = new PickupItemKey(1, 101);
var b = new PickupItemKey(2, 102);
var c = new PickupItemKey(3, 103);
var cases = new (string Name, Action Test)[]
{
    ("opposite far and five far candidates cannot interrupt one approach", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(p.ActiveItem == a, "far target retained");
        var pending = Enumerable.Range(2, 5).Select(i => new PickupItemKey((uint)i, -i));
        foreach (var t in new[] { 35L, 80L, 120L, 149L, 150L })
        {
            p.Observe(t, t >= 150, t >= 150 ? 1 : 0, 0);
            foreach (var key in pending)
                Expect(!p.CanDispatch(key, t), "other far blocked even when its cooldown is free");
        }
        p.Observe(600, true, 20, 0);
        Expect(!p.CanDispatch(b, 600), "walking blocks");
        Expect(p.Observe(750, false, 20, 0).AllowDispatch, "stopped approach releases");
        Expect(p.ActiveItem == null && p.CanDispatch(b, 750), "opposite target eligible after stop");
    }),
    ("near A B C interleave with actual dispatch cooldown", () =>
    {
        var p = Ready();
        Expect(p.IsUnattempted(a), "A initially unattempted");
        p.RecordDispatch(a, 0, 0, 0, false);
        p.Observe(35, false, 0, 0);
        Expect(p.CanDispatch(b, 35) && !p.CanDispatch(a, 35), "A cooling, B free");
        p.RecordDispatch(b, 35, 0, 0, false);
        p.Observe(70, false, 0, 0);
        Expect(p.CanDispatch(c, 70), "C free");
        p.RecordDispatch(c, 70, 0, 0, false);
        p.Observe(799, false, 0, 0);
        Expect(!p.CanDispatch(a, 799), "A before 800");
        p.Observe(800, false, 0, 0);
        Expect(p.CanDispatch(a, 800) && !p.CanDispatch(b, 800), "per item 800 boundary");
        Expect(p.CanDispatch(b, 800, 500), "configurable cooldown");
        Expect(!p.IsUnattempted(a) && !p.IsUnattempted(b) && !p.IsUnattempted(c), "only actual attempts counted");
        Expect(p.IsUnattempted(new PickupItemKey(1, 999)), "same id different address is distinct");
    }),
    ("unexpected motion after near input freezes without target attribution", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, false);
        Expect(!p.Observe(35, true, 0, 0).AllowDispatch, "near unexpectedly walks");
        Expect(p.ActiveItem == null && p.State == PickupMotionState.MovementPause, "no invented far target");
        Expect(!p.CanDispatch(b, 35), "B frozen");
        p.Observe(70, true, 2, 0);
        Expect(p.Observe(90, false, 3, 0).AllowDispatch, "explicit flag stop resumes immediately despite final position update");
    }),
    ("movement without fresh-activation permission pauses", () =>
    {
        var p = new PickupMotionPolicy();
        Expect(!p.Observe(0, true, 0, 0).AllowDispatch, "moving at activation");
        Expect(!p.CanDispatch(a, 0), "no first click");
        Expect(p.Observe(35, false, 0, 0).AllowDispatch, "known stop permits first click");
    }),
    ("fresh activation redirects existing motion and retains its far target until stop", () =>
    {
        var p = new PickupMotionPolicy();
        Expect(p.Observe(0, true, 0, 0, allowInitialMovementTakeover: true).State == PickupMotionState.TakingOverMovement,
            "first selection does not wait for preexisting walking to stop");
        Expect(p.CanDispatch(a, 0), "first target allowed while walking");
        // The final guard samples again after hover preparation, before native input.
        Expect(p.Observe(20, true, 1, 0, allowInitialMovementTakeover: true).AllowDispatch,
            "preparation must not consume the first-dispatch permission");
        p.RecordDispatch(a, 25, 1, 0, true);
        Expect(p.ActiveItem == a && p.State == PickupMotionState.AwaitingMovement, "sent far command owns target and awaits post-input observation");
        Expect(p.Observe(35, true, 2, 0).State == PickupMotionState.Approaching, "continuing walking does not need a second false-to-true edge");
        Expect(!p.Observe(200, true, 10, 0).AllowDispatch && p.ActiveItem == a, "already moving does not need a fresh start edge or timeout");
        Expect(!p.CanDispatch(b, 200), "opposite target remains blocked");
        Expect(p.Observe(250, false, 11, 0).AllowDispatch, "stop releases immediately");
    }),
    ("old walking stop immediately after takeover is not arrival at new target", () =>
    {
        var p = new PickupMotionPolicy();
        p.Observe(0, true, 0, 0, allowInitialMovementTakeover: true);
        p.RecordDispatch(a, 10, 0, 0, true);
        Expect(p.Observe(20, false, 0, 0).State == PickupMotionState.AwaitingMovement && p.ActiveItem == a,
            "old command can stop before the new command is reflected in memory");
        Expect(!p.CanDispatch(b, 20), "opposite far cannot interrupt during handoff");
        Expect(p.Observe(50, true, 1, 0).State == PickupMotionState.Approaching, "new movement starts normally");
        Expect(p.Observe(90, false, 2, 0).AllowDispatch, "new command's observed stop releases");
    }),
    ("rejected first click retains takeover permission until actual dispatch", () =>
    {
        var p = new PickupMotionPolicy();
        p.Observe(0, true, 0, 0, allowInitialMovementTakeover: true);
        // Selection/hover rejection does not call RecordDispatch.
        Expect(p.Observe(500, true, 20, 0, allowInitialMovementTakeover: true).AllowDispatch, "next candidate can still redirect");
        Expect(p.IsUnattempted(a) && p.IsUnattempted(b), "failed preparation does not consume ID or session attempt");
        p.RecordDispatch(b, 510, 20, 0, true);
        Expect(p.ActiveItem == b, "only actual successful dispatch creates ownership");
    }),
    ("initial takeover never overrides an existing approach even if permission is stale", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(!p.Observe(35, false, 0, 0, allowInitialMovementTakeover: true).AllowDispatch, "owned start remains protected");
        Expect(!p.Observe(80, true, 2, 0, allowInitialMovementTakeover: true).AllowDispatch, "owned walk remains protected");
        Expect(p.ActiveItem == a && !p.CanDispatch(b, 80), "another direction cannot interrupt");
    }),
    ("near first input consumes takeover and later unexpected walking still pauses", () =>
    {
        var p = new PickupMotionPolicy();
        p.Observe(0, true, 0, 0, allowInitialMovementTakeover: true);
        p.RecordDispatch(a, 10, 0, 0, false);
        Expect(p.ActiveItem == null, "near input does not invent a far target");
        Expect(!p.Observe(35, true, 1, 0).AllowDispatch, "after first input movement requires a stop");
        Expect(p.Observe(50, false, 2, 0).AllowDispatch && p.CanDispatch(b, 50), "near burst resumes at stop");
        p.Reset();
        Expect(p.Observe(60, true, 2, 0, allowInitialMovementTakeover: true).AllowDispatch, "new hotkey activation can redirect again");
    }),
    ("failed start releases other items at 150ms without a global backoff", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        foreach (var t in new[] { 1L, 35L, 100L, 149L })
            Expect(!p.Observe(t, false, 0, 0).AllowDispatch && p.ActiveItem == a, "false before start timeout waits");
        Expect(p.Observe(150, false, 0, 0).AllowDispatch, "failed start permits selection on the same observation");
        Expect(p.ActiveItem == null && p.CanDispatch(b, 150), "another item is free immediately");
        Expect(!p.CanDispatch(a, 150, 800), "failed item's original retry cooldown still applies");
        p.RecordDispatch(b, 150, 0, 0, true);
        Expect(p.ActiveItem == b && !p.Observe(185, false, 0, 0).AllowDispatch, "next far command owns its own start window");
    }),
    ("observed 123ms slow automatic start is retained past the 150ms deadline", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(!p.Observe(14, false, 0, 0).AllowDispatch, "observed pre-start sample");
        Expect(p.Observe(123, true, 0, 0).State == PickupMotionState.Approaching, "slowest recorded automatic start");
        Expect(p.Observe(170, true, 1, 0).State == PickupMotionState.Approaching && p.ActiveItem == a, "deadline does not cap genuine walking");
        Expect(!p.CanDispatch(b, 170), "opposite target cannot interrupt walking");
        Expect(p.Observe(200, false, 2, 0).AllowDispatch, "stop releases immediately");
    }),
    ("late observation checks movement before the expired start deadline", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(p.Observe(248, true, 11, 2).State == PickupMotionState.Approaching, "manual trace late frame already walking");
        Expect(p.ActiveItem == a && !p.CanDispatch(b, 248), "late frame does not discard real motion");
        Expect(p.Observe(300, false, 12, 2).AllowDispatch, "explicit stop releases");

        p.RecordDispatch(b, 400, 12, 2, true);
        Expect(p.Observe(1103, false, 21, 2).State == PickupMotionState.Approaching, "703ms missed walk detected from displacement");
        Expect(!p.CanDispatch(c, 1103), "position fallback still settles before dispatch");
        Expect(p.Observe(1203, false, 21, 2).AllowDispatch, "settled missed walk releases without backoff");
    }),
    ("position fallback sees a short walk missed by movement flag", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(p.Observe(35, false, 4, 0).State == PickupMotionState.Approaching, "false flag position movement");
        Expect(!p.Observe(100, false, 4, 0).AllowDispatch, "short walk settles");
        Expect(p.Observe(135, false, 4, 0).AllowDispatch, "settled short walk releases before start timeout");
    }),
    ("obstacle flag stop with distance remaining 20.8 ends approach immediately", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        p.Observe(100, true, 3, 0);
        const double reportedDistanceRemaining = 20.8;
        Expect(reportedDistanceRemaining > 20, "fixture is outside near radius");
        // Distance remaining belongs to the adapter; the policy intentionally has no radius input.
        Expect(p.Observe(135, false, 4.2, 0).AllowDispatch, "true to false plus final position releases before settle and outside radius");
        Expect(!p.CanDispatch(a, 250) && p.CanDispatch(b, 250), "no blind replay; other item allowed");
    }),
    ("wall detour progresses without watchdog timeout", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        foreach (var t in new[] { 100L, 1000L, 2000L, 3000L, 4000L })
        {
            Expect(p.Observe(t, true, -t / 100.0, t / 150.0).State == PickupMotionState.Approaching, "detour movement is progress");
            Expect(p.ActiveItem == a && !p.CanDispatch(b, t), "detour maintains lock");
        }
        Expect(p.Observe(4200, false, -40, 4000 / 150.0).AllowDispatch, "detour stopped");
    }),
    ("target disappearance immediately releases while motion remains frozen", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        Expect(p.Observe(35, false, 0, 0, false).AllowDispatch, "stationary disappeared immediate release");
        p.RecordDispatch(b, 40, 0, 0, true);
        Expect(!p.Observe(75, true, 2, 0, false).AllowDispatch, "moving disappeared remains paused");
        Expect(p.ActiveItem == null && p.State == PickupMotionState.MovementPause, "approach cleared, global freeze retained");
        Expect(p.Observe(200, false, 2, 0).AllowDispatch, "stop clears freeze");
    }),
    ("no progress watchdog releases approach without arbitrary new input", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        p.Observe(35, true, 0, 0);
        Expect(p.Observe(1535, true, 0, 0).State == PickupMotionState.BlockedPause, "watchdog stalled");
        Expect(p.ActiveItem == null && !p.CanDispatch(b, 1535), "active gone but no click allowed");
        Expect(!p.Observe(3000, true, 0, 0).AllowDispatch, "stuck true keeps frozen");
        Expect(p.Observe(3050, false, 0, 0).AllowDispatch && p.CanDispatch(b, 3050), "actual stop releases without a timed backoff");
    }),
    ("approach maximum bounds genuine endless movement", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        for (var t = 100; t < 15000; t += 100)
            p.Observe(t, true, t, 0);
        Expect(p.Observe(15000, true, 15000, 0).State == PickupMotionState.BlockedPause && p.ActiveItem == null, "15s cap releases active");
        Expect(!p.CanDispatch(b, 15000), "still moving does not dispatch");
        Expect(!p.Observe(15050, true, 15000, 0).AllowDispatch, "still walking after approach cap remains frozen");
        Expect(p.Observe(15100, false, 15000, 0).AllowDispatch, "stop releases without a timed backoff");
    }),
    ("cancellation preserves cooldown, area reset clears it", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, true);
        p.Reset();
        Expect(p.ActiveItem == null && !p.IsUnattempted(a), "cancel clears active, retains attempt");
        Expect(!p.CanDispatch(b, 35), "must observe after reset");
        p.Observe(35, false, 0, 0);
        Expect(!p.CanDispatch(a, 35) && p.CanDispatch(b, 35), "no replay after release");
        p.ResetForArea();
        Expect(p.IsUnattempted(a), "new area clears identity history");
        p.Observe(50, false, 0, 0);
        Expect(p.CanDispatch(a, 50), "new area id reuse allowed");
    }),
    ("unknown movement uses stable position fallback and finite input", () =>
    {
        var p = new PickupMotionPolicy();
        Expect(!p.Observe(0, null, 0, 0).AllowDispatch, "unknown initial settle");
        Expect(p.Observe(100, null, 0, 0).AllowDispatch, "unknown stable position permits");
        p.RecordDispatch(a, 100, 0, 0, true);
        Expect(!p.Observe(135, null, 3, 0).AllowDispatch, "unknown sees movement");
        Expect(p.Observe(235, null, 3, 0).AllowDispatch, "unknown stable stop");
        Expect(!p.Observe(250, false, double.NaN, 0).AllowDispatch, "invalid position freezes");
    }),
    ("preexisting motion cannot be attributed by an erroneous late dispatch", () =>
    {
        var p = new PickupMotionPolicy();
        p.Observe(0, true, 0, 0);
        p.RecordDispatch(a, 5, 0, 0, true);
        Expect(!p.IsUnattempted(a) && p.ActiveItem == null, "actual input recorded but preexisting motion not attributed");
        Expect(!p.Observe(35, true, 1, 0).AllowDispatch, "global motion still freezes");
    }),
    ("idle duration does not count toward movement watchdog", () =>
    {
        var p = Ready();
        p.Observe(10000, false, 0, 0);
        Expect(p.Observe(10035, true, 0, 0).State == PickupMotionState.MovementPause, "new motion gets its own watchdog");
    }),
    ("cooldown records expire after sixty seconds", () =>
    {
        var p = Ready();
        p.RecordDispatch(a, 0, 0, 0, false);
        p.Observe(59000, false, 0, 0);
        Expect(!p.IsUnattempted(a), "history retained before sixty seconds");
        p.Observe(60000, false, 0, 0);
        Expect(p.IsUnattempted(a) && p.CanDispatch(a, 60000), "expired identity pruned");
    })
};

var failed = 0;
foreach (var test in cases)
{
    try { test.Test(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
}
Console.WriteLine($"{cases.Length - failed}/{cases.Length} traces passed");
return failed == 0 ? 0 : 1;

static PickupMotionPolicy Ready()
{
    var p = new PickupMotionPolicy();
    Expect(p.Observe(0, false, 0, 0).AllowDispatch, "initial known stationary");
    return p;
}

static void Expect(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
