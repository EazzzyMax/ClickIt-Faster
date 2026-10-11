# Changelog

## Source update — ClickIt Faster display name — 2026-10-12

- Show **ClickIt Faster** in the plugin menu after initialization and in assembly title/product metadata. Keep ClickItMax DLL/internal identity and settings paths.
- Document the maintainer's report of more stable pickup and fewer missed items with the near-ground guard, separately from automated validation.
- DLL build and release are deferred at the maintainer's request; the display-name change is not yet verified in the live host.

## Near-ground departure guard — 2026-10-12

- Block ordinary far clicks and movement-skill dispatch beyond the configured movement-distance threshold while eligible ground loot remains within the configured nearby radius, even during its click cooldown.
- Track known near item identities against ground entities when labels hide; release after disappearance or leaving the nearby radius. Recheck before mouse/skill input, including deferred Flame Dash. Keep waiting on unreadable state, without an automatic timeout, and log blocker identities/release.

## Distance-only pickup selection — 2026-10-12

- Replace near/new/retry passes with one nearest-eligible scan using fresh world/grid distance. Prior clicks only impose the configured per-item cooldown; expired retries have no priority penalty. Near/far now determines the action after selection, not its rank.
- Preserve candidate eligibility and approach ownership; the regular pipeline keeps its existing mechanic/cursor ranking. Log the selected target, distance, cooldown exclusions and selection policy.

## Movement input hold — 2026-10-12

- Apply bounded key hold to all eight supported movement skills, replacing the old 20 ms pulse for non-FD skills. Use an 80 ms deadline and 5 ms polling, with early release on own-action start or charge spend, context cancellation and an independent key-up watchdog.
- Treat IsUsing/stage as diagnostics only: a live FD trace briefly set IsUsing without action, charge spend or displacement. Preserve observed start evidence when handing control to the travel tracker; acceptance still does not mean arrival.
- Record hold results, acknowledgement source and full bounded traces for every movement skill. Preserve pickup thresholds, target selection, repeat values and existing arrival policy.

## Repeat-delay slider — 2026-10-12

- Set the per-item repeat-delay slider and setting bounds to 10–500 ms, with a 100 ms default for new profiles. Existing in-range profile values are preserved.

## Previous experiment — Flame Dash input hold

- Hold the existing Flame Dash binding until a new use, charge spend or own action is observed, with a 150 ms deadline and 5 ms polling.
- Release on context cancellation; an independent key-up watchdog also releases if an SDK read stalls. Preserve held user modifiers and send one keydown per attempt.
- Log acceptance, release reason, polling faults and high-resolution/native hold durations. Acceptance remains separate from arrival.
- Pickup thresholds, target selection, repeat policy and other movement input remain unchanged.

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
