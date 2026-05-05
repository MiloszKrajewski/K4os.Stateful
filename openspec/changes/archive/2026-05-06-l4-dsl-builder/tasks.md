## 1. Rename TransitionRule → EventHandler

- [x] 1.1 Rename `Internal/TransitionRule.cs` to `Internal/EventHandler.cs`; rename class to `EventHandler`; keep existing 4 readonly fields and constructor unchanged
- [x] 1.2 Add `EventHandler<TContext, TState, TEvent>` as a generic sealed subclass in the same file; constructor takes all 4 ranking fields plus `Guard` and `Action` delegates; all fields readonly
- [x] 1.3 Rename `Internal/TransitionRuleRanker.cs` to `Internal/EventHandlerRanker.cs`; rename class to `EventHandlerRanker`; update all internal references
- [x] 1.4 Rename `TransitionRuleRankerTests.cs` to `EventHandlerRankerTests.cs`; update class name and any `TransitionRule` constructor calls to use base `EventHandler` (no delegate args needed for ranking tests)
- [x] 1.5 Update `MachineDefinition<C,S,E>`: change `_rules` field type from `TransitionRule[]` to `EventHandler<C,S,E>[]`; update cache type and `GetRankedHandlers` return type accordingly
- [x] 1.6 Update `MachineDefinitionTests.cs`: update `Rule()` helper to return `EventHandler<Ctx, State, Event>` with null delegates; verify all tests still pass

## 2. Activation Types

- [x] 2.1 Create `Activation.cs` with two public sealed classes: `Activation<TContext, TCurrentState>` (Context, State, CancellationToken) and `Activation<TContext, TCurrentState, TCurrentEvent>` (Context, State, Event, CancellationToken); all properties readonly, set via constructor

## 3. StateHandler Types

- [x] 3.1 Create `Internal/StateHandler.cs` with `StateHandler<TContext, TState>` (sealed): `StateType`, `DeclarationOrder`, `OnEnter` (`Func<Activation<TContext, TState>, ValueTask>?`), `OnExit` (`Func<Activation<TContext, TState>, ValueTask>?`); all readonly, set in constructor
- [x] 3.2 Create `Internal/StateHandlerConfig.cs` with `StateHandlerConfig<TContext, TState>` (sealed): mutable `OnEnter` (`Func<Activation<TContext, TState>, ValueTask>?`) and `OnExit` (`Func<Activation<TContext, TState>, ValueTask>?`) fields, already base-typed (wrapping from concrete `TCS` happens in `StateConfigBuilder<TCS>` before storing here); `ToFrozen(Type stateType, int declarationOrder)` returns `StateHandler<TContext, TState>`

## 4. EventHandlerConfig

- [x] 4.1 Create `Internal/EventHandlerConfig.cs` with `EventHandlerConfig<TContext, TState, TEvent>` (sealed): mutable fields for `StateType`, `EventType`, `Guard`, `Action`, `DeclarationOrder`; `ToFrozen()` method that returns `EventHandler<TContext, TState, TEvent>`

## 5. DSL Interfaces

- [x] 5.1 Create `StateMachine.cs` with the static `StateMachine` class and a static `Define<TContext, TState, TEvent>()` factory method
- [x] 5.2 Create `IMachineConfig.cs` (or nest inside `StateMachine<C,S,E>`): `IMachineConfig` with `In<TCS>()` and `Build()`
- [x] 5.3 Create `IStateConfig.cs`: `IStateConfig<TCurrentState>` with `OnEnter()`, `OnExit()`, `On<TCE>()`, `In<TNextState>()`, `Build()`
- [x] 5.4 Create `IEventConfig.cs`: `IEventConfig<TCS, TCE>` with `When()` (returns `IEventConfig<TCS, TCE>` — same interface, enables chaining), `GoTo()`, and `Stay()`; no `IGuardedEventConfig`

## 6. Builder Implementations

- [x] 6.1 Create `Internal/MachineConfigBuilder.cs`: implements `IMachineConfig`; holds `List<EventHandler<C,S,E>>` and `List<StateHandler<C,S>>`; tracks monotonic declaration order counter; `In<TCS>()` creates a `StateConfigBuilder` and flushes any prior state config; `Build()` converts all configs to frozen arrays and returns `MachineDefinition<C,S,E>`
- [x] 6.2 Create `Internal/StateConfigBuilder.cs`: implements `IStateConfig<TCS>`; holds reference back to `MachineConfigBuilder` and a `StateHandlerConfig<C,S,TCS>`; `OnEnter(fn)` / `OnExit(fn)` set the mutable config fields; `On<TCE>()` creates and returns an `EventConfigBuilder`; `In<TNext>()` flushes current state config and delegates to `MachineConfigBuilder.In<TNext>()`; `Build()` delegates to `MachineConfigBuilder.Build()`
- [x] 6.3 Create `Internal/EventConfigBuilder.cs`: implements `IEventConfig<TCS,TCE>`; holds reference to `StateConfigBuilder` and an `EventHandlerConfig`; `When(guard)` ANDs the guard into the accumulated guard list and returns `this`; `GoTo(action)` / `Stay()` / `Stay(action)` set the action, call `ToFrozen()`, register the handler, and return `IStateConfig<TCS>`

## 7. Extension Methods

- [x] 7.1 Create `StateMachineExtensions.cs`: add sync and `Task<>`-returning overloads for `When`, `GoTo`, `Stay` on `IEventConfig<TCS,TCE>`; add state-only `GoTo(Func<TCS, TState>)` shorthand; add sync `OnEnter` / `OnExit` overloads on `IStateConfig<TCS>`

## 8. Update MachineDefinition

- [x] 8.1 Add `StateHandler<C,S>[]` field `_stateHandlers` and a corresponding `ConcurrentDictionary` cache to `MachineDefinition<C,S,E>`; update constructor to accept both arrays; expose `GetSortedStateHandlers(Type actualStateType)` returning the cached per-actual-state slice — handlers whose `StateType` is assignable from `actualStateType`, sorted by ascending distance (most derived first); OnEnter caller iterates in reverse (base → derived), OnExit caller iterates forward (derived → base)

## 9. Tests — DSL / Builder

- [x] 9.1 Create `DslBuilderTests.cs`: cover all spec scenarios — `GoTo` registers correct `EventHandler` fields; `When` sets `HasGuard`; `Stay()` registers identity action; `Stay(cb)` registers callback action; `OnEnter` / `OnExit` register correct `StateHandler` fields; chained and disconnected styles produce identical registrations; `Build()` returns immutable definition
- [x] 9.2 Add test confirming `.When(g1).When(g2).GoTo(...)` fires only when both guards pass; verify AND semantics with one guard passing and one failing
- [x] 9.3 Create `ActivationTests.cs`: verify `Activation<C,S,E>` and `Activation<C,S>` constructors set all properties correctly; verify `CancellationToken` flows through

## 10. Verify

- [x] 10.1 Run full solution test suite; all tests pass with zero warnings introduced by the rename
