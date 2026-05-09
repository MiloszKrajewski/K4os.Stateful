# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build System

Standard .NET SDK project targeting `net8.0`. Solution: `src/K4os.Stateful.sln`.

```
dotnet build src/K4os.Stateful.sln          # Build
dotnet test src/K4os.Stateful.sln           # Run all tests
dotnet pack src/K4os.Stateful.sln           # Pack NuGet packages
```

To run a single test class or method:
```
dotnet test src/K4os.Stateful.Legacy.Tests/K4os.Stateful.Legacy.Tests.csproj --filter "FullyQualifiedName~CalculatorTests"
dotnet test src/K4os.Stateful.Tests/K4os.Stateful.Tests.csproj --filter "FullyQualifiedName~EventHandlerRankerTests"
```

## Project Layout

Four projects in the solution:

- `K4os.Stateful.Legacy` — original synchronous implementation
- `K4os.Stateful.Legacy.Tests` — tests for the legacy implementation
- `K4os.Stateful` — new async rewrite (in active development, **not** a stub)
- `K4os.Stateful.Tests` — tests for the new implementation

## Shared Concept

K4os.Stateful is a **hierarchical state machine library** for .NET. Both states and events are typed objects (not enums), enabling polymorphic handler matching via class inheritance. A handler registered for `Animal` fires when the current state is `Dog : Animal` (distance = 1); a handler for `Dog` fires at distance 0 and takes priority. This applies to both state and event types independently.

```
TContext  — shared data accessible in all callbacks
TState    — base class for all states (states can carry data as fields)
TEvent    — base class for all events (events can carry data as fields)
```

---

## New API (`K4os.Stateful`)

### Configuration DSL

Entry point: `StateMachine<TContext, TState, TEvent>.Define()` returns `IMachineConfig`.

```csharp
var definition = StateMachine<Ctx, IState, IEvent>.Define()
    .In<StateA>()
        .OnEnter(x => { /* x.Context, x.State, x.CancellationToken */ })
        .OnExit(x => { })
        .On<EventX>()
            .When(x => x.State.Flag)
            .GoTo(x => new StateB())
    .In<StateB>()
        .On<EventY>()
            .Stay()   // no transition; OnExit/OnEnter not called
    .Build();
```

`Build()` returns a frozen, immutable `MachineDefinition<TContext, TState, TEvent>` that is safe to share across threads. All state in the DSL builders is discarded after `Build()`.

### DSL interfaces (nested in `StateMachine<,,>`)

- `IMachineConfig` — `.In<T>()`, `.Build()`
- `IStateConfig<TCurrentState>` — `.OnEnter(...)`, `.OnExit(...)`, `.On<TEvent>()`, `.In<T>()`, `.Build()`
- `IEventConfig<TCurrentState, TCurrentEvent>` — `.When(...)`, `.GoTo(...)`, `.Stay(...)`

All callbacks have three overloads: `Func<…, ValueTask>`, `Func<…, Task>`, and `Action<…>`.  
`.When(...)` calls accumulate as AND logic (all guards must pass).  
`.GoTo(...)` accepts sync or async functions returning `TState`.  
`.Stay(...)` stays in the same state without triggering `OnExit`/`OnEnter`.

### Context objects

- `Activation<TContext, TState>` — passed to `OnEnter`/`OnExit`; exposes `.Context`, `.State`, `.CancellationToken`
- `Activation<TContext, TState, TEvent>` — passed to event handlers; adds `.Event`

### Handler ranking (new API)

Implemented in `Runtime/EventHandlerRanker.cs`. Sort key (ascending = higher priority):

1. State type distance (most derived wins)
2. State is interface (class beats interface at same distance)
3. Event type distance (most derived wins)
4. Event is interface (class beats interface at same distance)
5. Has no guard (guarded handlers rank before unguarded at same specificity)
6. Declaration order (registration order breaks remaining ties)

State handlers for `OnEnter`/`OnExit` are sorted ascending by distance (most-derived first = index 0). `OnExit` iterates forward; `OnEnter` iterates in reverse (base class fires first on enter, derived class fires first on exit).

