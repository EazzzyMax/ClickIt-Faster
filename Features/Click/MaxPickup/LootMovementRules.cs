using System;

namespace ClickItMax.Features.Click.MaxPickup;

// Numeric order is the user-approved priority, not the order of skill bar slots.
public enum LootMovementKind { FlameDash, Frostblink, LeapSlam, WhirlingBlades, Dash, BlinkArrow, LightningWarp, ShieldCharge }

public static class LootMovementRules
{
    public const int FlameDashInstantIntervalMs = 850;

    public static LootMovementKind? Classify(string? internalName) => internalName?.ToLowerInvariant() switch
    {
        "flame_dash" or "flamedash" => LootMovementKind.FlameDash,
        "frostblink" or "frostblinkskillgem" => LootMovementKind.Frostblink,
        "leap_slam" or "leapslam" => LootMovementKind.LeapSlam,
        "whirling_blades" or "whirlingblades" => LootMovementKind.WhirlingBlades,
        "dash" or "quickdashgem" => LootMovementKind.Dash,
        "blink_arrow" or "blinkarrow" => LootMovementKind.BlinkArrow,
        "lightning_warp" or "lightningwarp" => LootMovementKind.LightningWarp,
        "shield_charge" or "shieldcharge" => LootMovementKind.ShieldCharge,
        _ => null // In particular, no Contains(): Return/Projection and other variants are not approved.
    };

    public static bool ShouldUse(float distance, int nearRadius, int movementDistance) =>
        float.IsFinite(distance) && distance > nearRadius && distance >= Math.Max(nearRadius, movementDistance);

    public static bool IsReady(LootMovementKind kind, bool allowed, bool usable, bool weapon, bool cooldown,
        int remainingUses, long now, long? lastFlameDashUse) =>
        allowed && usable && weapon && !cooldown &&
        (kind != LootMovementKind.FlameDash || remainingUses > 0 &&
            (lastFlameDashUse == null || now - lastFlameDashUse.Value >= FlameDashInstantIntervalMs));

    public static bool IsTeleport(LootMovementKind kind) => kind is LootMovementKind.FlameDash or
        LootMovementKind.Frostblink or LootMovementKind.Dash or LootMovementKind.BlinkArrow or LootMovementKind.LightningWarp;
}
