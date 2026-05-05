# K4os.Stateful — Modernization Plan

> **Status:** Draft
> **Last updated:** 2026-05-04
> **Reference design:** [mechanicus-design.md](mechanicus-design.md)
> **Implementation layers:** [mechanicus-implementation-plan.md](mechanicus-implementation-plan.md)

---

## Current State (as-is)

The existing library at `src/K4os.Stateful/` is a working, synchronous state machine with:

- `StateMachine<TContext, TState, TEvent>` — static generic container class
- `NewConfigurator()` → fluent config via `In<T>()` / `On<T,E>()` / `When()` / `Goto()` / `Loop()` / `OnEnter()` / `OnExit()` / `OnTrigger()`
- `NewExecutor(config, ctx, state)` — creates and starts executor, fires `OnEnter` on creation
- `IStateContext<T>` / `IEventContext<T,E>` — callback parameter objects
- Type-hierarchy matching via BFS distance (state distance + event distance, sorted separately)
- Three tests files covering lifecycle ordering, guard evaluation, and a calculator example

---

## Gap Analysis

The gaps are grouped by severity: **Breaking** (API must change), **Missing** (new capability needed), and **Behavioral** (semantics must change without necessarily changing method names).

---

### Breaking — API surface changes

#### B1. Entry point and freeze model

| Current | Target |
|---|---|
| `StateMachine<C,S,E>.NewConfigurator()` | `StateMachine.Define<C,S,E>()` → `IMachineConfig` |
| `config.NewExecutor(ctx, state)` (ext method) | `sm.Create()` → executor; `executor.Start(ctx, state)` |
| No frozen definition object | `Build()` returns immutable `StateMachine<C,S,E>` |

The current library has no "frozen" definition object — configuration and execution are loosely coupled via `IConfigurationProvider`. The design requires a two-step freeze: `Define()` → `Build()` → `Create()`.

#### B2. DSL method renames and restructuring

| Current | Target | Note |
|---|---|---|
| `Goto(fn)` | `GoTo(fn)` | Capitalisation |
| `Loop()` | `Stay()` | Semantics changed (see B5) |
| `OnTrigger(action)` | *(removed)* | Side effects absorbed into `GoTo`/`Stay` callbacks |
| `Name(string)` | *(removed)* | No label/visualization feature in target |
| `On<TState, TEvent>()` (top-level) | *(removed)* | Only `In<T>().On<E>()` chain |

#### B3. Return types in the fluent chain

| Current | Target |
|---|---|
| `GoTo`/`Loop` return `IEventConfigurator<S,E>` (stay on event facet) | `GoTo`/`Stay` return `IStateConfig<S>` (return to state facet, enable chaining `.On<E>()` or `.In<S>()`) |
| `OnEnter`/`OnExit` return `IStateConfigurator` | Same concept; `OnEnter`/`OnExit` return `IStateConfig<S>` |
| No `Build()` on state/event facets | `Build()` available everywhere in chain |

#### B4. Callback parameter objects

| Current | Target |
|---|---|
| `IStateContext<TActualState>` — `.Context`, `.State` | `StateActivation<TContext, TCurrentState>` — `.Context`, `.State`, `.CancellationToken` |
| `IEventContext<TActualState, TActualEvent>` — extends above, adds `.Event` | `Activation<TContext, TCurrentState, TCurrentEvent>` — `.Context`, `.State`, `.Event`, `.CancellationToken` |

The `CancellationToken` field is new and must flow from every `FireAsync` call to every lambda in the chain.

#### B5. `Stay()` / `Loop()` semantics

| Current `Loop()` | Target `Stay()` |
|---|---|
| Signals "no transition" explicitly; executor skips OnExit/OnEnter | Returns the same object reference; state-change predicate (`!ReferenceEquals`) naturally suppresses OnExit/OnEnter |
| No callback option | `Stay(callback?)` — optional side-effect callback; state reference unchanged |

The distinction matters: `Stay()` is now a terminal that the executor treats like any `GoTo`, but the predicate outcome controls whether entry/exit fires.

#### B6. `IGuardedEventConfig` facet

Current: `.When()` returns the same `IEventConfigurator` — a second `.When()` is allowed and silently overwrites the first.

Target: `.When()` returns `IGuardedEventConfig<S,E>` which has only `GoTo` and `Stay` — no second `.When()` is possible at compile time.

#### B7. `OnEnter` / `OnExit` single vs multiple

