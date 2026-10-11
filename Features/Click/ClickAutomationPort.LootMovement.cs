using ClickItMax.Features.Click.MaxPickup;
using ExileCore.PoEMemory.Components;
using ActorSkill = ExileCore.PoEMemory.MemoryObjects.ActorSkill;
using Camera = ExileCore.PoEMemory.MemoryObjects.Camera;

namespace ClickItMax.Features.Click;

public sealed partial class ClickAutomationPort
{
    private readonly LootMovementTracker _maxTravel = new();
    private readonly Dictionary<PickupItemKey, long> _maxMovementAttempts = new();
    private readonly FlameDashCooldownClock _maxFlameClock = new();
    private readonly FlameDashWalkWait _maxFlameWalk = new();
    private long? _maxFlameDashLastUse => _maxFlameClock.LastUse;
    private long _maxFlameSampleAt;
    private int _maxSessionMovementCasts;
    private string _maxTravelReason = "";
    private bool _maxMovementPanelLogged;
    private long _maxMovementDecisionLogAt;
    private int _maxTravelInitialCharges;

    private sealed record LootSkillBinding(LootMovementKind Kind, ActorSkill Skill, LootMovementChord Chord, string KeyText);

    private Actor? MaxFreshActor(Entity player)
    {
        var shared = player.GetComponent<Actor>();
        return shared == null || shared.Address == 0 ? null : player.GetObject<Actor>(shared.Address);
    }

    // Passive, also called while the pickup hotkey is up. A manual Flame Dash must
    // count towards the same 850 ms interval as a cast sent by the picker.
    private void ObserveMaxMovementCooldowns()
    {
        long now = Environment.TickCount64;
        if (now - _maxFlameSampleAt < 20) return;
        _maxFlameSampleAt = now;
        try
        {
            var player = _gameController.Player;
            if (player == null || !player.IsValid) return;
            var actor = MaxFreshActor(player);
            var skill = actor?.ActorSkills.FirstOrDefault(s => LootMovementRules.Classify(s.InternalName) == LootMovementKind.FlameDash);
            if (skill == null) return;
            int charges = skill.RemainingUses;
            bool active = ((int)actor!.Action & 2) != 0 && actor.CurrentAction?.Skill?.Id == skill.Id;
            _maxFlameClock.Observe(now, skill.Address, charges, skill.CooldownInfo?.MaxUses ?? 0, active);
            if (!_maxMovementPanelLogged && _settings.Enable.Value && _settings.MaxPickupMovementEnabled.Value)
            {
                var preferred = MaxFindMovementSkill(player, out var panel);
                MaxLog("movement-panel", new { preferred = preferred?.Kind.ToString(), preferred?.KeyText,
                    distance = _settings.MaxPickupMovementDistance.Value, flameInstantIntervalMs = LootMovementRules.FlameDashInstantIntervalMs,
                    flameDashWalkWaitMs = _settings.MaxPickupFlameDashWaitMs.Value, panel });
                _maxMovementPanelLogged = true;
            }
        }
        catch { /* Missing state never grants readiness; selection reads it again. */ }
    }

    private bool MaxSkillEnabled(LootMovementKind kind) => kind switch
    {
        LootMovementKind.FlameDash => _settings.MaxPickupFlameDash.Value,
        LootMovementKind.Frostblink => _settings.MaxPickupFrostblink.Value,
        LootMovementKind.LeapSlam => _settings.MaxPickupLeapSlam.Value,
        LootMovementKind.WhirlingBlades => _settings.MaxPickupWhirlingBlades.Value,
        LootMovementKind.Dash => _settings.MaxPickupDash.Value,
        LootMovementKind.BlinkArrow => _settings.MaxPickupBlinkArrow.Value,
        LootMovementKind.LightningWarp => _settings.MaxPickupLightningWarp.Value && !_maxTravel.WarpUncertain,
        LootMovementKind.ShieldCharge => _settings.MaxPickupShieldCharge.Value,
        _ => false
    };

    private bool MaxSkillReady(LootSkillBinding binding, long now) => LootMovementRules.IsReady(binding.Kind,
        binding.Skill.AllowedToCast, binding.Skill.CanBeUsed, binding.Skill.CanBeUsedWithWeapon,
        binding.Skill.IsOnCooldown, binding.Kind == LootMovementKind.FlameDash ? binding.Skill.RemainingUses : 0,
        now, _maxFlameDashLastUse);

