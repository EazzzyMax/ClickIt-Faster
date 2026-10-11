using ClickItMax.Features.Click.MaxPickup;
using ExileCore.PoEMemory.Components;

namespace ClickItMax.Features.Click;

public sealed partial class ClickAutomationPort
{
    private MovementCastDiagnosticTrace? _maxMovementDiagnostic;
    private long _maxMovementDiagnosticSequence;
    private long _maxMovementDiagnosticTrackerSampleAt;
    private ushort _maxMovementDiagnosticSkill;
    private LootMovementChord _maxMovementDiagnosticChord;
    private uint _maxMovementDiagnosticPlayer, _maxMovementDiagnosticArea;

    private MovementCastDiagnosticTrace? MaxBeginMovementDiagnostic(LootSkillBinding binding,
        PickupItemKey target, System.Numerics.Vector2 position, Vector2 aim, object aimContext)
    {
        try
        {
            long now = Environment.TickCount64;
            _maxMovementDiagnostic?.Finish(now, "next-movement-attempt");
            _maxMovementDiagnosticSkill = binding.Skill.Id;
            _maxMovementDiagnosticChord = binding.Chord;
            _maxMovementDiagnosticPlayer = _maxPlayer ?? 0;
            _maxMovementDiagnosticArea = _maxArea ?? 0;
            var header = new
            {
                target, kind = binding.Kind.ToString(), skillId = binding.Skill.Id, skillAddress = binding.Skill.Address,
                binding.KeyText, binding.Chord.Ctrl, binding.Chord.Shift, binding.Chord.Alt,
                player = _maxMovementDiagnosticPlayer, area = _maxMovementDiagnosticArea,
                originX = position.X, originY = position.Y, aimX = aim.X, aimY = aim.Y,
                aimContext,
                previousClickSentAt = _maxPreviousSentAt, previousFlameUseAt = _maxFlameDashLastUse,
                sampleIntervalMs = MovementCastDiagnosticTrace.SampleIntervalMs,
                maximumDurationMs = MovementCastDiagnosticTrace.MaximumDurationMs,
                maximumEvents = MovementCastDiagnosticTrace.Capacity,
                inputMode = "hold-until-use", maximumHoldMs = MovementInputHold.MaximumHoldMs,
                holdPollIntervalMs = MovementInputHold.PollIntervalMs,
                acceptance = "own-action-or-charge-spend; IsUsing/Stage diagnostic only",
                observation = "supplementary SDK wrapper reads; not atomic, not raw memory or native keyboard hook"
            };
            var trace = new MovementCastDiagnosticTrace(++_maxMovementDiagnosticSequence, header, now,
                report => MaxLog("movement-cast-diagnostic", report));
            _maxMovementDiagnosticTrackerSampleAt = now;
            _maxMovementDiagnostic = trace;
            MaxCaptureMovementDiagnostic(trace, "before-aim");
            return trace;
        }
        catch { return null; }
    }

    private void MaxCaptureMovementDiagnostic(MovementCastDiagnosticTrace? trace, string phase)
    {
        try { trace?.Capture(Environment.TickCount64, phase, MaxReadMovementDiagnosticSnapshot); }
        catch { /* No diagnostic fault can authorize, cancel or repeat input. */ }
    }

