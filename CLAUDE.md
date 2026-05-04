# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build System

This project uses **FAKE** (F# make) with **Paket** for dependency management.

```
fake build           # Default: restore + build + test
fake build Clean     # Remove /bin and /obj
fake build Restore   # Restore NuGet via Paket
fake build Test      # Run XUnit tests only
fake build Rebuild   # Full clean + restore + build + test
fake build Release   # Pack NuGet packages
```

- Build script: `build.fsx`
- Dependencies: `paket.dependencies`
- Version: `Common.targets`

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
- Events are registered with `On<TActualState, TActualEvent>()` → returns `IEventConfigurator<TActualState, TActualEvent>`
  - `.When(predicate)` — guard condition
  - `.OnTrigger(action)` — side effect without transition
  - `.Goto(func)` — transition to new state
  - `.Loop()` — stay in same state (skips OnExit/OnEnter)
- Configurations stored in two dictionaries: `_states` (Type → config) and `_events` (EventKey → List<config>)

**Phase 2 — Execution** (`StateMachine.Executor.cs`)
- `configurator.NewExecutor(context, initialState)` creates an `IExecutor`
- `executor.Fire(event)` dispatches an event:
  1. Resolves matching state/event configs by **type inheritance distance** (closest match wins)
  2. Evaluates guard predicates (`.When(...)`)
  3. Runs triggers (non-transitioning `.OnTrigger(...)` handlers)
  4. Executes transition: `OnExit` → `Goto` → `OnEnter`

### Hierarchical Matching

The library resolves handlers by walking the type hierarchy. If you have:
```
class Animal : State {}
class Dog : Animal {}
```
A handler registered for `In<Animal>()` fires when the current state is `Dog` (distance = 1). A handler registered for `In<Dog>()` fires with distance 0 and takes priority. This applies to both states and events independently.

Implementation: `Internal/ReflectionExtender.cs` — `TypeDistance()` walks base types/interfaces with BFS and caches results in `CachedTypeDistanceMap`.

### Context Objects Passed to Callbacks

- `IStateContext<TActualState>` — provides `.Context` and `.State`
- `IEventContext<TActualState, TActualEvent>` — extends above, adds `.Event`

### Internal Utilities

- `Internal/EventKey.cs` — struct used as dictionary key for (stateType, eventType) pairs
- `Internal/CollectionExtender.cs` — `TryGet` with `TryGetMode` (None/First/Single/Exact)
- `Internal/ContractExtender.cs` — null/argument validation

## Tests

XUnit 2.0 in `src/K4os.Stateful.Test/`:

- `CalculatorTests.cs` — end-to-end example: expression calculator state machine
- `ConfigurationTests.cs` — configuration API contract and error cases
- `KotlinTests.cs` — comprehensive behavioral tests: lifecycle ordering, hierarchical dispatch, guards, loop vs goto

To run a single test class or method, use dotnet directly:
```
dotnet test src/K4os.Stateful.Test/K4os.Stateful.Test.csproj --filter "FullyQualifiedName~CalculatorTests"
```