    private LootSkillBinding? MaxFindMovementSkill(Entity player, out object diagnostic)
        => MaxFindMovementSkill(player, out diagnostic, out _);

    private LootSkillBinding? MaxFindMovementSkill(Entity player, out object diagnostic, out int flameWaitMs)
    {
        flameWaitMs = 0;
        var skipped = new List<object>();
        var candidates = new List<LootSkillBinding>();
        var actor = MaxFreshActor(player);
        if (actor == null) { diagnostic = "actor-unavailable"; return null; }
        var actorSkills = actor.ActorSkills;
        var sharedState = _gameController.IngameState;
        var ui = player.GetObject<ExileCore.PoEMemory.MemoryObjects.IngameState>(sharedState.Address).IngameUi;
        foreach (var bar in new[] { ui.SkillBar, ui.HiddenSkillBar })
        {
            if (bar == null || bar.Address == 0) continue;
            foreach (var entry in bar.Skills)
            {
                try
                {
                    // Use the actor-owned fresh object: RemainingUses requires its Actor owner.
                    var panelSkill = entry.Skill;
                    var skill = panelSkill == null ? null : actorSkills.FirstOrDefault(s => s.Id == panelSkill.Id);
                    if (skill == null || LootMovementRules.Classify(skill.InternalName) is not { } kind || !MaxSkillEnabled(kind)) continue;
                    string text = entry.GetChildAtIndex(0).GetChildAtIndex(0).GetChildAtIndex(0).GetChildAtIndex(1).GetText(256)?.Trim() ?? "";
                    if (!LootMovementChord.TryParse(text, out var chord)) { skipped.Add(new { kind, reason = "unsupported-key", text }); continue; }
                    // Don't release a physical key or accidentally use another Ctrl/Alt slot.
                    if (MaxKeyDown(chord.Key) || MaxKeyDown(Keys.ControlKey) && !chord.Ctrl || MaxKeyDown(Keys.Menu) && !chord.Alt)
                    { skipped.Add(new { kind, reason = "held-key-or-modifier", text }); continue; }
                    var candidate = new LootSkillBinding(kind, skill, chord, text);
                    long candidateNow = Environment.TickCount64;
                    if (!MaxSkillReady(candidate, candidateNow))
                    {
                        if (kind == LootMovementKind.FlameDash)
                        {
                            bool readyExceptInterval = LootMovementRules.IsReady(kind, skill.AllowedToCast, skill.CanBeUsed,
                                skill.CanBeUsedWithWeapon, skill.IsOnCooldown, skill.RemainingUses, candidateNow, null);
                            flameWaitMs = Math.Max(flameWaitMs, FlameDashWalkWait.EligibleWaitMs(readyExceptInterval,
                                candidateNow, _maxFlameDashLastUse, _settings.MaxPickupFlameDashWaitMs.Value));
                        }
                        skipped.Add(new { kind, reason = "not-ready", text, skill.AllowedToCast, skill.CanBeUsed, skill.CanBeUsedWithWeapon, skill.IsOnCooldown,
                            remaining = kind == LootMovementKind.FlameDash ? (int?)skill.RemainingUses : null,
                            flameElapsedMs = _maxFlameDashLastUse is long last ? candidateNow - last : (long?)null });
                        continue;
                    }
                    candidates.Add(candidate);
                }
                catch (Exception ex) { skipped.Add(new { reason = "slot-read-error", error = ex.GetType().Name }); }
            }
        }
        // Prefer an unmodified binding if the same skill is on both bars. No duplicate cast.
        var result = candidates.OrderBy(c => c.Kind).ThenBy(c => c.Chord.Ctrl || c.Chord.Alt || c.Chord.Shift).FirstOrDefault();
        diagnostic = new { available = candidates.Select(c => new { c.Kind, c.KeyText }), skipped, flameWaitMs };
        return result;
    }

    private static bool MaxKeyDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    private bool MaxMovementInputAllowed() => _settings.Enable.Value && _settings.MaxPickupEnabled.Value
        && _settings.MaxPickupMovementEnabled.Value && _inputHandler.IsClickHotkeyActiveForCurrentInputState()
        && _gameController.Window.IsForeground() && !MaxKeyDown(Keys.LButton) && !MaxKeyDown(Keys.RButton)
        && _inputHandler.CanClick(_gameController, false, false);

