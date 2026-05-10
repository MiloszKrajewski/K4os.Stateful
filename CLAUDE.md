# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

Standard .NET SDK. Solution: `src/K4os.Stateful.sln`.

```
dotnet build src/K4os.Stateful.sln
dotnet test src/K4os.Stateful.sln
dotnet pack src/K4os.Stateful.sln
```

Single test class/method:
```
dotnet test src/K4os.Stateful.Tests/K4os.Stateful.Tests.csproj --filter "FullyQualifiedName~ExecutorTests"
dotnet test src/K4os.Stateful.Legacy.Tests/K4os.Stateful.Legacy.Tests.csproj --filter "FullyQualifiedName~CalculatorTests"
```

## Project Layout

- `K4os.Stateful` — new async implementation (active development)
- `K4os.Stateful.Tests` — tests for the new implementation
- `K4os.Stateful.Legacy` — original synchronous implementation
- `K4os.Stateful.Legacy.Tests` — tests for the legacy implementation

## Core Concept

A **hierarchical state machine** where states and events are typed objects (not enums), with polymorphic handler matching via class inheritance. A handler registered for `Animal` fires when the current state is `Dog : Animal` (distance = 1); a handler for `Dog` fires at distance 0 and takes priority. This applies to both state and event types independently.

```
TContext  — shared services/infrastructure injected per-executor
TState    — base type for all states (states carry data as fields/properties)
TEvent    — base type for all events (events carry data as fields/properties)
```

---

## New API (`K4os.Stateful`)

### Configuration DSL

Entry point: `StateMachine.Configure<TContext, TState, TEvent>()` returns `IMachineConfig`.

```csharp
var definition = StateMachine.Configure<Ctx, IState, IEvent>()
    .In<StateA>()
        .OnEnter(x => { /* x.Context, x.State, x.CancellationToken */ })
        .OnExit(x => { })
        .On<EventX>()
            .When(x => x.State.Flag)
            .GoTo(x => new StateB())
    .In<StateB>()
        .On<EventY>()
            .Stay()   // no state change; OnExit/OnEnter not called
    .Build();
```

`Build()` returns a frozen `MachineDefinition<TContext, TState, TEvent>` safe to share across threads.

### DSL interfaces (nested in `StateMachineConfig<,,>`)

- `IMachineConfig` — `.WithStateChangeIf(pred)`, `.In<T>()`, `.Build()`
- `IStateConfig<TCurrentState>` — `.OnEnter(...)`, `.OnExit(...)`, `.On<TEvent>()`, `.In<T>()`, `.Build()`
- `IEventConfig<TCurrentState, TCurrentEvent>` — `.When(...)`, `.GoTo(...)`, `.Stay(...)`

All callbacks have three overloads: `Func<…, ValueTask>`, `Func<…, Task>`, and `Action<…>` (or `Func<…, bool>` for `.When`).  
`.When(...)` calls accumulate as AND logic.  
`.GoTo(...)` returns `IStateConfig<TCurrentState>` — enables chaining more handlers on the same state.  
`.WithStateChangeIf(pred)` sets a custom `Func<TState, TState, bool>` predicate (default: `!ReferenceEquals`).

### Activation bundles

- `Activation<TContext, TState>` — passed to `OnEnter`/`OnExit`; exposes `.Context`, `.State`, `.CancellationToken`
- `Activation<TContext, TState, TEvent>` — passed to event handlers; adds `.Event`

### Handler ranking (`Runtime/EventHandlerRanker.cs`)

Sort key (ascending = higher priority):

1. State type distance (most derived wins)
2. State is interface (class beats interface at same distance)
3. Event type distance (most derived wins)
4. Event is interface (class beats interface at same distance)
5. Has no guard (guarded handlers rank before unguarded at same specificity)
6. Declaration order (stable tie-break)

State handlers (`OnEnter`/`OnExit`) sorted ascending by distance — most-derived at index 0. `OnExit` iterates forward (derived→base); `OnEnter` iterates in reverse (base→derived).

### Type distance (`Runtime/TypeExtensions.cs`)

`DistanceFrom(Type parent)` — cached in a static `ConcurrentDictionary`. Class-to-class: walks `BaseType` chain. Class-to-interface: finds the class in the chain that first introduces the interface via set-difference. Interface-to-interface: throws `InvalidOperationException` (not implemented; runtime types are always concrete classes).