    private object MaxReadMovementDiagnosticSnapshot()
    {
        var errors = new List<string>();
        T? Read<T>(string field, Func<T> getter) where T : struct
        {
            try { return getter(); }
            catch (Exception ex) { errors.Add(field + ":" + ex.GetType().Name); return null; }
        }
        var player = _gameController.Player;
        Actor? actor = null; Actor.ActionWrapper? action = null;
        ExileCore.PoEMemory.MemoryObjects.ActorSkill? skill = null;
        Life? life = null;
        System.Numerics.Vector2? grid = null;
        try { if (MaxReadPlayer(out _, out var position, out _)) grid = position; }
        catch (Exception ex) { errors.Add("position:" + ex.GetType().Name); }
        try
        {
            if (player != null && player.IsValid)
            {
                actor = MaxFreshActor(player);
                // Actor-owned skill wrappers retain the owner required by RemainingUses.
                skill = actor?.ActorSkills.FirstOrDefault(s => s.Id == _maxMovementDiagnosticSkill);
            }
        }
        catch (Exception ex) { errors.Add("actor-or-skill:" + ex.GetType().Name); }
        try { action = actor?.CurrentAction; }
        catch (Exception ex) { errors.Add("current-action:" + ex.GetType().Name); }
        try
        {
            var component = player?.GetComponent<Life>();
            if (component != null) life = player!.GetObject<Life>(component.Address);
        }
        catch (Exception ex) { errors.Add("life:" + ex.GetType().Name); }
        var snapshot = new
        {
            player = player?.Id, area = Read("area", () => _gameController.IngameState.Data.CurrentAreaHash),
            gridX = grid?.X, gridY = grid?.Y,
            actorAddress = actor?.Address,
            actionFlags = actor == null ? null : Read("action-flags", () => (int)actor.Action),
            actionId = actor == null ? null : Read("action-id", () => actor.ActionId),
            actorMoving = actor == null ? null : Read("actor-moving", () => actor.isMoving),
            animationId = actor == null ? null : Read("animation-id", () => actor.AnimationId),
            currentActionAddress = action?.Address,
            currentActionSkillId = action == null ? null : Read("action-skill", () => action.Skill.Id),
            destinationX = action == null ? null : Read("destination-x", () => action.DestinationX),
            destinationY = action == null ? null : Read("destination-y", () => action.DestinationY),
            skillAddress = skill?.Address,
            allowedToCast = skill == null ? null : Read("allowed", () => skill.AllowedToCast),
            canBeUsed = skill == null ? null : Read("usable", () => skill.CanBeUsed),
            canBeUsedWithWeapon = skill == null ? null : Read("weapon", () => skill.CanBeUsedWithWeapon),
            isOnCooldown = skill == null ? null : Read("cooldown", () => skill.IsOnCooldown),
            remainingUses = skill == null ? null : Read("remaining-uses", () => skill.RemainingUses),
            skillUseStage = skill == null ? null : Read("skill-use-stage", () => skill.SkillUseStage),
            isUsing = skill == null ? null : Read("skill-is-using", () => skill.IsUsing),
            isUsingOrCharging = skill == null ? null : Read("skill-is-using-or-charging", () => skill.IsUsingOrCharging),
            cost = skill == null ? null : Read("cost", () => skill.Cost),
            lifeCost = skill == null ? null : Read("life-cost", () => skill.LifeCost),
            esCost = skill == null ? null : Read("es-cost", () => skill.EsCost),
            mana = life == null ? null : Read("mana", () => life.CurMana),
            hp = life == null ? null : Read("hp", () => life.CurHP),
            es = life == null ? null : Read("es", () => life.CurES),
            foreground = Read("foreground", () => _gameController.Window.IsForeground()),
            hotkeyActive = Read("hotkey", () => _inputHandler.IsClickHotkeyActiveForCurrentInputState()),
            keyDown = Read("key", () => MaxKeyDown(_maxMovementDiagnosticChord.Key)),
            shiftDown = Read("shift", () => MaxKeyDown(Keys.ShiftKey)),
            ctrlDown = Read("ctrl", () => MaxKeyDown(Keys.ControlKey)),
            altDown = Read("alt", () => MaxKeyDown(Keys.Menu)),
            leftMouseDown = Read("lmb", () => MaxKeyDown(Keys.LButton)),
            rightMouseDown = Read("rmb", () => MaxKeyDown(Keys.RButton)),
            nativeInputDisabled = Mouse.DisableNativeInput,
            cursor = Read("cursor", Mouse.GetCursorPosition),
            previousFlameUseAt = _maxFlameDashLastUse,
            uiHoverAddress = Read("ui-hover", () => _gameController.IngameState.UIHoverElement?.Address ?? 0),
            errors
        };
        // Only frozen scalar values / scalar lists are returned, never live SDK wrappers.
        return snapshot;
    }

    private void MaxSampleMovementDiagnostic()
    {
        try
        {
            var trace = _maxMovementDiagnostic;
            if (trace == null || trace.Finished) return;
            long now = Environment.TickCount64;
            var player = _gameController.Player;
            if (player != null && (player.Id != _maxMovementDiagnosticPlayer ||
                _gameController.IngameState.Data.CurrentAreaHash != _maxMovementDiagnosticArea))
            { trace.Finish(now, "player-or-area-changed"); return; }
            if (trace.SampleDue(now)) MaxCaptureMovementDiagnostic(trace, "passive-observation");
            if (trace.EndDue(now)) trace.Finish(Environment.TickCount64, "observation-window-ended");
        }
        catch (Exception ex)
        {
            try
            {
                var trace = _maxMovementDiagnostic;
                long now = Environment.TickCount64;
                trace?.Note(now, "passive-read-error", new { error = ex.GetType().Name });
                if (trace?.EndDue(now) == true) trace.Finish(now, "observation-window-ended");
            }
            catch { }
        }
    }
}
