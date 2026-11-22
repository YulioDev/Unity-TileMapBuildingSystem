# TMBS - TileMap Building System

Modular Unity tilemap building framework organized around a decoupled core pipeline, configurable runtime composition, and Unity UI/Editor tooling.

## Architecture Overview

| Component | Namespace | Role |
|---|---|---|
| `BuildeableTilemap` | `TMBS.Runtime.Interface` | Scene `MonoBehaviour` facade that binds `TmbsRootConfig`, scene `Tilemap` references, selection state, input adapters, `IEventBus`, and `IPendingWorkApi`. |
| `TmbsRootConfig` | `TMBS.Runtime.Config` | `ScriptableObject` holding execution mode (`Immediate` or `Pending`), input configuration, camera policy, preview settings, history/metadata capacities, pending settings, and ordered `IValidator` entries. |
| `TmbsFactory` | `TMBS.Runtime.Interface` | Composition root (`Compose(...)`) that instantiates and wires `TmbsSession` dependencies from `TmbsRootConfig` and scene references. |
| `IBuildInputAdapter` | `TMBS.Core.Input` | Input abstraction exposing `InputCapabilities`, `BuildIntentRaised`, `Enable()`, and `Disable()`. Implemented by `LegacyMouseAdapter`, `InputSystemAdapter`, or custom external adapters. |
| `IBuildPipeline` | `TMBS.Core.Pipeline` | Processes `BuildIntent` values into a `PipelineContext` by resolving grid cells, drag bounds or blueprint footprints, placement transforms, validation, and routing to an `ExecutionDecision`. |
| `IValidator` | `TMBS.Core.Validation` | Validation stage (`Validate(in PipelineContext, ValidationMode)`) executed sequentially by `ValidatorPipeline` for `Quick` (preview) and `Full` (confirm) checks. |
| `IBuildExecutor` | `TMBS.Core.Execution` | Applies accepted `PipelineContext` decisions to the target `Tilemap`. Implemented by `ImmediateBuildExecutor` and `PendingBuildExecutor`. |
| `IPendingWorkApi` | `TMBS.Core.Pending` | Runtime API for querying and advancing deferred construction orders (`TryDeliverResources`, `TryAddWork`, `TryCompleteCell`, `TryCancel`, `TryGetCellStatus`, `GetAll`). |

## Folder Structure

- `Runtime/Core/` - Pure pipeline, intent, selection, grid, validation, execution, history, metadata, pending, focus, and event-bus contracts.
- `Runtime/Config/` - `ScriptableObject` assets (`TmbsRootConfig`, `TmbsBuildableData`) and serializable configuration models.
- `Runtime/Unity/` - Unity-specific grid, tilemap occupancy, preview rendering, and input adapter implementations (`LegacyMouseAdapter`, `InputSystemAdapter`, `UiFocusGuard`).
- `Runtime/Factory/` - `TmbsFactory` dependency composition.
- `Runtime/Interface/` - `BuildeableTilemap` facade and `TmbsSession` container.
- `Runtime/Debug/` - Optional IMGUI overlay (`TmbsDebugMenu`) for toggling execution modes and stepping pending orders.
- `UI/` - `TmbsBuildingUI`, `TmbsPaletteSlot`, and UI prefabs subscribed to `IEventBus` events.
- `Editor/` - Scene UI builder (`Tools/TMBS/*`), custom inspectors (`TmbsRootConfigEditor`, `TmbsBuildableDataEditor`), and `TmbsInputConfiguratorWindow` (`Window/TMBS/TMBS Settings` and `Window/TMBS/Buildables`).

## Execution Flow

1. An `IBuildInputAdapter` translates pointer and keyboard/action input into `BuildIntent` events (respecting `IInputFocusService`).
2. `BuildeableTilemap` routes each `BuildIntent` through `IBuildPipeline.Process`, updating preview rendering on pointer/drag updates and invoking `IBuildExecutor.Execute` on confirmed operations.
3. In `ExecutionMode.Immediate`, `ImmediateBuildExecutor` writes tiles and metadata via undoable `PlaceTilesCommand` operations.
4. In `ExecutionMode.Pending`, `PendingBuildExecutor` records deferred construction orders in `IPendingStore`, which external gameplay systems complete through `IPendingWorkApi`.
5. `TmbsBuildingUI` listens to `SelectionChangedEvent`, `ValidationFailedEvent`, and pending order events to synchronize the palette, HUD, and feedback messages.