| Current | Target |
|---|---|
| Only one `OnEnter` per state; throws `InvalidOperationException` on second call | Multiple `OnEnter`/`OnExit` per state type; all fire in declaration order |

#### B8. Error types

| Current | Target |
|---|---|
| `InvalidOperationException` on unhandled event | `UnhandledEventException` |
| `InvalidOperationException` on ambiguous transition | *(removed — declarationOrder tie-break makes ambiguity impossible)* |
| No concurrent-fire detection | `ConcurrentFireException` on overlapping `FireAsync` calls |

---

### Missing — new capabilities

#### M1. Async-native callbacks

All callbacks are currently `Action<>` / `Func<>` (synchronous). Every callback must support `async Task` / `ValueTask`.

- Interface methods declare `ValueTask` / `ValueTask<T>` forms
- Extension methods wrap sync `Action`, sync `Func`, and `Task`-returning overloads
- The executor must `await` every callback
- This is the single most pervasive change — touches every layer

#### M2. `FireAsync` / `TryFireAsync` primary API

| Current | Target |
|---|---|
| `executor.Fire(event)` — sync only | `executor.FireAsync(event, ct)` — primary async API |
| *(none)* | `executor.TryFireAsync(event, ct)` — returns `bool`, no throw on miss |
| *(none)* | `executor.Fire(event, ct)` — sync convenience wrapper |
| *(none)* | `executor.TryFire(event, ct)` — sync convenience wrapper |

#### M3. `executor.Start()` does NOT fire `OnEnter`

Current `NewExecutor(config, ctx, state)` fires `OnEnter` for the initial state. The target `executor.Start(ctx, state)` explicitly does **not** fire `OnEnter` — it positions the executor without a "transition-in" event. This is required to support deserialized state restore without phantom entry handlers.

#### M4. `WithStateChangeIf` — configurable state-change predicate

