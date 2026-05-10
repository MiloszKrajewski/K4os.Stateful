# Spec: Incomplete Handler Detection

## Purpose

Defines the validation behavior of `Build()` when event handler chains are left in an incomplete
state — i.e., `On<E>()` was called but neither `GoTo` nor `Stay` was subsequently provided.
Ensures that no silent no-ops or partially constructed handlers can make it into a frozen
`MachineDefinition`.

## Requirements

### Requirement: Build() throws on incomplete event handler chains

If `On<E>()` was called but neither `GoTo` nor `Stay` was subsequently called to provide a
transition action, the `EventHandlerConfig` entry SHALL be considered incomplete. `Build()` SHALL
throw `IncompleteEventHandlerException` naming the incomplete state type and event type.
`IncompleteEventHandlerException` SHALL carry `StateType` and `EventType` properties.

The check SHALL be performed before any frozen handlers are returned; no partial `MachineDefinition`
is produced.

#### Scenario: Build() throws when On<E> has no terminal

- **WHEN** `In<S>().On<E>()` is called with no subsequent `GoTo` or `Stay`, and then `Build()` is called
- **THEN** `Build()` throws `IncompleteEventHandlerException` with `StateType = typeof(S)` and `EventType = typeof(E)`

#### Scenario: Build() succeeds when all On<E> chains are terminated

- **WHEN** every `On<E>()` call is followed by `GoTo` or `Stay` before `Build()` is called
- **THEN** `Build()` returns a valid `MachineDefinition` with no exception

#### Scenario: Build() throws even when On<E> has guards but no terminal

- **WHEN** `In<S>().On<E>().When(guard)` is called with no subsequent `GoTo` or `Stay`, and then `Build()` is called
- **THEN** `Build()` throws `IncompleteEventHandlerException`

#### Scenario: Subsequent Build() after completing the chain succeeds

- **WHEN** `Build()` throws for an incomplete handler, then `GoTo` or `Stay` is called on the pending config, and `Build()` is called again
- **THEN** the second `Build()` succeeds and returns a valid `MachineDefinition`
