## ADDED Requirements

### Requirement: IMachineConfig exposes WithStateChangeIf for predicate configuration
`IMachineConfig` SHALL expose `WithStateChangeIf(Func<TState, TState, bool> predicate)` returning
`IMachineConfig` for fluent chaining. The predicate SHALL be copied into the `MachineDefinition`
at `Build()` time. Calling `WithStateChangeIf` more than once SHALL replace the previous predicate
(last write wins). If never called, the default predicate `!ReferenceEquals(s1, s2)` SHALL be used.

#### Scenario: WithStateChangeIf stores predicate in MachineDefinition
- **WHEN** `.WithStateChangeIf((s1, s2) => s1.GetType() != s2.GetType()).Build()` is called
- **THEN** the returned `MachineDefinition` uses that predicate for all executors created from it

#### Scenario: Default predicate is ReferenceEquals negation
- **WHEN** `Build()` is called without calling `WithStateChangeIf`
- **THEN** the `MachineDefinition` uses `!ReferenceEquals(s1, s2)` as the state-change predicate

#### Scenario: WithStateChangeIf returns IMachineConfig for chaining
- **WHEN** `.WithStateChangeIf(pred).In<SomeState>().On<SomeEvent>().GoTo(...)` is called
- **THEN** the chain compiles and both the predicate and the handler are registered