    private bool MaxTryCastMovement(LabelOnGround label, Entity target, Entity player, out int flameWaitMs, bool onlyFlameDash = false)
    {
        flameWaitMs = 0;
        if (!_settings.MaxPickupMovementEnabled.Value || !MaxReadPlayer(out _, out var initialPosition, out _)
            || !LootMovementRules.ShouldUse(MaxGridDistance(target, initialPosition),
            _settings.MaxPickupNearRadius.Value, _settings.MaxPickupMovementDistance.Value)) return false;
        var key = MaxKey(target);
        long now = Environment.TickCount64;
        // After one jump toward an item, let an ordinary pickup click finish the approach.
        // A blocked/short cast therefore cannot loop, repeatedly consume charges, or ping-pong.
        if (_maxMovementAttempts.ContainsKey(key)) return false;
        var binding = MaxFindMovementSkill(player, out var diagnostic, out var waitMs);
        if (binding?.Kind != LootMovementKind.FlameDash && waitMs > 0 && !onlyFlameDash)
        {
            // Let the ordinary verified pickup click start walking. Own the deferred blink
            // only after that click succeeds, never on a failed point/hover preparation.
            flameWaitMs = waitMs;
            return false;
        }
        if (onlyFlameDash && binding?.Kind != LootMovementKind.FlameDash) return false;
        if (binding == null)
        {
            MaxSkip("movement:no-ready-skill", diagnostic);
            if (now - _maxMovementDecisionLogAt >= 500)
            {
                MaxLog("movement-unavailable", new { target = key, diagnostic, fallback = "walk" });
                _maxMovementDecisionLogAt = now;
            }
            return false;
        }
        var window = _gameController.Window.GetWindowRectangleTimeCache;
        var camera = player.GetObject<Camera>(_gameController.IngameState.Camera.Address);
        var render = target.GetComponent<ExileCore.PoEMemory.Components.Render>();
        var playerRender = player.GetComponent<ExileCore.PoEMemory.Components.Render>();
        if (render == null || playerRender == null) return false;
        var ground = target.GetObject<ExileCore.PoEMemory.Components.Render>(render.Address).PosNum;
        var projected = camera.WorldToScreen(ground);
        var point = new Vector2(projected.X + window.X, projected.Y + window.Y);
        bool invertDash = binding.Kind == LootMovementKind.Dash &&
            (_settings.MaxPickupDashAlwaysAttackInPlace.Value || binding.Chord.Shift || MaxKeyDown(Keys.ShiftKey));
        if (invertDash)
        {
            var center = camera.WorldToScreen(player.GetObject<ExileCore.PoEMemory.Components.Render>(playerRender.Address).PosNum);
            point = new Vector2(2 * (center.X + window.X) - point.X, 2 * (center.Y + window.Y) - point.Y);
        }
        bool finite = float.IsFinite(point.X) && float.IsFinite(point.Y);
        bool inside = finite && PointInRect(window, point);
        bool allowedAim = inside && PointIsInClickableArea(point, "Items");
        ClickPointDiagnosticTrace.MovementAim(ground, new Vector2(projected.X + window.X, projected.Y + window.Y), point, window, finite, inside, allowedAim);
        if (!allowedAim)
        {
            ClickPointDiagnosticTrace.Reject("movement-aim", !finite ? "non-finite" : !inside ? "outside-game-window" : "ui-area-rejected");
            MaxSkip("movement:ground-point-blocked", new { key, binding.Kind, point.X, point.Y, invertDash }); return false;
        }
        if (!MaxMovementInputAllowed() || !MaxFreshItem(label, out var current) || MaxKey(current!) != key) return false;
        if (!Mouse.SetCursorPos((int)point.X, (int)point.Y)) return false;
        // Let PoE observe the new ground aim, then recheck context before input.
        Thread.Sleep(10);
        if (!MaxMovementInputAllowed() || !MaxReadPlayer(out var freshPlayer, out var position, out _)
            || freshPlayer!.Id != _maxPlayer || _gameController.IngameState.Data.CurrentAreaHash != _maxArea
            || !MaxFreshItem(label, out current) || MaxKey(current!) != key
            || !LootMovementRules.ShouldUse(MaxGridDistance(current!, position), _settings.MaxPickupNearRadius.Value, _settings.MaxPickupMovementDistance.Value))
            return false;
        // Re-read availability after aiming; no stale SkillBar cooldown snapshot authorizes a cast.
        var freshActor = MaxFreshActor(freshPlayer);
        var freshSkill = freshActor?.ActorSkills.FirstOrDefault(s => s.Id == binding.Skill.Id);
        if (freshSkill == null) return false;
        var recheckedBinding = MaxFindMovementSkill(freshPlayer, out _, out var recheckedWaitMs);
        if (recheckedBinding == null || recheckedBinding.Skill.Id != binding.Skill.Id || recheckedBinding.Chord != binding.Chord) return false;
        if (recheckedBinding.Kind != LootMovementKind.FlameDash && recheckedWaitMs > 0)
        { flameWaitMs = recheckedWaitMs; return false; }
        binding = binding with { Skill = freshSkill };
        if (!MaxSkillReady(binding, Environment.TickCount64)) return false;
        int charges = binding.Kind == LootMovementKind.FlameDash ? freshSkill.RemainingUses : 0;
        int castTime = (int)Math.Clamp(freshSkill.CastTime.TotalMilliseconds, 0, 3000);
        long sentAt = Environment.TickCount64;
        if (!MaxSendMovementChord(binding.Chord)) return false;
        ClickPointDiagnosticTrace.Outcome("movement-input", "sent:" + binding.KeyText);
        _maxTravel.Begin(key, binding.Kind, freshSkill.Id, sentAt, position.X, position.Y, charges, false, castTime);
        _maxTravelInitialCharges = charges;
        _maxMovementAttempts[key] = sentAt;
        _maxSessionMovementCasts++;
        _maxFlameWalk.Reset();
        if (binding.Kind == LootMovementKind.FlameDash) _maxFlameClock.RecordSent(sentAt);
        _maxTravelReason = "";
        _performanceMonitor.MarkInterval(IntervalKind.Click);
        MaxLog("movement-cast", new { target = key, kind = binding.Kind.ToString(), internalName = freshSkill.InternalName,
            binding.KeyText, binding.Chord.Ctrl, binding.Chord.Shift, binding.Chord.Alt, invertDash, distance = MaxGridDistance(current!, position),
            threshold = _settings.MaxPickupMovementDistance.Value, sentAt, x = position.X, y = position.Y, aimX = point.X, aimY = point.Y,
            chargesBefore = charges, castTimeMs = castTime, flameInstantIntervalMs = LootMovementRules.FlameDashInstantIntervalMs, diagnostic });
        _settings.MaxPickupStatus = "Movement: " + binding.Kind;
        return true;
    }

