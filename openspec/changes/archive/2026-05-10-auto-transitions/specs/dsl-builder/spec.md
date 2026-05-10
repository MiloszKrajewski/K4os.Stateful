## ADDED Requirements

### Requirement: Auto registers a completion transition on a state
`IStateConfig<TCurrentState>.Auto(handler)` SHALL register a completion transition handler
for the current state type. The primary overload SHALL accept
`Func<Activation<TContext, TCurrentState>, ValueTask<TState>>`. Interface default implementations
SHALL provide sync (`TState`) and `Task<TState>` overloads. `.Auto()` SHALL return
`IMachineConfig` (not `IStateConfig`); continued event-handler chaining on the same state type
requires a new `In<S>()` call (disconnected style).

#### Scenario: Auto registers StateHandler with Auto delegate
- **WHEN** `In<RouterState>().Auto(x => new TargetState())` is called
- **THEN** the `StateHandler` for `RouterState` has a non-null `Auto` delegate

#### Scenario: Auto returns IMachineConfig allowing further state definitions
- **WHEN** `In<S>().Auto(handler)` is called
- **THEN** `IMachineConfig` is returned, from which `.In<T>()` or `.Build()` can be called; to add event handlers on the same state, open a new `In<S>()` block

#### Scenario: Sync Auto overload is accepted and wraps correctly
- **WHEN** `.Auto(x => new TargetState())` is used (sync return, not ValueTask)
- **THEN** the registration succeeds and the stored delegate wraps the sync lambda in `ValueTask<TState>`