Current: entry/exit fires whenever state changes (any transition that isn't `Loop()`).

Target: `WithStateChangeIf((s1, s2) => !ReferenceEquals(s1, s2))` — a user-configurable predicate, with `!ReferenceEquals` as the default. Controls whether entry/exit fires for any given `(previousState, nextState)` pair.

This unlocks:
- ETag / version-based change detection
- Value-equality semantics
- Always-fire (even on conceptual no-ops)
- Type-change-only (classic FSM semantics)

#### M5. `.Auto()` — completion transitions

A state can declare a handler that fires automatically after `OnEnter` completes, without any external event. Enables routing states and multi-step initialization pipelines.

```
FireAsync(event)
  → GoTo → new state S1
  → predicate: changed? → OnExit(old), OnEnter(S1)
  → Auto(S1)? → returns S2 (different ref)
    → predicate: changed? → OnExit(S1), OnEnter(S2)
    → Auto(S2)? → ...
  → return to caller
```

`Start()` does **not** trigger `Auto`.

#### M6. Thread safety — `ConcurrentFireException`

The executor is single-threaded by contract. Concurrent `FireAsync` calls on the same instance must be detected via `Interlocked` and throw `ConcurrentFireException`.

---

### Behavioral — semantics change, same names

#### V1. Rule matching: all-fire → first-match wins

| Current | Target |
|---|---|
| ALL matching `OnTrigger` callbacks fire; then ONE transition is selected by minimum distance | ONE rule fires — rules sorted by 4-part key; first rule whose guard passes is the winner (short-circuit) |
| Ambiguous transition (two rules at equal distance) → `InvalidOperationException` | Ambiguity impossible — `declarationOrder` is always a unique tie-breaker |

The `OnTrigger` concept is eliminated entirely. Side effects live inside `GoTo` or `Stay` callbacks.

#### V2. Rule sort key

| Current | Target |
|---|---|
| `(stateDistance, eventDistance, isFallback)` | `(distance, class/interface, hasGuard, declarationOrder)` |

"distance" in the target is the state type distance only (event type distance is implicit in the rule's specificity but not a separate sort dimension). The `class/interface` key reflects whether the registered type is a class or interface at that rank.

#### V3. Interface distance algorithm

| Current | Target |
|---|---|
| Interface distance = minimum BFS path | Interface distance = distance of the class that **introduces** the interface in the type hierarchy |

This changes how base-interface rules rank relative to base-class rules. An interface declared on `class A` gets distance equal to `A`, not the minimum path length.

#### V4. Entry/exit firing: order within rank

| Current | Target |
|---|---|
| Sort by distance; all handlers at same rank fire in unspecified order | Within same rank: interfaces fire before the class; secondary sort is declaration order |

---

## What Can Be Kept

The following components are sound and can be adapted rather than replaced:

| Component | Keep / adapt |
|---|---|
| `Internal/EventKey.cs` | Keep — (stateType, eventType) pair as dict key is still needed |
| `Internal/ContractExtender.cs` | Keep — null/arg validation |
| `Internal/CollectionExtender.cs` | Adapt — `GetOrCreate` still useful; `Iterate` less so with async |
| `Internal/ReflectionExtender.cs` | Adapt — distance algorithm needs the "introducing class" fix (V3); caching strategy is sound |
| Test structure (xunit, three files) | Keep — expand in place |
| `CalculatorTests.cs` as integration example | Rewrite — demonstrates new API and async callbacks |

---

## Implementation Plan

This plan is structured to match the layers in `mechanicus-implementation-plan.md`. Each step is independently testable. Steps are listed in recommended execution order.

---

### Step 0 — Prepare the project

- [ ] Add `K4os.Stateful` and test project to a clean `net8.0` solution structure
- [ ] Add NuGet references: `xunit`, `Microsoft.NET.Test.Sdk`, `FluentAssertions` (or equivalent)
- [ ] Delete all existing `.cs` source files; the old API surface will not be preserved
- [ ] Keep `Internal/EventKey.cs` and `Internal/ContractExtender.cs` — they survive unchanged

The old code is structurally incompatible with the target API. A clean rewrite is cleaner than an incremental migration.

---

### Step 1 — Distance algorithm (Layer 1)

File: `Internal/ReflectionExtender.cs` (rewrite)

- Implement `TypeDistance(actualType, registeredType) → int?` returning `null` for unrelated types
- Rules:
  - Same type → 0
  - Each step up the class chain → +1
  - Interface → distance of the class that **introduces** it (not minimum BFS)
  - Unrelated → `null`
- Thread-safe cache (dictionary of `(actual, registered) → int?`)
- Unit tests: see Layer 1 concepts in `mechanicus-implementation-plan.md`

---

### Step 2 — Core data types

Files: `Activation.cs`, `StateActivation.cs`, `Exceptions.cs`

- `Activation<TContext, TCurrentState, TCurrentEvent>` — `.Context`, `.State`, `.Event`, `.CancellationToken`
- `StateActivation<TContext, TCurrentState>` — `.Context`, `.State`, `.CancellationToken`
- `UnhandledEventException`
- `ConcurrentFireException`

No logic — pure data types. No tests needed beyond compilation.

---

### Step 3 — DSL interfaces (Layer 4 interfaces only)

Files: inside `StateMachine<TContext, TState, TEvent>` as nested interfaces

- `IMachineConfig` — `In<T>()`, `WithStateChangeIf(pred)`, `Build()`
- `IStateConfig<TCurrentState>` — `OnEnter(fn)`, `OnExit(fn)`, `Auto(fn)`, `On<TEvent>()`, `In<T>()`, `Build()`
- `IEventConfig<TS, TE>` — `When(guard)`, `GoTo(fn)`, `Stay(fn?)`
- `IGuardedEventConfig<TS, TE>` — `GoTo(fn)`, `Stay(fn?)`

Define only the `ValueTask`-based interface members. Extension methods come in Step 4.

---

### Step 4 — DSL extension methods

File: `StateMachineExtensions.cs`

Provide sync and `Task<>`-returning overloads for:
- `When(Func<..., bool>)`, `When(Func<..., Task<bool>>)`
- `GoTo(Func<..., TState>)`, `GoTo(Func<..., Task<TState>>)`, `GoTo(Func<TCurrentState, TState>)`
- `Stay(Action<...>)`, `Stay(Func<..., Task>)`
- `OnEnter(Action<...>)`, `OnExit(Action<...>)`
- `Auto(Func<..., TState>)`, `Auto(Func<..., Task<TState>>)`

Unit tests: verify wrapped lambdas produce correct `ValueTask<>` results.

---

### Step 5 — Builder implementation (Layer 4 concrete)

Files: `StateMachine.Configurator.cs`, `StateMachine.StateBuilder.cs`, `StateMachine.EventBuilder.cs`

Implement the concrete builder classes behind the interfaces from Step 3:

- `MachineConfigBuilder` implements `IMachineConfig`
- `StateConfigBuilder<TS>` implements `IStateConfig<TS>`
- `EventConfigBuilder<TS, TE>` implements `IEventConfig<TS,TE>` and `IGuardedEventConfig<TS,TE>`

Each builder accumulates registered rules/handlers into an internal list (raw, unsorted). `Build()` freezes all lists into immutable arrays and returns a `StateMachine<C,S,E>` instance.

Internal rule/handler model (stored after `Build()`):
```
RuleEntry        { stateType, eventType, guard?, gotoFn, isStay, declarationIndex }
EnterHandler     { stateType, callback, declarationIndex }
ExitHandler      { stateType, callback, declarationIndex }
AutoHandler      { stateType, callback }
StateChangePred  { Func<TState, TState, bool> }
```

Unit tests (Layer 4): verify that calling DSL methods registers the expected entries; chained and disconnected styles produce identical rule sets.

---

### Step 6 — Rule ranking / sort (Layer 2)

File: `Internal/RuleRanker.cs`

Sort `RuleEntry` list by 4-part key given the actual runtime state type:

```
(distance(actualState, rule.stateType), isInterface(rule.stateType), !rule.hasGuard, rule.declarationIndex)
```

All ascending; `isInterface` treated as `1` for interface, `0` for class (class sorts first = lower value).

Unit tests: see Layer 2 concepts in `mechanicus-implementation-plan.md`.

---

### Step 7 — Guard evaluation (Layer 3)

File: `Internal/RuleEvaluator.cs`

Walk the sorted candidate list:
1. Filter to candidates where `distance != null` for both state and event
2. For each candidate in order: evaluate guard (await `ValueTask<bool>`)
3. First passing guard → winner; return it
4. All fail → return `null`

Unit tests: see Layer 3 concepts in `mechanicus-implementation-plan.md`.

---

### Step 8 — Entry/exit firing order (Layer 5)

File: `Internal/EntryExitFirer.cs`

Given an actual state type and lists of `EnterHandler`/`ExitHandler`:

1. For each handler whose `stateType` is assignable from actual: compute `distance(actual, stateType)`
2. Assign within-rank order: interface before class, then declaration order
3. Entry: sort by descending distance (far → close), fire all
4. Exit: sort by ascending distance (close → far), fire all

Unit tests: see Layer 5 concepts in `mechanicus-implementation-plan.md`.

---

### Step 9 — State-change predicate (Layer 6)

File: inline in `StateMachine.Executor.cs`

- Store `Func<TState, TState, bool>` predicate on the frozen definition (default: `!ReferenceEquals`)
- After each `GoTo`/`Stay` handler returns: call predicate with `(previousState, nextState)`
- If `true` → fire entry/exit pipeline (Step 8)
- If `false` → skip

Unit tests: see Layer 6 concepts in `mechanicus-implementation-plan.md`.

---

### Step 10 — Executor core (Layer 7)

File: `StateMachine.Executor.cs`

Implement `FireAsync(event, ct)`:

```
1. Interlocked check → ConcurrentFireException if already firing
2. Build candidate list for (actualStateType, eventType)
3. Sort (Step 6) + evaluate guards (Step 7) → winner rule
4. If no winner → UnhandledEventException
5. Capture previousState = executor.State
6. await winner.gotoFn(activation)  → nextState
7. executor.State = nextState
8. Evaluate predicate(previousState, nextState)
9. If changed: await ExitFire(previousState), await EnterFire(nextState)
10. Auto check (Step 12)
11. Release Interlocked
```

`TryFireAsync`: same, catch `UnhandledEventException`, return `false`.
Sync wrappers `Fire` / `TryFire`: call `.GetAwaiter().GetResult()`.

Unit tests: see Layer 7 concepts in `mechanicus-implementation-plan.md`.

---

### Step 11 — Unhandled events (Layer 8)

Covered by Step 10's `UnhandledEventException` path. Tests validate:
- `FireAsync` throws when no rule matches
- `TryFireAsync` returns `false` when no rule matches
- Catch-all rule suppresses exception

---

### Step 12 — Auto transitions (Layer 14)

File: inline in `StateMachine.Executor.cs`, after entry/exit firing

After `OnEnter` completes for a new state, check if an `AutoHandler` is registered:

```
while (auto handler exists for executor.State.GetType()):
    await auto(stateActivation)  → nextState
    if predicate(current, nextState) is true:
        await ExitFire(current), set executor.State = nextState, await EnterFire(nextState)
    else:
        break  // same reference → stop chain
```

`Start()` does **not** invoke `Auto`.

Unit tests: see Layer 14 concepts in `mechanicus-implementation-plan.md`.

---

### Step 13 — Executor lifecycle (Layer 9)

Implement `sm.Create()` and `executor.Start(ctx, state)`:
- `Create()` returns an uninitialised executor bound to the frozen definition
- `Start(ctx, state)` sets `Context` and `State` without firing `OnEnter` or `Auto`

Unit tests: see Layer 9 concepts in `mechanicus-implementation-plan.md`.

---

### Step 14 — Thread safety (Layer 10)

Implement `Interlocked`-based concurrent-fire guard in `FireAsync` (Step 10 already allocates the slot).

Unit tests: see Layer 10 concepts in `mechanicus-implementation-plan.md`.

---

### Step 15 — Error propagation (Layer 11)

Verify that:
- Exception in `GoTo` → propagates; `executor.State` is unchanged (capture `previousState`, only assign after success)
- Exception in `When` → propagates; no state change
- Exception in `OnExit` → propagates; `OnEnter` does NOT run
- Exception in `OnEnter` → propagates

Unit tests: see Layer 11 concepts in `mechanicus-implementation-plan.md`.

---

### Step 16 — CancellationToken (Layer 12)

The `ct` passed to `FireAsync(event, ct)` is threaded into every `Activation`/`StateActivation` constructed during that fire. Verify:
- Token reaches every lambda type (`GoTo`, `Stay`, `When`, `OnEnter`, `OnExit`, `Auto`)
- Cancellation in `GoTo` propagates as `OperationCanceledException`; state unchanged

Unit tests: see Layer 12 concepts in `mechanicus-implementation-plan.md`.

---

### Step 17 — Rewrite tests and example

- Rewrite `CalculatorTests.cs` using the new async API
- Rewrite `KotlinTests.cs` covering the behavioral changes (V1–V4)
- Rewrite `ConfigurationTests.cs` covering DSL registration and `.Build()` freeze

---

### Step 18 — Nested states (Layer 13, documentation)

No library change needed — nested states are a data pattern. Add a test that:
- Carries a `Parent` field in a nested state record
- Returns to parent via `GoTo(x => x.State.Parent)`
- Verifies entry/exit fires for parent on return
- Round-trips through `System.Text.Json`

---

## Out of Scope (by design)

These Stateless features are **explicitly not supported** in the target design:

- DOT / Mermaid graph visualization
- `GetInfo()` / `CanFire()` / `GetPermittedTriggers()` reflection API
- `OnActivate` / `OnDeactivate`
- `OnEntryFrom(trigger)` — per-trigger entry
- `OnTransitioned` / `OnTransitionCompleted` cross-cutting hooks
- `OnUnhandledTrigger` callback
- `FiringMode.Queued` — caller serializes access
- Guard descriptions / metadata
- `RetainSynchronizationContext`
- `WithMaxAutoDepth(n)` — infinite-loop guard for Auto chains (acknowledged risk, deferred)

---

## File Layout (target)

```
src/K4os.Stateful/
  StateMachine.cs                   — static entry point; Define<C,S,E>()
  StateMachine.Interfaces.cs        — IMachineConfig, IStateConfig<T>, IEventConfig<S,E>, IGuardedEventConfig<S,E>
  StateMachine.Configurator.cs      — MachineConfigBuilder
  StateMachine.StateBuilder.cs      — StateConfigBuilder<TS>
  StateMachine.EventBuilder.cs      — EventConfigBuilder<TS,TE>
  StateMachine.Executor.cs          — Executor; FireAsync, TryFireAsync, Start, State
  Activation.cs                     — Activation<C,S,E> and StateActivation<C,S>
  Exceptions.cs                     — UnhandledEventException, ConcurrentFireException
  StateMachineExtensions.cs         — sync / Task<> overloads for DSL methods
  Internal/
    EventKey.cs                     — (stateType, eventType) dict key [keep]
    ReflectionExtender.cs           — distance algorithm (rewrite)
    RuleRanker.cs                   — 4-part rule sort
    EntryExitFirer.cs               — hierarchical entry/exit order
    ContractExtender.cs             — null/arg validation [keep]
    CollectionExtender.cs           — GetOrCreate, etc. [adapt]
```
