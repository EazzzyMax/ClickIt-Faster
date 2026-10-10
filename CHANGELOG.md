# Changelog

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