    private void MaxBeginFlameDashWalk(PickupItemKey key, int waitMs)
    {
        if (waitMs <= 0 || _maxPickup.ActiveItem != key) return;
        _maxFlameWalk.Begin(key);
        MaxLog("movement-walk-wait-start", new { target = key, remainingMsAtSelection = waitMs,
            maximumWaitMs = _settings.MaxPickupFlameDashWaitMs.Value, fallback = "walk-same-target" });
    }

    private void MaxCancelFlameDashWalk(string reason)
    {
        if (_maxFlameWalk.Target is { } target)
            MaxLog("movement-walk-wait-end", new { target, reason });
        _maxFlameWalk.Reset();
    }

    // Runs before normal walk dispatch is blocked. It can replace only our owned walk,
    // with a ready Flame Dash toward exactly the same still-visible item.
    private bool MaxTryResumeFlameDashWalk(IReadOnlyList<LabelOnGround> labels, Entity player, System.Numerics.Vector2 position)
    {
        if (_maxFlameWalk.Target is not { } key) return false;
        LabelOnGround? label = null;
        Entity? target = null;
        foreach (var candidate in labels)
        {
            try
            {
                if (MaxFreshItem(candidate, out var item) && MaxKey(item!) == key) { label = candidate; target = item; break; }
            }
            catch { }
        }
        float distance = target == null ? float.NaN : MaxGridDistance(target, position);
        bool contextAllowed = MaxMovementInputAllowed() && _settings.MaxPickupFlameDash.Value && _settings.MaxPickupFlameDashWaitMs.Value > 0;
        bool eligibleTarget = target != null && float.IsFinite(distance) && distance <= _settings.ClickDistance.Value
            && string.Equals(_labelInteractionPort.GetMechanicIdForLabel(label!), MechanicIds.Items, StringComparison.Ordinal);
        LootSkillBinding? binding = null;
        int waitMs = 0;
        if (contextAllowed && eligibleTarget && _maxPickup.ActiveItem == key)
            binding = MaxFindMovementSkill(player, out _, out waitMs);
        var decision = _maxFlameWalk.Observe(_maxPickup.ActiveItem, contextAllowed, eligibleTarget, distance,
            _settings.MaxPickupNearRadius.Value, _settings.MaxPickupMovementDistance.Value,
            binding?.Kind == LootMovementKind.FlameDash, waitMs);
        if (decision == FlameDashWalkDecision.Cancelled)
        {
            MaxLog("movement-walk-wait-end", new { target = key, reason = _maxFlameWalk.Reason, distance });
            return false; // The existing ordinary approach is still owned by the pickup policy.
        }
        if (decision != FlameDashWalkDecision.Cast) return false;
        using var pointDiagnostics = MaxBeginPointDiagnostics(label!, target!, position);
        try
        {
            if (MaxTryCastMovement(label!, target!, player, out _, onlyFlameDash: true))
            {
                bool transferred = _maxPickup.TransferWalkToMovement(key);
                MaxLog("movement-walk-wait-end", new { target = key, reason = "flame-dash-sent", distance,
                    replacedWalkClickCooldownReleased = transferred });
                return true;
            }
        }
        catch (Exception ex)
        {
            MaxLog("movement-error", new { ex.Message, type = ex.GetType().Name, inputAlreadySent = _maxTravel.Active });
            if (_maxTravel.Active) { _maxPickup.TransferWalkToMovement(key); _maxFlameWalk.Reset(); return true; }
        }
        // Never repeatedly move the cursor over a rejected world projection while walking.
        _maxFlameWalk.Reset();
        MaxLog("movement-walk-wait-end", new { target = key, reason = "cast-rejected-continue-walk", distance });
        return false;
    }

