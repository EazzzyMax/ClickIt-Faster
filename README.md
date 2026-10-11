# ClickIt-Faster

An experimental item-pickup fork of [Barragek0/ClickIt](https://github.com/Barragek0/ClickIt), based on upstream commit `eb5d6b71821b7582108525d7f402a5ec8cb5f376`.

The repository is **ClickIt-Faster**; the menu display name is **ClickIt Faster**. The DLL, namespace/internal identity and settings file remain **ClickItMax** for continuity with existing installations. The original ClickIt remains available for comparison. Enable only one at a time: the plugins do not coordinate input.

## What changed

- Fast pickup always selects the nearest eligible item by fresh world/grid distance. A previous click excludes that ground entity only during its repeat delay; after expiry it competes equally with new items. Nearby pickup clicks do not wait for inventory updates.
- Before any walk or movement-skill command to an item beyond the movement-distance threshold, wait while any known eligible ground item remains within the nearby radius. Click cooldowns and temporarily hidden labels do not release this guard; known item identity is checked against the ground entity list. There is no automatic wait timeout.
- Distance uses world/grid positions, independently of displaced screen labels. The configurable nearby radius defaults to 20.
- A distant target owns the approach until observed movement ends, its eligible label disappears, or recovery conditions apply. This prevents alternating between distant targets while walking.
- A new hotkey session can redirect walking that began before pickup was activated.
- Independent checkboxes control hover verification for nearby and distant clicks.
- Per-item repeat delay is adjustable from 10 to 500 ms; new profiles default to 100 ms.
- Movement-start observation is limited to 150 ms, without the extra global 300 ms retry pause. Movement and progress guards still apply.
- Periodic label refresh presses the configured Toggle Items Hotkey twice at the configured interval. The first pair happens after a full interval, never immediately at activation.
- Structured diagnostics record sessions, dispatches, movement, rejection reasons and refresh events independently of on-screen debug messages.
- Optional movement skills approach visible distant loot, using the priority Flame Dash, Frostblink, Leap Slam, Whirling Blades, Dash, Blink Arrow, Lightning Warp, Shield Charge. Each skill can be disabled; the world-distance threshold defaults to 60.
- Flame Dash uses a shared 850 ms interval after manual or automatic use. If a charged, otherwise ready Flame Dash will be available within the configurable wait window (default 300 ms), walk toward the same item instead of using a lower-priority skill. Blink when ready only if the item is still far enough away.
- All eight supported movement skills hold their configured key until their own action starts or a charge is spent, with an 80 ms deadline and 5 ms polling. `IsUsing`/stage alone do not confirm a cast. One keydown is sent; context cancellation and an independent key-up watchdog bound ownership. An observed start is separate from arrival. Bounded diagnostic traces include every movement skill.

Fast mode handles items using ClickIt's existing eligibility/filtering rules. Turning off **«Быстрый подбор предметов»** selects the original regular pipeline inside this fork, including its broader mechanics. Total pickup time depends on walking, label overlap, latency and settings; the name is not a universal speedup guarantee.

## Requirements and installation

Windows x64, .NET 10 SDK for building, and a compatible PoE 1 ExileAPI/ExileCore host providing `ExileCore.dll`, `GameOffsets.dll` and `ProcessMemoryUtilities.dll`. Compatibility was checked against the locally installed host on 2026-10-11, not every host fork or future game patch.

Build with your host directory:

```powershell
dotnet build ClickItMax.csproj -c Release -p:exapiPackage="D:\ExileAPI"
```

With the host closed, create `Plugins/Compiled/ClickItMax` inside it. Copy `bin/Release/net10.0-windows/win-x64/ClickItMax.dll` into that folder. Place the included `alert.wav` at `config/ClickItMax/alert.wav` to avoid a first-run download. Do not overwrite host dependencies with this repository's inherited test libraries.

Start the host normally, open settings (normally F12), select **ClickIt Faster**, enable it and configure **Click Hotkey**. New profiles use F6. Check item/mechanics settings for what should be collected. Older builds show ClickItMax in the menu. Keep the original ClickIt disabled while using this fork.

Settings: `config/global/ClickItMax_settings.json`. Existing ClickIt profiles are separate. To uninstall, close the host and remove `Plugins/Compiled/ClickItMax`; optionally remove its settings, sound directory and logs.

## Main controls

| Control | Effect |
| --- | --- |
| «Быстрый подбор предметов» | Enables the alternate near/far item algorithm. |
| Nearby radius | World/grid distance classifying near and far targets; default 20. |
| Per-item repeat delay | Minimum interval before another actual click on the same ground entity; 10–500 ms, default 100 ms. |
| Movement to distant loot | Enables approved ground-target travel skills for visible loot. |
| Movement distance | World/grid threshold for using a travel skill; default 60. |
| «Ждать Flame Dash на ходу, мс» | Prefer walking until Flame Dash is ready within this window; default 300 ms, 0 disables waiting. |
| «Проверять наведение: ближние клики» | Verifies the hovered label before nearby clicks. |
| «Проверять наведение: дальние клики» | Independently verifies the hovered label before distant clicks. |
| Click Frequency Target | Target cadence; frame scheduling and other work may make it slower. |
| Toggle Item View | Enables periodic label refresh during pickup. |
| Toggle Item View Interval (ms) | Interval before the first and subsequent refresh pairs. |
| Toggle Items Hotkey | Key pressed twice for refresh, usually Z; not hardcoded. |
| Disable Clicking after Toggle Items (ms) | Extra block after both keypresses finish. |

Both hover checks default to enabled. Disabling them skips label-identity verification, retaining focus, hotkey, UI, movement and target-validity guards. Cursor-settle delays remain. Releasing pickup stops new commands but does not cancel walking already accepted by the game.

Movement settings are under **Controls → Movement: скиллы и приоритет**. Skill-bar bindings retain Ctrl/Shift/Alt modifiers. Manual and automatic Flame Dash share the interval; it is a tested working setting, not a claim about every character's exact instant-cast threshold. Waiting requires a charge and the other API readiness gates; it does not predict recharge of a missing charge. While waiting, the same visible item owns the walk. If it becomes closer than the movement threshold, continue ordinary pickup. Only after a deferred Flame Dash is actually sent does the replaced walk command release its own retry timer, allowing pickup on landing; other item timers remain intact.

Only one travel attempt is made toward an item per hotkey session. Teleports complete from observed displacement; ground movement uses its own action completion. Pending casts remain tracked across hotkey release. An uncertain Lightning Warp disables further Warp use until the area changes, avoiding queued teleports. For Dash, mirror its in-game Always Attack Without Moving setting in the plugin to compensate for reversed direction. This mode does not use offscreen pathfinding.

## Diagnostics and tests

Host logs: `Logs/ClickItMax/pickup-*.jsonl`. Look for session-start/end, click-sent, motion/rejection records and toggle-items. Dispatch is not proof of collection. Hotkey edges are sampled observations, not native input timestamps.

Movement emits `movement-cast`, `movement-end` and `movement-walk-wait-start/end`. Sampled `point-diagnostic` records preserve the decision-time label rectangle, attempted click/ground points, overlap and named UI blockers, window bounds, hover and the exact rejection stage. Supplementary reads are identified separately. Diagnostic detail is rate-limited; serialization and disk writes use a bounded background queue, dropping diagnostics rather than blocking input when full. Each process run retains up to four rotating parts; old runs are not deleted automatically. Logs are diagnostic data and should be reviewed before sharing.

Independent movement traces need no game or host:

```powershell
dotnet run --project .scripts/MaxPickupPolicyTests/MaxPickupPolicyTests.csproj -c Release
```

Focused regressions require compatible host libraries:

```powershell
dotnet test Tests/ClickItMax.Tests.csproj -c Release -p:exapiPackage="D:\ExileAPI" --filter "FullyQualifiedName~InputHandler|FullyQualifiedName~InteractionExecutor|FullyQualifiedName~ClickLabelInteractionServiceTests|FullyQualifiedName~InteractionExecutionRuntimeTests|FullyQualifiedName~LabelSelectionServiceTests|FullyQualifiedName~LabelSelectionEngineTests|FullyQualifiedName~PluginLoopHostTests"
```

The test assembly disables native mouse input; periodic refresh tests use fake clocks and keyboard delegates. The latest pickup/movement changes passed **122 focused regressions** locally; distance-only selection also passed **23 independent motion traces**. The earlier movement/diagnostic baseline passed 261 focused regressions; this is a separate historical run. The complete inherited suite is not claimed to pass. CI runs only independent motion traces without game or proprietary host binaries. Useful filters include `FullyQualifiedName~NearGroundLootBarrierTests|FullyQualifiedName~NearestPickupSelectionTests|FullyQualifiedName~MovementInputHoldTests|FullyQualifiedName~LootMovementTests`.

On 2026-10-12, the maintainer reported more stable live pickup and fewer missed items after adding the near-ground departure guard. This is a user report, not a benchmark or universal success guarantee. The subsequent display-name-only change is source-only pending the next approved build; it has not been compiled or verified in the live menu. No DLL release accompanies this source update.

## Credits and terms

Original ClickIt: **Barragek0 and upstream contributors**. Original documentation: [README.upstream.md](README.upstream.md). Fork maintained for **EazzzyMax**, with implementation and investigation assisted by OpenAI Codex (GPT-6). No new generated art or audio is included.

Upstream supplied no repository LICENSE at the pinned revision. This fork does not assign a new license to upstream code or third-party libraries. Inherited ThirdParty libraries and alert.wav are unchanged from upstream. No game files, local decompilation output, machine settings or private diagnostic sessions were added.

See [CHANGELOG.md](CHANGELOG.md) and the [Russian overview](README-Max.md).
