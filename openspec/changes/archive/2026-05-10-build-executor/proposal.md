## Why

Layers 1–4 (distance algorithm, handler ranking, guard combining, DSL/builder) are complete, but there is no executor — nothing that actually runs a state machine. Without it the library cannot be used end-to-end.

## What Changes

- Add `WithStateChangeIf` to `IMachineConfig` (stored into `MachineDefinition` at `Build()` time; default `!ReferenceEquals`)
- Add `Create()` to `MachineDefinition<TContext,TState,TEvent>` returning a new `MachineExecutor`
- Introduce `MachineExecutor<TContext,TState,TEvent>` with:
  - `Start(context, state)` — positions without firing `OnEnter`
  - `State` property — current state
  - `FireAsync(event, ct)` / `TryFireAsync(event, ct)` — primary async API
  - `Fire(event, ct)` / `TryFire(event, ct)` — sync convenience wrappers
- Introduce `UnhandledEventException` — thrown by `FireAsync`/`Fire` when no handler matches
- Introduce `ConcurrentFireException` — thrown when `FireAsync` is called concurrently on the same executor
- Add unit tests covering all executor behaviour (Layers 5–9)

## Capabilities

### New Capabilities

- `machine-executor`: Executor lifecycle (`Start`, `State`), event firing (`FireAsync`/`TryFireAsync`), entry/exit firing order, state-change predicate, guard walk and short-circuit, unhandled-event handling, and concurrent-fire detection.

### Modified Capabilities

- `dsl-builder`: `IMachineConfig` gains `WithStateChangeIf`; `MachineDefinition` gains `Create()` and stores the predicate.

## Impact

- New file: `src/K4os.Stateful/Runtime/MachineExecutor.cs`
- New file: `src/K4os.Stateful/Runtime/UnhandledEventException.cs`
- New file: `src/K4os.Stateful/Runtime/ConcurrentFireException.cs`
- Modified: `src/K4os.Stateful/Configuration/StateMachine.Interfaces.cs` — `WithStateChangeIf` on `IMachineConfig`
- Modified: `src/K4os.Stateful/Runtime/MachineDefinition.cs` — stores predicate, exposes `Create()`
- New file: `src/K4os.Stateful.Tests/ExecutorTests.cs`