### Execution

`definition.Create(context, initialState)` returns a `MachineExecutor<TContext, TState, TEvent>`.

```csharp
var executor = definition.Create(ctx, new IdleState());
await executor.FireAsync(new StartEvent());          // throws UnhandledEventException on no match
bool matched = await executor.TryFireAsync(new UnknownEvent());  // returns false on no match
executor.State;  // current state
```

Sync wrappers: `Fire` / `TryFire` (via `.GetAwaiter().GetResult()`).

**Transition pipeline inside `FireAsync`:**
1. `Interlocked.CompareExchange` sets in-flight flag — throws `ConcurrentFireException` if already set
2. Ranked handlers for `(currentState.GetType(), event.GetType())` retrieved from cache
3. Guards evaluated in order; first passing handler short-circuits
4. Handler action invoked → `nextState`; if it throws, `_state` is not written
5. State-change predicate evaluated: if `true`, `OnExit` fires then `_state` updated then `OnEnter` fires; if `false`, `_state` updated silently
6. In-flight flag cleared in `finally`

**Domain exceptions:**
- `UnhandledEventException` — carries `.Event` and `.StateType`
- `ConcurrentFireException` — re-entrant fire on same executor
- `IncompleteEventHandlerException` — thrown by `Build()` when `.On<E>()` has no terminal (`.GoTo`/`.Stay`)

### MachineDefinition caching

Two lazy `ConcurrentDictionary` caches populated on first use:
- `_eventCache` — `(actualStateType, actualEventType)` → ranked `EventHandler[]`
- `_stateCache` — `actualStateType` → sorted `StateHandler[]`

### Folder layout (`src/K4os.Stateful/`)

- `Configuration/` — DSL builders: `StateMachine.Interfaces.cs`, `MachineConfigBuilder`, `StateConfigBuilder`, `EventConfigBuilder`, `StateHandlerConfig`, `EventHandlerConfig`
- `Runtime/` — execution: `MachineDefinition`, `MachineExecutor`, `EventHandler`, `StateHandler`, `Activation`, `EventHandlerRanker`, `TypeExtensions`, exception types
- `Internal/` — shared utilities (`Extensions.cs`)
- `StateMachine.cs` — static entry point (`Configure<,,>()`)

**Builder flush pattern:** `StateConfigBuilder` and `EventConfigBuilder` accumulate handlers into mutable config objects; calling `.In<T>()`, `.On<E>()`, or `.Build()` triggers `Flush()`, converting accumulated config to frozen handler records registered with `MachineConfigBuilder`.

---

## Legacy API (`K4os.Stateful.Legacy`)

```csharp
var config = StateMachine<Ctx, State, Event>.NewConfigurator();
config.In<State1>()
    .On<Event1>().When(c => c.State.Flag).Goto(c => new State2());
    // .Loop() instead of .Goto() to stay without lifecycle hooks

var executor = config.NewExecutor(context, initialState);  // extension in StateMachineExtenders.cs
executor.Fire(event);  // throws InvalidOperationException on ambiguity or no match
```

Context objects: `IStateContext<T>` (`.Context`, `.State`) and `IEventContext<TS, TE>` (adds `.Event`).

> Avoid registering handlers on interfaces in the legacy API — interface distance is ambiguous.

---

## Tests

**New** (`src/K4os.Stateful.Tests/`):
- `ExecutorTests.cs` — lifecycle, entry/exit ordering, predicate, guard walk, unhandled events, concurrency
- `DslBuilderTests.cs` — DSL wiring, AND guards, incomplete handler detection, build immutability
- `EventHandlerRankerTests.cs` — ranking algorithm: hierarchy, class vs interface
- `TypeExtensionsTests.cs` — distance algorithm correctness and caching
- `ActivationTests.cs` — activation type conversion helpers
- `MachineDefinitionTests.cs` — cache behavior

**Legacy** (`src/K4os.Stateful.Legacy.Tests/`):
- `CalculatorTests.cs` — end-to-end calculator state machine
- `KotlinTests.cs` — lifecycle ordering, hierarchical dispatch, guards
- `ConfigurationTests.cs` — API contract and error cases