### Type distance (`Runtime/TypeExtensions.cs`)

`DistanceFrom(Type parent)` — cached in a static `ConcurrentDictionary`. Walks the class chain for parent classes; for interfaces, finds the class in the chain that first introduces the interface via set-difference. Interface-to-interface distance is not implemented.

### MachineDefinition caching

`MachineDefinition` holds two `ConcurrentDictionary` caches that start empty and populate lazily on first executor use:

- `_eventCache` — `(actualStateType, actualEventType)` → ranked `EventHandler[]`
- `_stateCache` — `actualStateType` → sorted `StateHandler[]`

### Folder layout (`src/K4os.Stateful/`)

- `Configuration/` — DSL builder classes: `StateMachine.Interfaces.cs`, `MachineConfigBuilder`, `StateConfigBuilder`, `EventConfigBuilder`, `StateHandlerConfig`, `EventHandlerConfig`
- `Runtime/` — execution-time classes: `MachineDefinition`, `EventHandler`, `StateHandler`, `Activation`, `EventHandlerRanker`, `TypeExtensions`
- `Internal/` — shared utilities (`Extensions.cs`)
- `StateMachineConfig.cs` — `StateMachine<,,>` entry point (`.Define()`)

### Internal builder flow

1. `StateConfigBuilder<TCurrentState>` accumulates `OnEnter`/`OnExit` delegates into a mutable `StateHandlerConfig`, combining them sequentially.
2. Calling `.In<TNext>()` or `.Build()` triggers `Flush()`, which converts the accumulated config to a frozen `StateHandler` and registers it with `MachineConfigBuilder`.
3. `EventConfigBuilder` similarly accumulates guards and a transition action into `EventHandlerConfig`, flushed to `EventHandler` when the event scope closes.

---

## Legacy API (`K4os.Stateful.Legacy`)

The entire library lives in `StateMachine<TContext, TState, TEvent>` split across partial class files.

### Configuration

```csharp
var config = StateMachine<Ctx, State, Event>.NewConfigurator();
config.In<State1>()
    .OnEnter(c => { /* c.Context, c.State */ })
    .OnExit(c => { })
    .On<Event1>()
        .When(c => c.State.Flag)
        .Goto(c => new State2())
        .Loop();  // stay, skip OnExit/OnEnter
```

- `_states`: `Type → config`
- `_events`: `EventKey → List<config>` (struct key = stateType + eventType)

### Execution

`configurator.NewExecutor(context, initialState)` (extension method in `StateMachineExtenders.cs`).

`executor.Fire(event)`:
1. Resolves handlers by type inheritance distance
2. Evaluates `.When(...)` guards
3. Runs all `.OnTrigger(...)` handlers
4. Selects single closest-match transition; throws `InvalidOperationException` on ambiguity or no match
5. Executes `OnExit → Goto → OnEnter` (`.Loop()` skips lifecycle hooks)

Context objects: `IStateContext<TActualState>` (`.Context`, `.State`) and `IEventContext<TActualState, TActualEvent>` (adds `.Event`).

Internal utilities: `EventKey.cs` (dict key struct), `CollectionExtender.cs` (`TryGet` with `TryGetMode`), `ContractExtender.cs` (validation), `ReflectionExtender.cs` (`TypeDistance()` with `CachedTypeDistanceMap`).

> Avoid registering handlers on interfaces in the legacy API — interface distance is ambiguous (assumes longest inheritance path).

---

## Tests

**Legacy** (`src/K4os.Stateful.Legacy.Tests/`):
- `CalculatorTests.cs` — end-to-end: expression calculator state machine
- `ConfigurationTests.cs` — API contract and error cases
- `KotlinTests.cs` — lifecycle ordering, hierarchical dispatch, guards, loop vs goto

**New** (`src/K4os.Stateful.Tests/`):
- `DslBuilderTests.cs` — DSL configuration, guard AND semantics, build immutability
- `EventHandlerRankerTests.cs` — ranking algorithm: hierarchy precedence, class vs interface priority
- `ActivationTests.cs` — `Activation` type conversion helpers
- `MachineDefinitionTests.cs` — cache behavior