    private bool MaxSendMovementChord(LootMovementChord chord)
    {
        if (Mouse.DisableNativeInput) return false;
        var pressed = new List<Keys>();
        try
        {
            foreach (var pair in new[] { (chord.Ctrl, Keys.ControlKey), (chord.Shift, Keys.ShiftKey), (chord.Alt, Keys.Menu) })
                if (pair.Item1 && !MaxKeyDown(pair.Item2)) { Keyboard.KeyDown(pair.Item2); pressed.Add(pair.Item2); }
            if (!MaxMovementInputAllowed()) return false;
            Keyboard.KeyDown(chord.Key); pressed.Add(chord.Key);
            Thread.Sleep(20);
            return true;
        }
        finally
        {
            // Only release keys injected by us. In particular, never release the user's Shift.
            for (int i = pressed.Count - 1; i >= 0; i--) Keyboard.KeyUp(pressed[i]);
        }
    }

    // True means an owned movement cast still blocks pickup dispatch this tick.
    private bool MaxObserveTravel(Entity player, System.Numerics.Vector2 position)
    {
        if (!_maxTravel.Active) return false;
        var actor = MaxFreshActor(player);
        if (actor == null) { MaxSkip("movement:actor-unavailable"); return true; }
        var skill = actor.ActorSkills.FirstOrDefault(s => s.Id == _maxTravel.SkillId);
        bool ownAction = ((int)actor.Action & 2) != 0 && actor.CurrentAction?.Skill?.Id == _maxTravel.SkillId;
        var result = _maxTravel.Observe(Environment.TickCount64, position.X, position.Y, ownAction,
            _maxTravel.Kind == LootMovementKind.FlameDash && skill != null ? skill.RemainingUses : _maxTravelInitialCharges, skill?.IsOnCooldown == true);
        _settings.MaxPickupStatus = "Movement: " + result.Reason;
        if (result.Reason != _maxTravelReason || result.Finished)
        {
            _maxTravelReason = result.Reason;
            MaxLog(result.Finished ? "movement-end" : "movement-state", new { target = _maxTravel.Target, kind = _maxTravel.Kind.ToString(),
                result.Reason, result.Failed, elapsedMs = Environment.TickCount64 - _maxTravel.SentAt,
                x = position.X, y = position.Y, ownAction, actorMoving = actor.isMoving, warpBlockedUntilAreaChange = _maxTravel.WarpUncertain });
        }
        if (!result.Finished) { MaxSkip("movement:" + result.Reason); return true; }
        // Arrival changes every label's screen position. Never reuse a pre-cast point/layout.
        _maxPickup.Reset();
        _labelSelectionService.ResetCaches(); _labelClickPointResolver.InvalidateLayout(); _cachedLabels.ForceUpdate();
        return false;
    }
}
