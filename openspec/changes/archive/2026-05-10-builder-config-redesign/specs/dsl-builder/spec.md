## ADDED Requirements

### Requirement: In<T> is idempotent — multiple calls target the same state entry

Every call to `In<S>()` for the same type `S` SHALL resolve to the same underlying
`StateHandlerConfig` entry. Handlers registered across separate `In<S>()` calls SHALL be
accumulated into a single state entry in declaration order, identical to registering them all in
one `In<S>()` block.

#### Scenario: OnEnter registered across two In<S>() blocks fires in declaration order

- **WHEN** `In<S>().OnEnter(X)` and a later `In<S>().OnEnter(Y)` are registered (whether chained or disconnected)
- **THEN** `OnEnter` fires X then Y (declaration order) — the same as `In<S>().OnEnter(X).OnEnter(Y)`

#### Scenario: OnExit registered across two In<S>() blocks fires in declaration order

- **WHEN** `In<S>().OnExit(X)` and a later `In<S>().OnExit(Y)` are registered
- **THEN** `OnExit` fires X then Y (declaration order)

#### Scenario: In<S>() in disconnected style accumulates correctly

- **WHEN** `config.In<S>().OnEnter(X)` and `config.In<S>().OnEnter(Y)` are called as separate statement expressions
- **THEN** both `OnEnter` callbacks are registered; neither is silently dropped

---

## MODIFIED Requirements

### Requirement: Chained and disconnected styles produce identical registrations

Registering handlers via a continuous fluent chain SHALL produce the same `MachineDefinition`
as registering them via separate `c.In<T>()...` calls in any order. This equivalence SHALL hold
for both event handlers **and** state lifecycle handlers (`OnEnter`/`OnExit`).

#### Scenario: Chained style matches disconnected style for event handlers

- **WHEN** two configurations register the same event handlers — one chained, one via separate `c.In<T>()` calls
- **THEN** both produce `MachineDefinition` instances with the same handler count, types, and declaration order

#### Scenario: Chained style matches disconnected style for state lifecycle handlers

- **WHEN** two configurations register `OnEnter` and `OnExit` for the same state — one chained, one via separate `c.In<T>()` calls
- **THEN** both produce `MachineDefinition` instances where the callbacks fire in the same order

---

### Requirement: Build() snapshots the current configuration

`IMachineConfig.Build()` and `IStateConfig.Build()` SHALL convert all accumulated
`EventHandlerConfig` and `StateHandlerConfig` instances into frozen `EventHandler<C,S,E>` and
`StateHandler<C,S>` objects and return an immutable `MachineDefinition<C,S,E>`.

`Build()` SHALL be non-destructive: calling it multiple times on the same config object SHALL
produce independent `MachineDefinition` instances, each reflecting the state of the configuration
at the time of that call. The config object remains fully usable after `Build()`.

After `Build()` the returned definition SHALL be safe to share across concurrent executor instances
with no locking.

`Build()` SHALL throw `IncompleteEventHandlerException` if any `On<E>()` chain has no `GoTo` or
`Stay` terminal (see `incomplete-handler-detection` spec).

#### Scenario: Build returns immutable MachineDefinition

- **WHEN** `Build()` is called after registering handlers
- **THEN** a `MachineDefinition<C,S,E>` is returned with all handlers frozen as readonly fields

#### Scenario: Multiple Build() calls produce independent definitions

- **WHEN** `Build()` is called twice on the same configurator
- **THEN** two independent `MachineDefinition` instances are returned with independent caches

#### Scenario: Config mutated between two Build() calls produces different definitions

- **WHEN** `Build()` is called, then a new handler is registered on the same config, then `Build()` is called again
- **THEN** the first definition does not contain the new handler; the second definition does

#### Scenario: Build() throws on incomplete handler then succeeds after completion

- **WHEN** `Build()` throws `IncompleteEventHandlerException`, the incomplete chain is terminated, and `Build()` is called again
- **THEN** the second call returns a valid `MachineDefinition`
