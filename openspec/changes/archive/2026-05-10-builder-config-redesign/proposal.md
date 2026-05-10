## Why

The current DSL builder is fragile in two ways: (1) state handlers registered in disconnected style
are silently dropped because the `StateConfigBuilder` is abandoned before `Flush()` is called, and
(2) calling `In<S>()` more than once in a chain produces duplicate `StateHandler` entries, causing
`OnEnter` to fire in reverse-declaration order while `OnExit` fires forward — an inconsistency rooted
in `InReverse()` being designed for hierarchy, not for same-level duplicates. Both bugs share a root
cause: builders own the mutable state instead of the machine config.

## What Changes

- `MachineConfigBuilder` replaces its flat `List<StateHandler>` and `List<EventHandler>` with a
  `Dictionary<Type, StateHandlerConfig>` — one live entry per state type, created on demand.
- `StateHandlerConfig` holds the combined `OnEnter`/`OnExit` delegates **and** a
  `List<EventHandlerConfig>` for event handlers; it is the single source of truth for each state.
- `In<S>()` becomes idempotent — every call for the same type returns a typed cursor into the same
  `StateHandlerConfig` entry; no `Flush()` is needed and no state is ever lost.
- `OnEnter` / `OnExit` mutate the `StateHandlerConfig` entry directly; `StateHandlerConfig` and its
  `Combine()` logic move from the builder into the config object.
- `On<E>()` appends a **new** `EventHandlerConfig` to the state entry's list (marked incomplete).
- `GoTo` / `Stay` mark the pending `EventHandlerConfig` complete (set its action).
- `Build()` snapshots all live configs into frozen handlers and throws
  `IncompleteEventHandlerException` if any `EventHandlerConfig` still has no action. **BREAKING**
  (new exception type).
- `Build()` is non-destructive — calling it multiple times on the same machine config produces
  independent `MachineDefinition` instances reflecting the config's state at each call.
- `StateConfigBuilder` loses all internal mutable state; it becomes a pure typed cursor.
- `Flush()` is removed entirely from the builder.

## Capabilities

### New Capabilities

- `incomplete-handler-detection`: `Build()` validates that every `On<E>()` call was terminated with
  `GoTo` or `Stay`; throws a descriptive exception naming the incomplete state/event types.

### Modified Capabilities

- `dsl-builder`: Requirements updated for idempotent `In<S>()`, non-destructive `Build()`, and the
  new disconnect-safe registration model. The requirement "chained and disconnected styles produce
  identical registrations" is strengthened to include state lifecycle handlers.

## Impact

- `src/K4os.Stateful/Configuration/MachineConfigBuilder.cs` — primary rewrite.
- `src/K4os.Stateful/Configuration/StateConfigBuilder.cs` — loses internal `_config` field and
  `Flush()`; becomes a thin cursor.
- `src/K4os.Stateful/Configuration/StateHandlerConfig.cs` — gains `Combine()` logic and owns
  `List<EventHandlerConfig>`.
- `src/K4os.Stateful/Configuration/EventConfigBuilder.cs` — `Commit()` changes target from machine
  to state config entry.
- New file: `src/K4os.Stateful/Runtime/IncompleteEventHandlerException.cs`.
- `src/K4os.Stateful.Tests/DslBuilderTests.cs` — new tests for idempotency, incomplete handler
  detection, non-destructive build, and disconnect-safe registration.
- `src/K4os.Stateful.Tests/ExecutorTests.cs` — fix the test that silently passes despite handler
  being dropped; add ordering regression tests.
