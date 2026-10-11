# Changelog

## Movement and diagnostics update — 2026-10-11

- Approved visible-loot travel skills with per-skill toggles and fixed priority; Shield Charge last. Configurable world-distance threshold defaults to 60.
- Fresh skill-bar availability, charges and Ctrl bindings; shared manual/automatic Flame Dash 850 ms interval; action/displacement completion tracking.
- Walk toward the same target while waiting up to a configurable 300 ms for charged Flame Dash. Cast only while still beyond the movement threshold; 0 disables the wait.
- Successful deferred Flame Dash replaces only its owned walk command and releases that command's retry timer for immediate landing pickup. Other cooldowns remain intact.
- Context/target guards, one travel attempt per item/session and uncertain Lightning Warp protection.
- Sampled point-refusal diagnostics with label rectangles, click/ground coordinates, overlaps, named UI blockers, window bounds, hover and explicit rejection reasons.
- Bounded background log serialization/writes, rate limits and four-part per-run rotation.
- Movement, wait-policy and diagnostic regression tests. Private runtime logs, profiles, calibration utilities and workstation journals remain outside the public repository.

## Initial Faster fork — 2026-10-10

- Separate ClickItMax assembly, namespace and profile; original installation preserved.
- Near-item dispatch without waiting for ground disappearance or inventory updates; configurable per-entity repeat suppression.
- Single distant-target approach ownership with observed movement and bounded recovery.
- Movement-start timeout calibrated to 150 ms; removed extra global 300 ms retry pause.
- First dispatched command can take over walking at hotkey-session activation.
- Independent nearby/distant hover-verification checkboxes.
- Periodic configured double-key refresh without activation-time flicker.
- Session diagnostics and focused regressions.
- Public snapshot based directly on upstream eb5d6b7; private workstation journals and diagnostic sessions excluded.
