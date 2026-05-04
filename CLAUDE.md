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
dotnet test src/K4os.Stateful.Tests/K4os.Stateful.Tests.csproj --filter "FullyQualifiedName~CalculatorTests"
```

## Architecture

K4os.Stateful is a **hierarchical state machine library** for .NET. The key differentiator is that both states and events are typed objects (not enums), enabling polymorphic matching via class inheritance.

### Core Abstraction

The entire library lives in a single static generic class `StateMachine<TContext, TState, TEvent>` split across multiple partial class files (`StateMachine.*.cs`).

```
TContext  — user-provided shared data accessible everywhere
TState    — base class for all states (states can hold data as fields)
TEvent    — base class for all events (events can carry data as fields)
```

### Two-Phase Design

**Phase 1 — Configuration** (`StateMachine.Configurator.cs`)
- `NewConfigurator()` creates an `IConfigurator`
- States are registered with `In<TActualState>()` → returns `IStateConfigurator<TActualState>`
  - `.OnEnter(action)` / `.OnExit(action)` — lifecycle hooks
  - `.On<TActualEvent>()` — shorthand for `On<TActualState, TActualEvent>()`
- Events are registered with `On<TActualState, TActualEvent>()` → returns `IEventConfigurator<TActualState, TActualEvent>`
  - `.When(predicate)` — guard condition
  - `.OnTrigger(action)` — side effect without transition
  - `.Goto(func)` — transition to new state
  - `.Loop()` — stay in same state (skips OnExit/OnEnter)
- Configurations stored in two dictionaries: `_states` (Type → config) and `_events` (EventKey → List<config>)

**Phase 2 — Execution** (`StateMachine.Executor.cs`)
- `configurator.NewExecutor(context, initialState)` creates an `IExecutor` (extension method in `StateMachineExtenders.cs`)
- `executor.Fire(event)` dispatches an event:
  1. Resolves matching state/event configs by **type inheritance distance** (closest match wins)
  2. Evaluates guard predicates (`.When(...)`)
  3. Runs all matching `.OnTrigger(...)` handlers
  4. Selects the single closest-match transition; throws `InvalidOperationException` if none or if two configs tie at the same distance
  5. Executes: `OnExit` → `Goto` → `OnEnter` (or nothing extra for `.Loop()`)

### Hierarchical Matching

Handlers are resolved by walking the type hierarchy. A handler registered for `In<Animal>()` fires when the current state is `Dog : Animal` (distance = 1). A handler for `In<Dog>()` fires at distance 0 and takes priority. This applies to both state and event types independently.

Implementation: `Internal/ReflectionExtender.cs` — `TypeDistance()` walks base types/interfaces with BFS and caches results in `CachedTypeDistanceMap`.

> Avoid using interfaces for rule definitions — interface distance is ambiguous (the library assumes the longest inheritance path).

### Context Objects Passed to Callbacks

- `IStateContext<TActualState>` — provides `.Context` and `.State`
- `IEventContext<TActualState, TActualEvent>` — extends above, adds `.Event`

### Internal Utilities

- `Internal/EventKey.cs` — struct used as dictionary key for (stateType, eventType) pairs
- `Internal/CollectionExtender.cs` — `TryGet` with `TryGetMode` (None/First/Single/Exact)
- `Internal/ContractExtender.cs` — null/argument validation

## Tests

XUnit 2.5 in `src/K4os.Stateful.Tests/`:

- `CalculatorTests.cs` — end-to-end example: expression calculator state machine
- `ConfigurationTests.cs` — configuration API contract and error cases
- `KotlinTests.cs` — comprehensive behavioral tests: lifecycle ordering, hierarchical dispatch, guards, loop vs goto
