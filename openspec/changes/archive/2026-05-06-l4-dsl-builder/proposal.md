## Why

Layer 1 (type distance) and Layer 2 (event handler ranking) are complete. The next foundational
layer is the fluent DSL and the frozen definition types it produces — without these, no executor
can be built and no further layers can be tested end-to-end.

## What Changes

- **BREAKING** Rename `TransitionRule` → `EventHandler<TContext, TState, TEvent>` (now generic,
  adds `Guard` and `Action` delegate fields)
- **BREAKING** Rename `TransitionRuleRanker` → `EventHandlerRanker` (updated to work with generic type)
- Introduce `EventHandlerConfig<TContext, TState, TEvent>` — mutable accumulator for the
  `In→On→When→GoTo/Stay` chain; converted to frozen `EventHandler` at `Build()` time
- Introduce `StateHandler<TContext, TState>` — frozen lifecycle handler carrying optional `OnEnter`
  and `OnExit` delegates for one registered state type
- Introduce `StateHandlerConfig<TContext, TState, TCurrentState>` — mutable accumulator for the
  `In→OnEnter/OnExit` chain; converted to `StateHandler` at `Build()` time
- Introduce `Activation<TContext, TCurrentState, TCurrentEvent>` — typed lambda bundle for
  `When` / `GoTo` / `Stay` callbacks
- Introduce `Activation<TContext, TCurrentState>` — typed lambda bundle for `OnEnter` / `OnExit` / `Auto`
- Implement DSL interfaces: `IMachineConfig`, `IStateConfig<TCS>`, `IEventConfig<TCS, TCE>`
  (nested inside `StateMachine<C,S,E>`); multiple `.When()` calls AND their guards — no separate
  `IGuardedEventConfig` needed
- Implement builder classes: `MachineConfigBuilder`, `StateConfigBuilder<TCS>`,
  `EventConfigBuilder<TCS, TCE>`
- Add sync and `Task<>`-returning extension method overloads for `When`, `GoTo`, `Stay`
- Update `MachineDefinition<TContext, TState, TEvent>` to store `EventHandler[]` and
  `StateHandler[]`; remove separate enter/exit arrays (single `_stateHandlers[]`, OnExit
  iterates in reverse)
- Update test file `TransitionRuleRankerTests` → `EventHandlerRankerTests`; update
  `MachineDefinitionTests` helper

## Capabilities

### New Capabilities

- `dsl-builder`: Fluent configuration DSL — `StateMachine.Define<C,S,E>()` entry point,
  `In<T>()`, `On<E>()`, `When()`, `GoTo()`, `Stay()`, `OnEnter()`, `OnExit()`, `Build()`
- `activation-context`: `Activation<TContext, TCurrentState, TCurrentEvent>` and
  `Activation<TContext, TCurrentState>` lambda bundle types

### Modified Capabilities

- `handler-ranking`: `TransitionRule` becomes `EventHandler<TContext, TState, TEvent>` (generic);
  ranker class renamed; ranking algorithm unchanged

## Impact

- `K4os.Stateful` — all Internal types touched; new public types (`Activation`, DSL interfaces)
- `K4os.Stateful.Tests` — two test files renamed/updated; new DSL test file added
- No executor yet — `Build()` produces a frozen `MachineDefinition`; execution is Layer 7+
