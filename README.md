# ClickIt-Faster

An experimental item-pickup fork of [Barragek0/ClickIt](https://github.com/Barragek0/ClickIt), based on upstream commit `eb5d6b71821b7582108525d7f402a5ec8cb5f376`.

The repository is **ClickIt-Faster**. The installed plugin, assembly and profile are **ClickItMax**, so the original ClickIt remains available for comparison. Enable only one at a time: the plugins do not coordinate input.

## What changed

- Nearby items are clicked once, then temporarily excluded by ground-entity identity. The next click does not wait for inventory updates or ground disappearance.
- Distance uses world/grid positions, independently of displaced screen labels. The configurable nearby radius defaults to 20.
- A distant target owns the approach until observed movement ends, its eligible label disappears, or recovery conditions apply. This prevents alternating between distant targets while walking.
- A new hotkey session can redirect walking that began before pickup was activated.
- Independent checkboxes control hover verification for nearby and distant clicks.
- Per-item repeat delay is adjustable; new profiles default to 800 ms.
- Movement-start observation is limited to 150 ms, without the extra global 300 ms retry pause. Movement and progress guards still apply.
- Periodic label refresh presses the configured Toggle Items Hotkey twice at the configured interval. The first pair happens after a full interval, never immediately at activation.
- Structured diagnostics record sessions, dispatches, movement, rejection reasons and refresh events independently of on-screen debug messages.

Fast mode handles items using ClickIt's existing eligibility/filtering rules. Turning off **«Быстрый подбор предметов»** selects the original regular pipeline inside this fork, including its broader mechanics. Total pickup time depends on walking, label overlap, latency and settings; the name is not a universal speedup guarantee.

## Requirements and installation

Windows x64, .NET 10 SDK for building, and a compatible PoE 1 ExileAPI/ExileCore host providing `ExileCore.dll`, `GameOffsets.dll` and `ProcessMemoryUtilities.dll`. Compatibility was checked against the locally installed host on 2026-10-10, not every host fork or future game patch.

Build with your host directory:

```powershell
dotnet build ClickItMax.csproj -c Release -p:exapiPackage="D:\ExileAPI"
```

With the host closed, create `Plugins/Compiled/ClickItMax` inside it. Copy `bin/Release/net10.0-windows/win-x64/ClickItMax.dll` into that folder. Place the included `alert.wav` at `config/ClickItMax/alert.wav` to avoid a first-run download. Do not overwrite host dependencies with this repository's inherited test libraries.

Start the host normally, open settings (normally F12), select **ClickItMax**, enable it and configure **Click Hotkey**. New profiles use F6. Check item/mechanics settings for what should be collected. Keep the original ClickIt disabled while using ClickItMax.

Settings: `config/global/ClickItMax_settings.json`. Existing ClickIt profiles are separate. To uninstall, close the host and remove `Plugins/Compiled/ClickItMax`; optionally remove its settings, sound directory and logs.

## Main controls

| Control | Effect |
| --- | --- |
| «Быстрый подбор предметов» | Enables the alternate near/far item algorithm. |
| Nearby radius | World/grid distance classifying near and far targets; default 20. |
| Per-item repeat delay | Minimum interval before another actual click on the same ground entity; default 800 ms. |
| «Проверять наведение: ближние клики» | Verifies the hovered label before nearby clicks. |
| «Проверять наведение: дальние клики» | Independently verifies the hovered label before distant clicks. |
| Click Frequency Target | Target cadence; frame scheduling and other work may make it slower. |
| Toggle Item View | Enables periodic label refresh during pickup. |
| Toggle Item View Interval (ms) | Interval before the first and subsequent refresh pairs. |
| Toggle Items Hotkey | Key pressed twice for refresh, usually Z; not hardcoded. |
| Disable Clicking after Toggle Items (ms) | Extra block after both keypresses finish. |

Both hover checks default to enabled. Disabling them skips label-identity verification, retaining focus, hotkey, UI, movement and target-validity guards. Cursor-settle delays remain. Releasing pickup stops new commands but does not cancel walking already accepted by the game.

## Diagnostics and tests

Host logs: `Logs/ClickItMax/pickup-*.jsonl`. Look for session-start/end, click-sent, motion/rejection records and toggle-items. Dispatch is not proof of collection. Hotkey edges are sampled observations, not native input timestamps.

Independent movement traces need no game or host:

```powershell
dotnet run --project .scripts/MaxPickupPolicyTests/MaxPickupPolicyTests.csproj -c Release
```

Focused regressions require compatible host libraries:

```powershell
dotnet test Tests/ClickItMax.Tests.csproj -c Release -p:exapiPackage="D:\ExileAPI" --filter "FullyQualifiedName~InputHandler|FullyQualifiedName~InteractionExecutor|FullyQualifiedName~ClickLabelInteractionServiceTests|FullyQualifiedName~InteractionExecutionRuntimeTests|FullyQualifiedName~LabelSelectionServiceTests|FullyQualifiedName~LabelSelectionEngineTests|FullyQualifiedName~PluginLoopHostTests"
```

The test assembly disables native mouse input; periodic refresh tests use fake clocks and keyboard delegates. The publication snapshot passed 179 focused tests and 23 movement traces locally. The complete inherited suite is not claimed to pass. CI runs only independent motion traces without game or proprietary host binaries.

## Credits and terms

Original ClickIt: **Barragek0 and upstream contributors**. Original documentation: [README.upstream.md](README.upstream.md). Fork maintained for **EazzzyMax**, with implementation and investigation assisted by OpenAI Codex (GPT-6). No new generated art or audio is included.

Upstream supplied no repository LICENSE at the pinned revision. This fork does not assign a new license to upstream code or third-party libraries. Inherited ThirdParty libraries and alert.wav are unchanged from upstream. No game files, local decompilation output, machine settings or private diagnostic sessions were added.

See [CHANGELOG.md](CHANGELOG.md) and the [Russian overview](README-Max.md).
